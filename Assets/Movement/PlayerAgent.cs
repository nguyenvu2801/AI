using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerAgent : MonoBehaviour
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
        var hasBall = new ConditionNode(() => ball.currentHolder == this);
        var ballNearby = new ConditionNode(() => Vector2.Distance(transform.position, ball.transform.position) < 3f);
        var inShootingRange = new ConditionNode(() =>
        {
            Vector2 goal = (team == Blackboard.Team.A) ? Blackboard.Instance.goalAPosition : Blackboard.Instance.goalBPosition;
            return Vector2.Distance(transform.position, goal) < 12f;
        });
        var ballLoose = new ConditionNode(() => ball.currentHolder == null);
        var opponentHasBall = new ConditionNode(() => ball.currentHolder != null && ball.currentHolder.team != team);
        var teamHasBall = new ConditionNode(() => ball.currentHolder != null && ball.currentHolder.team == team);
        var isClosestToBall = new ConditionNode(() => Blackboard.Instance.GetClosestToBall(team) == this);

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
            if (ball.currentHolder == this) // Got the ball while returning? Abort!
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
        // This sequence will re-run every frame while you have the ball
        new SequenceNode(hasBall, new SelectorNode(
            new SequenceNode(inShootingRange, actionShoot),   // shoot if possible
            new SequenceNode(isClosestToBall, actionPass),  
            actionPass,                                       
            actionDribble                                    // default: keep dribbling forward
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
        Vector2 goalPos = (team == Blackboard.Team.A) ? Blackboard.Instance.goalAPosition : Blackboard.Instance.goalBPosition;
        var teammates = (team == Blackboard.Team.A) ? Blackboard.Instance.teamAAgents : Blackboard.Instance.teamBAgents;
        PlayerAgent best = null;
        float bestScore = Vector2.Distance(transform.position, goalPos) * 1.4f;

        foreach (var t in teammates)
        {
            if (t == this) continue;
            float score = Vector2.Distance(t.transform.position, goalPos) * 1.4f + Vector2.Distance(transform.position, t.transform.position);
            if (score < bestScore) { bestScore = score; best = t; }
        }

        if (best != null)
        {
            ball.ReleaseWithForce((best.transform.position - transform.position).normalized, kickPower * 1.9f);
            debugState = "Pass";
            return BTStatus.Success;
        }
        return BTStatus.Failure;
    }

    BTStatus DoDribble()
    {
        if (emergencyPassCooldown <= 0f)
        {
            int opponentsClose = CountOpponentsNearby(4f);
            int dangerThreshold = 2;

            if (opponentsClose >= dangerThreshold)
            {
                PlayerAgent nearest = GetNearestTeammate();

                // Also avoid passing if teammate is also surrounded 
                if (nearest != null && CountPlayer(nearest, 4f) <= dangerThreshold)
                {
                    // Emergency pass
                    ball.ReleaseWithForce(
                        (nearest.transform.position - transform.position).normalized,
                        kickPower * 1.8f
                    );

                    debugState = "Emergency Pass";

                    // Prevent chain-pass loop
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

        Vector2 finalTarget = goal + dribbleWobbleOffset;
        if (MoveTowards(finalTarget, 2f))
            return BTStatus.Success;

        debugState = "Dribbling - Goal";
        return BTStatus.Running;
    }

    BTStatus TryTackle()
    {
        if (isTackleStunned) return BTStatus.Failure;

        if (ball.currentHolder == null) return BTStatus.Failure;

        float tackleRange = 0.9f;
        float dist = Vector2.Distance(transform.position, ball.transform.position);

        if (dist < tackleRange)
        {
            // actual tackle attempt
            if (Random.value < TackleChance)
            {
                ball.GiveTo(this);
                postTackleAttackTimer = 1.0f;
                debugState = "Tackle WON";
                return BTStatus.Success;
            }
            else
            {
                tackleCooldownTimer = TackleStun;
                debugState = "Tackle FAILED";
                return BTStatus.Failure;
            }
        }
        MoveTowards(ball.currentHolder.transform.position, tackleRange + 0.1f);
        debugState = "Closing in to Tackle";
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
    int CountOpponentsNearby(float radius)
    {
        var opponents = (team == Blackboard.Team.A) ?
            Blackboard.Instance.teamBAgents :
            Blackboard.Instance.teamAAgents;

        int count = 0;
        foreach (var opp in opponents)
        {
            if (opp == this) continue;
            if (Vector2.Distance(transform.position, opp.transform.position) <= radius)
                count++;
        }
        return count;
    }
    int CountPlayer(PlayerAgent player, float radius)
    {
        var opponents = (player.team == Blackboard.Team.A)
            ? Blackboard.Instance.teamBAgents
            : Blackboard.Instance.teamAAgents;

        int count = 0;
        foreach (var opp in opponents)
        {
            if (opp == player) continue;
            if (Vector2.Distance(player.transform.position, opp.transform.position) <= radius)
                count++;
        }
        return count;
    }
    // Find the nearest teammate (for emergency pass)
    PlayerAgent GetNearestTeammate()
    {
        var mates = (team == Blackboard.Team.A) ?
            Blackboard.Instance.teamAAgents :
            Blackboard.Instance.teamBAgents;

        PlayerAgent closest = null;
        float bestDist = float.MaxValue;

        foreach (var t in mates)
        {
            if (t == this) continue;

            float d = Vector2.Distance(transform.position, t.transform.position);
            if (d < bestDist)
            {
                bestDist = d;
                closest = t;
            }
        }
        return closest;
    }
    void ApplyRoleStats()
    {
        if (roleStats != null)
        {
            maxSpeed = roleStats.maxSpeed;
            kickPower = roleStats.kickPower;
            ShootDistance = roleStats.shootDistance;
            TackleChance = roleStats.tackleChance;
            pressDistance = roleStats.pressDistance;
        }
        else
        {
            Debug.LogWarning($"{name} has no RoleStats assigned!");
        }
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