using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerAgent : MonoBehaviour, IFootballAgent
{
    #region Variable
    public Blackboard.Team team = Blackboard.Team.A;
    public Role role = Role.Midfielder;
    public Rigidbody2D rb;
    private float maxSpeed;
    private float kickPower;
    private float ShootDistance;
    private float TackleChance;
    private float emergencyPassCooldown = 0f;
    private float postTackleAttackTimer = 0f;
    private float tackleCooldownTimer = 0f;      
    private const float TackleStun = 1.2f;
    private Vector2 dribbleWobbleOffset = Vector2.zero;
    [Header("Natural Movement")]
    [Range(0f, 0.4f)] public float wobbleAmount = 0.18f;      
    [Range(1f, 15f)] public float wobbleFrequency = 7.5f;  
    private float wobblePhase = 0f;
    private bool isTackleStunned => tackleCooldownTimer > 0f;

    Blackboard.Team IFootballAgent.team => team;

    Role IFootballAgent.role => role;

    Rigidbody2D IFootballAgent.rb => rb;

    Vector2 IFootballAgent.facing => facing;

    [Header("Stats (from SO)")]
    public RoleStats roleStats;
    [Header("Formation")]
    public Vector2 formationWorldPos; 
    private float pressDistance; 
    [HideInInspector] public Vector2 facing = Vector2.right;

    // BT
    private BTNode root;
    private BallController ball;
    public string debugState = "Idle";
    #endregion
    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
    }

    void Start()
    {
        ApplyRoleStats();
        ball = FindObjectOfType<BallController>();
        RegisterToBlackboard();
        BuildBehaviorTree();
        wobblePhase = Random.Range(0f, 10f);
    }

    void RegisterToBlackboard()
    {
        var bb = Blackboard.Instance;
        if (bb != null)
        {
            if (team == Blackboard.Team.A) bb.teamAAgents.Add(this);
            else if (team == Blackboard.Team.B) bb.teamBAgents.Add(this);
        }
    }
    #region Tree
    void BuildBehaviorTree()
    {
        #region condition
        var hasBall = new ConditionNode(() => ball.currentHolder == (IFootballAgent)this);
        var ballNearby = new ConditionNode(() => Vector2.Distance(transform.position, ball.transform.position) < 3f);
        var inShootingRange = new ConditionNode(() =>
        {
            Vector2 goal = (team == Blackboard.Team.A) ? Blackboard.Instance.goalAPosition : Blackboard.Instance.goalBPosition;
            return Vector2.Distance(transform.position, goal) < ShootDistance;
        });
        var ballLoose = new ConditionNode(() => ball.currentHolder == null);
        var opponentHasBall = new ConditionNode(() => ball.currentHolder != null && ball.currentHolder.team != team);
        var teamHasBall = new ConditionNode(() => ball.currentHolder != null && ball.currentHolder.team == team);
        var isClosestToBall = new ConditionNode(() => Blackboard.Instance.GetClosestToBall(team) == (IFootballAgent)this);

        var ballInOurDefensiveZone = new ConditionNode(() =>
        {
            Vector2 ourGoal = team == Blackboard.Team.A ? Blackboard.Instance.goalBPosition : Blackboard.Instance.goalAPosition;
            return Vector2.Distance(ball.transform.position, ourGoal) < 15f;
        });

        var isDefender = new ConditionNode(() => role == Role.Defender);
        var isNotDefender = new ConditionNode(() => role != Role.Defender); // <-- replaced Inverter
        var recentlyWonTackle = new ConditionNode(() => postTackleAttackTimer > 0f);
        var opponentInPressRange = new ConditionNode(() =>
        {
            if (ball.currentHolder == null || ball.currentHolder.team == team) return false;
            return Vector2.Distance(transform.position, ball.currentHolder.transform.position) <= pressDistance;
        });
        #endregion
        #region Action
        // ====== ACTIONS =====
        var actionSupport = new ActionNode(SupportMove);
        var actionShoot = new ActionNode(TryShoot);
        var actionPass = new ActionNode(TryPass);
        var actionDribble = new ActionNode(DoDribble);
        var actionTackle = new ActionNode(TryTackle);
        var actionPress = new ActionNode(PressBallCarrier);
        var actionChaseBall = new ActionNode(() =>
        {
            if (MoveTowards(ball.transform.position, 0.5f))
                return BTStatus.Success;
            debugState = "Chasing Loose Ball";
            return BTStatus.Running;
        });
        var actionReturnForm = new ActionNode(() =>
        {
            if (ball.currentHolder == (IFootballAgent)this)
                return BTStatus.Failure;
            if (MoveTowards(formationWorldPos))
                return BTStatus.Success;
            debugState = "Return to Formation";
            return BTStatus.Running;
        });
        #endregion
        #region Tree
        // ROOT TREE - NO InverterNode used
        root = new SelectorNode(
    new SequenceNode(hasBall, new SelectorNode(
    new SequenceNode(inShootingRange, actionShoot),
    actionPass,
    actionDribble
)),
        // Quick counter after winning tackle (you still have ball here too!)
        new SequenceNode(recentlyWonTackle, new SelectorNode(
            actionPass,
            actionDribble,
            actionShoot
        )),
        new SequenceNode(teamHasBall, actionSupport),
        // ===== Everything else only runs when you DO NOT have the ball =====
        new SequenceNode(ballLoose, ballNearby, actionChaseBall),
        new SequenceNode(ballLoose, isClosestToBall, actionChaseBall),

        // Defensive behaviours
        new SequenceNode(opponentHasBall, new SelectorNode(
            new SequenceNode(opponentInPressRange, actionTackle),
            new SequenceNode(isDefender, ballInOurDefensiveZone, actionPress),
            new SequenceNode(isNotDefender, opponentInPressRange, actionPress),
            actionReturnForm
        )),

        // Absolute final fallback
        actionReturnForm
        #endregion
    );
    }
    #endregion
    void Update()
    {
        if (root != null) root.Tick();
        if (postTackleAttackTimer > 0)
            postTackleAttackTimer -= Time.deltaTime;
        if (rb.velocity.magnitude > 0.01f)
            facing = rb.velocity.normalized;
        if (emergencyPassCooldown > 0f)
            emergencyPassCooldown -= Time.deltaTime;
        if (tackleCooldownTimer > 0f)
        {
            tackleCooldownTimer -= Time.deltaTime;

            // Optional: reduce speed while stunned
            if (tackleCooldownTimer > 0f)
                rb.velocity *= 0f;
        }

        if (rb.velocity.sqrMagnitude > 0.01f)
            facing = rb.velocity.normalized;
    }

    #region Movement and Action
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

    BTStatus PressBallCarrier()
    {
        if (ball.currentHolder == null || ball.currentHolder.team == team)
            return BTStatus.Failure;

        MoveTowards(ball.currentHolder.transform.position, 1.2f);
        debugState = "Pressing";
        return BTStatus.Running;
    }

    BTStatus SupportMove()
    {
        Vector2 oppGoal = (team == Blackboard.Team.A) ? Blackboard.Instance.goalAPosition : Blackboard.Instance.goalBPosition;
        Vector2 dir = (oppGoal - formationWorldPos).normalized;
        float distToGoal = Vector2.Distance(formationWorldPos, oppGoal);
        float pushDist = Mathf.Min(distToGoal * 0.7f, pressDistance);
        Vector2 supportTarget = formationWorldPos + dir * pushDist;

        if (MoveTowards(supportTarget))
            return BTStatus.Success;

        debugState = "Support";
        return BTStatus.Running;
    }

    public void OnGainBall(BallController b) => debugState = "HasBall";

    BTStatus TryShoot()
    {
        Vector2 goal = (team == Blackboard.Team.A) ? Blackboard.Instance.goalAPosition : Blackboard.Instance.goalBPosition;
        ball.KickTowards(goal, kickPower);
        debugState = "Shoot";
        return BTStatus.Success;
    }

    BTStatus TryPass()
    {
        Vector2 goalPos = (team == Blackboard.Team.A)
            ? Blackboard.Instance.goalAPosition
            : Blackboard.Instance.goalBPosition;

        var teammates = team == Blackboard.Team.A
            ? Blackboard.Instance.teamAAgents
            : Blackboard.Instance.teamBAgents;

        IFootballAgent bestTeammate = null;
        float bestScore = float.MaxValue;

        foreach (var teammate in teammates)
        {
            if (teammate == (IFootballAgent)this || teammate == null) continue;

            float distToGoalTeammate = Vector2.Distance(teammate.transform.position, goalPos);
            float passDistance = Vector2.Distance(transform.position, teammate.transform.position);
            float score = distToGoalTeammate * 2.5f
                        + passDistance * 0.6f
                        + CountOpponentsNearPosition(teammate.transform.position, 5f) * 4f;

            if (score < bestScore) { bestScore = score; bestTeammate = teammate; }
        }

        if (bestTeammate == null) return BTStatus.Failure;

        float distToGoalMe = Vector2.Distance(transform.position, goalPos);
        float distToGoalBest = Vector2.Distance(bestTeammate.transform.position, goalPos);

        if (distToGoalMe < 4f) return BTStatus.Failure;
        if (distToGoalMe < distToGoalBest - 4f) return BTStatus.Failure;

        Vector2 passDirection = (bestTeammate.transform.position - transform.position).normalized;
        Vector2 goalDirection = (goalPos - (Vector2)transform.position).normalized;

        if (Vector2.Dot(passDirection, goalDirection) < 0.35f) return BTStatus.Failure;
        if (CountOpponentsNearPosition(bestTeammate.transform.position, 3.5f) >= 3) return BTStatus.Failure;

        ball.ReleaseWithForce(passDirection, kickPower * 1.7f);
        debugState = "Pass";
        return BTStatus.Success;
    }

    BTStatus DoDribble()
    {
        if (emergencyPassCooldown <= 0f)
        {
            if (CountOpponentsNearby(4f) >= 2)
            {
                IFootballAgent nearest = GetNearestTeammate();
                if (nearest != null && CountOpponentsNearPosition(nearest.transform.position, 4f) <= 2)
                {
                    ball.ReleaseWithForce(
                        (nearest.transform.position - transform.position).normalized,
                        kickPower * 1.8f);
                    debugState = "Emergency Pass";
                    emergencyPassCooldown = 1.0f;
                    return BTStatus.Success;
                }
            }
        }

        if (Time.time % 0.35f < Time.deltaTime)
            dribbleWobbleOffset = new Vector2(0, Random.Range(-2.5f, 2.5f));

        Vector2 goal = (team == Blackboard.Team.A)
            ? Blackboard.Instance.goalAPosition
            : Blackboard.Instance.goalBPosition;

        if (MoveTowards(goal + dribbleWobbleOffset, 2f)) return BTStatus.Success;
        debugState = "Dribbling - Goal";
        return BTStatus.Running;
    }

    IFootballAgent GetNearestTeammate()
    {
        var mates = team == Blackboard.Team.A
            ? Blackboard.Instance.teamAAgents
            : Blackboard.Instance.teamBAgents;

        IFootballAgent closest = null;
        float bestDist = float.MaxValue;
        foreach (var t in mates)
        {
            if (t == (IFootballAgent)this) continue;
            float d = Vector2.Distance(transform.position, t.transform.position);
            if (d < bestDist) { bestDist = d; closest = t; }
        }
        return closest;
    }

    BTStatus TryTackle()
    {
        if (isTackleStunned)
            return BTStatus.Failure;

        if (ball.currentHolder == null)
            return BTStatus.Failure;

        float tackleRange = 1.1f;           // Slightly bigger feel
        float dist = Vector2.Distance(transform.position, ball.currentHolder.transform.position);

        // Only attempt tackle when close enough
        if (dist > tackleRange + 0.3f)
        {
            MoveTowards(ball.currentHolder.transform.position, tackleRange);
            debugState = "Closing in to Tackle";
            return BTStatus.Running;
        }

        // === TACKLE ATTEMPT ===
        if (dist <= tackleRange)
        {
            // Make it harder to tackle successfully
            float finalTackleChance = TackleChance;

            // Reduce chance if attacker is moving fast (dribbling)
            if (ball.currentHolder.rb.velocity.magnitude > 3f)
                finalTackleChance *= 0.65f;

            // Defender bonus if standing still or moving slowly
            if (rb.velocity.magnitude < 2f)
                finalTackleChance *= 1.2f;

            // Cap it realistically
            finalTackleChance = Mathf.Clamp(finalTackleChance, 0.25f, 0.85f);

            if (Random.value < finalTackleChance)
            {
                ball.GiveTo(this);
                postTackleAttackTimer = 1.0f;
                debugState = "Tackle WON";
                return BTStatus.Success;
            }
            else
            {
                tackleCooldownTimer = TackleStun;
                debugState = "Tackle FAILED - Stunned";
                return BTStatus.Failure;
            }
        }

        return BTStatus.Running;
    }
    #endregion
    #region Helper
    void OnTriggerEnter2D(Collider2D col)
    {
        var ballCtrl = col.GetComponent<BallController>();
        if (ballCtrl != null && ballCtrl.currentHolder == null)
            ballCtrl.GiveTo(this);
    }
    int CountOpponentsNearby(float radius) => CountOpponentsNearPosition(transform.position, radius);
    int CountPlayer(PlayerAgent player, float radius)
    {
        var opponents = (player.team == Blackboard.Team.A)
            ? Blackboard.Instance.teamBAgents
            : Blackboard.Instance.teamAAgents;

        int count = 0;
        foreach (var opp in opponents)
        {
            if (opp == (IFootballAgent)player) continue;
            if (Vector2.Distance(player.transform.position, opp.transform.position) <= radius)
                count++;
        }
        return count;
    }
    int CountOpponentsNearPosition(Vector2 pos, float radius)
    {
        var opponents = team == Blackboard.Team.A
            ? Blackboard.Instance.teamBAgents
            : Blackboard.Instance.teamAAgents;
        int count = 0;
        foreach (var opp in opponents)
            if (opp != null && Vector2.Distance(pos, opp.transform.position) <= radius) count++;
        return count;
    }
    public void ApplyRoleStats(TeamTactic currentTactic = null)
    {
        if (roleStats == null)
        {
            Debug.LogWarning($"{name} has no RoleStats assigned for role {role}!");
            return;
        }

        float speedMult = 1f;
        float kickMult = 1f;
        float shootMult = 1f;
        float tackleMult = 1f;
        float pressMult = 1f;

        if (currentTactic != null)
        {
            speedMult = currentTactic.speedMultiplier;
            kickMult = currentTactic.kickPowerMultiplier;
            shootMult = currentTactic.shootDistanceMultiplier;
            tackleMult = currentTactic.tackleChanceMultiplier;
            pressMult = currentTactic.pressDistanceMultiplier;
        }

        // Apply base stats + tactic multipliers
        maxSpeed = roleStats.maxSpeed * speedMult;
        kickPower = roleStats.kickPower * kickMult;
        ShootDistance = roleStats.shootDistance * shootMult;
        TackleChance = roleStats.tackleChance * tackleMult;
        pressDistance = roleStats.pressDistance * pressMult;

        Debug.Log($"{name} ({role}) - Tactic applied | Speed: {maxSpeed:F1} (x{speedMult:F2}), " +
                  $"Kick: {kickPower:F1} (x{kickMult:F2}), ShootDist: {ShootDistance:F1}");
    }

    void OnDrawGizmos()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(transform.position, transform.position + (Vector3)facing * 0.6f);
        UnityEditor.Handles.color = Color.white;
        UnityEditor.Handles.Label(transform.position + Vector3.up * 0.5f, debugState);
    }
    #endregion
}