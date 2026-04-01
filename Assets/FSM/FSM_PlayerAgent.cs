using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// FSM-based player agent. Replaces the Behaviour-Tree version (PlayerAgent).
/// States are evaluated top-down each tick; the highest-priority valid state wins.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class FSM_PlayerAgent : MonoBehaviour
{

    public enum AgentState
    {
        Idle,
        HasBall,
        ChaseBall,
        Support,
        Press,
        Tackle,
        TackleStunned,
        ReturnToFormation
    }

    [Header("Identity")]
    public FSM_Blackboard.Team team = FSM_Blackboard.Team.A;
    public Role role = Role.Midfielder;

    [Header("Stats (from SO)")]
    public RoleStats roleStats;

    [Header("Formation")]
    public Vector2 formationWorldPos;

    [Header("Natural Movement")]
    [Range(0f, 0.4f)] public float wobbleAmount = 0.18f;
    [Range(1f, 15f)] public float wobbleFrequency = 7.5f;
    //  Private runtime fields
    [HideInInspector] public Rigidbody2D rb;
    [HideInInspector] public Vector2 facing = Vector2.right;

    // Derived stats (filled by ApplyRoleStats)
    private float maxSpeed;
    private float kickPower;
    private float shootDistance;
    private float tackleChance;
    private float pressDistance;

    // Timers
    private float emergencyPassCooldown = 0f;
    private float postTackleAttackTimer = 0f;
    private float tackleCooldownTimer = 0f;
    private const float TackleStun = 1.2f;

    // Movement helpers
    private float wobblePhase = 0f;
    private Vector2 dribbleWobbleOffset = Vector2.zero;

    // FSM
    public AgentState currentState = AgentState.Idle;
    public string debugState = "Idle";   // shown in gizmo

    // References
    private FSM_BallController ball;

    //  Unity lifecycle
    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
    }

    void Start()
    {
        ApplyRoleStats();
        ball = Object.FindFirstObjectByType<FSM_BallController>();
        wobblePhase = Random.Range(0f, 10f);
        RegisterToBlackboard();
    }

    void RegisterToBlackboard()
    {
        var bb = FSM_Blackboard.Instance;
        if (bb == null) return;
        if (team == FSM_Blackboard.Team.A) bb.teamAAgents.Add(this);
        else bb.teamBAgents.Add(this);
    }

    //  Main tick
    void Update()
    {
        TickTimers();
        TransitionState();
        ExecuteState();

        if (rb.velocity.sqrMagnitude > 0.01f)
            facing = rb.velocity.normalized;
    }
    //  Timer management
    void TickTimers()
    {
        if (postTackleAttackTimer > 0f) postTackleAttackTimer -= Time.deltaTime;
        if (emergencyPassCooldown > 0f) emergencyPassCooldown -= Time.deltaTime;

        if (tackleCooldownTimer > 0f)
        {
            tackleCooldownTimer -= Time.deltaTime;
            rb.velocity = Vector2.zero;   // stun lock
        }
    }

    //  FSM: transition logic  (priority order  top wins)
    void TransitionState()
    {
        if (FSM_Blackboard.Instance.resetTimer > 0)
        {
            SetState(AgentState.ReturnToFormation);
            return;
        }
        // 1. STUN: Always highest priority
        if (tackleCooldownTimer > 0f)
        {
            SetState(AgentState.TackleStunned);
            return;
        }

        // 2. SELF: If I am the one holding the ball
        if (ball.currentHolder == this)
        {
            SetState(AgentState.HasBall);
            return;
        }

        // 3. LOOSE BALL: If nobody has the ball, chase it if close
        if (ball.currentHolder == null)
        {
            float distToBall = Vector2.Distance(transform.position, ball.transform.position);
            bool isClosest = FSM_Blackboard.Instance.GetClosestToBall(team) == this;

            if (isClosest || distToBall < 10f)
            {
                SetState(AgentState.ChaseBall);
                return;
            }
        }

        // 4. TEAMMATE HAS BALL: If a friend has it, support them
        // Note: Use 'else if' or a separate check here to ensure we don't 'Support' 
        // while the ball is loose or held by an enemy.
        else if (ball.currentHolder.team == team)
        {
            SetState(AgentState.Support);
            return;
        }

        // 5. OPPONENT HAS BALL: Defensive behavior
        if (ball.currentHolder != null && ball.currentHolder.team != team)
        {
            float distToOpponent = Vector2.Distance(transform.position, ball.currentHolder.transform.position);

            if (distToOpponent <= 1.2f)
            {
                SetState(AgentState.Tackle);
            }
            else if (distToOpponent <= pressDistance * 1.2f || IsInOurDefensiveZone())
            {
                SetState(AgentState.Press);
            }
            else
            {
                SetState(AgentState.ReturnToFormation);
            }
            return;
        }

        // 6. DEFAULT: If none of the above are true, go to your spot
        SetState(AgentState.ReturnToFormation);
    }
    void SetState(AgentState next)
    {
        if (currentState == next) return;
        currentState = next;
    }

    //  FSM: execute current state
    void ExecuteState()
    {
        switch (currentState)
        {
            case AgentState.Idle: debugState = "Idle"; break;
            case AgentState.TackleStunned: debugState = "Stunned"; break;
            case AgentState.HasBall: ExecuteHasBall(); break;
            case AgentState.ChaseBall: ExecuteChaseBall(); break;
            case AgentState.Support: ExecuteSupport(); break;
            case AgentState.Press: ExecutePress(); break;
            case AgentState.Tackle: ExecuteTackle(); break;
            case AgentState.ReturnToFormation: ExecuteReturnToFormation(); break;
        }
    }
//state
    void ExecuteHasBall()
    {
        // After winning a tackle we get an aggressive sub-priority: pass/dribble/shoot quickly
        // Priority: Shoot if in range pass to better-positioned teammate dribble
        if (CanShoot())
        {
            DoShoot();
            return;
        }
        if (TryPass()) return;
        DoDribble();
    }

    bool CanShoot()
    {
        Vector2 goal = GetOurAttackingGoal();
        return Vector2.Distance(transform.position, goal) < shootDistance;
    }

    void DoShoot()
    {
        Vector2 goal = GetOurAttackingGoal();
        ball.KickTowards(goal, kickPower);
        debugState = "Shoot";
    }

    bool TryPass()
    {
        Vector3 goalPos = GetOurAttackingGoal();

        // Check if WE are in the opponent's defensive zone (usually within ~20 units of their goal)
        bool inOpponentZone = Vector2.Distance(transform.position, goalPos) < 20f;

        var teammates = team == FSM_Blackboard.Team.A
            ? FSM_Blackboard.Instance.teamAAgents
            : FSM_Blackboard.Instance.teamBAgents;

        FSM_PlayerAgent bestTeammate = null;
        float bestScore = float.MaxValue;

        foreach (var mate in teammates)
        {
            if (mate == this || mate == null) continue;

            float distToGoal = Vector2.Distance(mate.transform.position, goalPos);
            float MyDistToGoal = Vector2.Distance(transform.position, goalPos);

            // --- NEW FORWARD PASS RESTRICTION ---
            // If we are in the opponent's zone, don't pass to anyone further from the goal than us
            if (inOpponentZone && distToGoal > MyDistToGoal)
            {
                continue; // Skip this teammate, they are "backward"
            }
            // -------------------------------------

            float passDist = Vector2.Distance(transform.position, mate.transform.position);
            float score = distToGoal * 2.5f
                          + passDist * 0.6f
                          + mate.CountOpponentsNearby(5f) * 4f;

            if (score < bestScore) { bestScore = score; bestTeammate = mate; }
        }
    

        if (bestTeammate == null) return false;

        float myDistToGoal = Vector2.Distance(transform.position, goalPos);
        float bestDistToGoal = Vector2.Distance(bestTeammate.transform.position, goalPos);

        // Prefer shooting over passing when very close
        if (myDistToGoal < 4f) return false;
        // Don't pass to someone further from goal
        if (myDistToGoal < bestDistToGoal - 4f) return false;

        Vector2 passDir = (bestTeammate.transform.position - transform.position).normalized;
        Vector2 goalDir = ((Vector2)goalPos - (Vector2)transform.position).normalized;
        float forwardDot = Vector2.Dot(passDir, goalDir);

        if (forwardDot < 0.35f) return false;
        if (bestTeammate.CountOpponentsNearby(3.5f) >= 3) return false;

        ball.ReleaseWithForce(passDir, kickPower * 1.7f);
        debugState = "Pass";
        return true;
    }

    void DoDribble()
    {
        // Emergency pass when swarmed
        if (emergencyPassCooldown <= 0f)
        {
            int danger = CountOpponentsNearby(4f);
            if (danger >= 2)
            {
                FSM_PlayerAgent nearest = GetNearestTeammate();
                if (nearest != null && CountPlayer(nearest, 4f) <= 2)
                {
                    ball.ReleaseWithForce(
                        (nearest.transform.position - transform.position).normalized,
                        kickPower * 1.8f);
                    emergencyPassCooldown = 1.0f;
                    debugState = "Emergency Pass";
                    return;
                }
            }
        }

        // Dribble toward attacking goal with slight wobble
        if (Time.time % 0.35f < Time.deltaTime)
            dribbleWobbleOffset = new Vector2(0, Random.Range(-2.5f, 2.5f));

        Vector2 goal = GetOurAttackingGoal();
        Vector2 target = goal + dribbleWobbleOffset;
        MoveTowards(target, 2f);
        debugState = "Dribbling";
    }


    void ExecuteChaseBall()
    {
        MoveTowards(ball.transform.position, 0.5f);
        debugState = "Chasing Ball";
    }


    void ExecuteSupport()
    {
        if (ball.currentHolder == null) return;

        float distToDribbler = Vector2.Distance(transform.position, ball.currentHolder.transform.position);

        // 1. HARD STOP if too close to teammate
        if (distToDribbler < 5f)
        {
            rb.velocity = Vector2.Lerp(rb.velocity, Vector2.zero, Time.deltaTime * 2f);
            debugState = "Giving Space";
            return;
        }

        // 2. Move to formation, NOT the ball
        // This keeps the winger on the wing and the defender in the back
        Vector2 targetPos = formationWorldPos;

        // Shift the formation forward slightly as the ball moves up
        float ballProgress = ball.transform.position.x; // Assuming X is forward
        targetPos.x += (ballProgress * 0.3f);

        MoveTowards(targetPos);
        debugState = "Supporting (In Position)";
    }


    void ExecutePress()
    {
        if (ball.currentHolder == null || ball.currentHolder.team == team)
        {
            // Target gone — will retransition next frame
            return;
        }
        MoveTowards(ball.currentHolder.transform.position, 1.2f);
        debugState = "Pressing";
    }

    // Tackle 
    void ExecuteTackle()
    {
        if (ball.currentHolder == null || ball.currentHolder.team == team) return;

        float tackleRange = 0.1f;
        float dist = Vector2.Distance(transform.position, ball.currentHolder.transform.position);

        if (dist > tackleRange + 0.3f)
        {
            MoveTowards(ball.currentHolder.transform.position, tackleRange);
            debugState = "Closing in";
            return;
        }

        if (dist <= tackleRange)
        {
            float chance = tackleChance;
            if (ball.currentHolder.rb.velocity.magnitude > 3f) chance *= 0.65f;
            if (rb.velocity.magnitude < 2f) chance *= 1.2f;
            chance = Mathf.Clamp(chance, 0.25f, 0.85f);

            if (Random.value < chance)
            {
                ball.GiveTo(this);
                postTackleAttackTimer = 1.0f;
                debugState = "Tackle WON";
                // State will flip to HasBall next frame via transition
            }
            else
            {
                tackleCooldownTimer = TackleStun;
                debugState = "Tackle FAILED";
            }
        }
    }

    void ExecuteReturnToFormation()
    {
        // Bail immediately if we somehow got the ball
        if (ball.currentHolder == this) return;
        MoveTowards(formationWorldPos);
        debugState = "Returning";
    }

    //  Movement helper

    bool MoveTowards(Vector2 target, float acceptDistance = 0.1f)
    {
        Vector2 dir = target - rb.position;
        float dist = dir.magnitude;

        if (dist < acceptDistance)
        {
            rb.velocity = Vector2.zero;
            return true;
        }

        dir.Normalize();
        wobblePhase += Time.deltaTime * wobbleFrequency;

        float wobble = Mathf.Sin(wobblePhase) * wobbleAmount;
        Vector2 perp = new Vector2(-dir.y, dir.x);
        Vector2 wobbledDir = (dir + perp * wobble).normalized;
        float speedVariation = 1f + Mathf.Sin(wobblePhase * 0.7f) * 0.07f;

        rb.velocity = Vector2.Lerp(rb.velocity, wobbledDir * maxSpeed * speedVariation, Time.deltaTime * 5f);
        return false;
    }

    //  Public callbacks
    public void OnGainBall(FSM_BallController b)
    {
        debugState = "HasBall";
        currentState = AgentState.HasBall;
    }

    //  Helpers    /// <summary>Returns the goal this team is attacking (not defending).</summary>
    Vector2 GetOurAttackingGoal()
    {
        // Swap these! Team A should attack Goal B, Team B attacks Goal A.
        return team == FSM_Blackboard.Team.A
            ? FSM_Blackboard.Instance.goalBPosition
            : FSM_Blackboard.Instance.goalAPosition;
    }

    bool IsInOurDefensiveZone()
    {
        // "Our" defensive goal is the one we are defending (opposite of attacking)
        Vector2 ourGoal = team == FSM_Blackboard.Team.A
            ? FSM_Blackboard.Instance.goalBPosition
            : FSM_Blackboard.Instance.goalAPosition;
        return Vector2.Distance(ball.transform.position, ourGoal) < 15f;
    }

    public int CountOpponentsNearby(float radius)
    {
        var opponents = team == FSM_Blackboard.Team.A
            ? FSM_Blackboard.Instance.teamBAgents
            : FSM_Blackboard.Instance.teamAAgents;

        int count = 0;
        foreach (var opp in opponents)
        {
            if (opp == this) continue;
            if (Vector2.Distance(transform.position, opp.transform.position) <= radius)
                count++;
        }
        return count;
    }

    int CountPlayer(FSM_PlayerAgent player, float radius)
    {
        var opponents = player.team == FSM_Blackboard.Team.A
            ? FSM_Blackboard.Instance.teamBAgents
            : FSM_Blackboard.Instance.teamAAgents;

        int count = 0;
        foreach (var opp in opponents)
        {
            if (opp == player) continue;
            if (Vector2.Distance(player.transform.position, opp.transform.position) <= radius)
                count++;
        }
        return count;
    }

    FSM_PlayerAgent GetNearestTeammate()
    {
        var mates = team == FSM_Blackboard.Team.A
            ? FSM_Blackboard.Instance.teamAAgents
            : FSM_Blackboard.Instance.teamBAgents;

        FSM_PlayerAgent closest = null;
        float bestDist = float.MaxValue;

        foreach (var t in mates)
        {
            if (t == this) continue;
            float d = Vector2.Distance(transform.position, t.transform.position);
            if (d < bestDist) { bestDist = d; closest = t; }
        }
        return closest;
    }

    //  Stats application (supports optional tactic multipliers)
    public void ApplyRoleStats(TeamTactic currentTactic = null)
    {
        if (roleStats == null)
        {
            Debug.LogWarning($"{name} has no RoleStats assigned for role {role}!");
            return;
        }

        float speedMult = currentTactic?.speedMultiplier ?? 1f;
        float kickMult = currentTactic?.kickPowerMultiplier ?? 1f;
        float shootMult = currentTactic?.shootDistanceMultiplier ?? 1f;
        float tackleMult = currentTactic?.tackleChanceMultiplier ?? 1f;
        float pressMult = currentTactic?.pressDistanceMultiplier ?? 1f;

        maxSpeed = roleStats.maxSpeed * speedMult;
        kickPower = roleStats.kickPower * kickMult;
        shootDistance = roleStats.shootDistance * shootMult;
        tackleChance = roleStats.tackleChance * tackleMult;
        pressDistance = roleStats.pressDistance * pressMult;

        Debug.Log($"{name} ({role}) stats applied | Speed:{maxSpeed:F1} Kick:{kickPower:F1} " +
                  $"Shoot:{shootDistance:F1} Tackle:{tackleChance:F2} Press:{pressDistance:F1}");
    }

    void OnTriggerEnter2D(Collider2D col)
    {
        var ballCtrl = col.GetComponent<FSM_BallController>();
        if (ballCtrl != null)
        {
            // ONLY take the ball if:
            // 1. No one has it (Loose ball)
            // OR 2. An opponent has it (Stealing/Tackling)
            if (ballCtrl.currentHolder == null || ballCtrl.currentHolder.team != this.team)
            {
                ballCtrl.GiveTo(this);
            }
        }
    }

    void OnDrawGizmos()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(transform.position, transform.position + (Vector3)facing * 0.6f);

#if UNITY_EDITOR
        UnityEditor.Handles.color = Color.white;
        UnityEditor.Handles.Label(transform.position + Vector3.up * 0.5f, debugState);
#endif
    }
}