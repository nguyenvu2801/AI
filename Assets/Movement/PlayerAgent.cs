using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerAgent : MonoBehaviour
{
    public Blackboard.Team team = Blackboard.Team.A;
    public Role role = Role.Midfielder;
    public Rigidbody2D rb;
    private float maxSpeed;
    private float kickPower;
    private float ShootDistance;
    private float TackleChance;
    private float postTackleAttackTimer = 0f;
    private float tackleCooldownTimer = 0f;      // Counts down when stunned
    private const float TackleStun = 1.2f;  // Adjust: longer = more punishment
    private bool isTackleStunned => tackleCooldownTimer > 0f;
    [Header("Stats (from SO)")]
    public RoleStats roleStats;
    [Header("Formation")]
    public Vector2 formationWorldPos; // World position of formation slot
    private float pressDistance; // relative to team center
    [HideInInspector] public Vector2 facing = Vector2.right;

    // BT
    private BTNode root;
    private BallController ball;
    public string debugState = "Idle";

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

    void BuildBehaviorTree()
    {
        // ====== CONDITIONS ======
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

        // ====== ACTIONS ======
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
            if (MoveTowards(formationWorldPos))
                return BTStatus.Success;
            debugState = "Return to Formation";
            return BTStatus.Running;
        });

        // ROOT TREE - NO InverterNode used
        root = new SelectorNode(
            // 1. I have ball  attack
            new SequenceNode(hasBall, new SelectorNode(
                new SequenceNode(inShootingRange, actionShoot),
                actionPass,
                actionDribble
            )),
            new SequenceNode(recentlyWonTackle, new SelectorNode(actionPass, actionDribble, actionShoot)),
            // 2. Loose ball nearby
            new SequenceNode(ballLoose, ballNearby, actionChaseBall),

            // 3. Loose ball far but closest
            new SequenceNode(ballLoose, isClosestToBall, actionChaseBall),

            // 4. Teammate has ball  support
            new SequenceNode(teamHasBall, actionSupport),

            // 5. OPPONENT HAS BALL - Defensive logic
            new SequenceNode(opponentHasBall, new SelectorNode(
                // Anyone close enough  tackle first (highest priority)
                new SequenceNode(opponentInPressRange, actionTackle),

                // Defender: press anytime the ball is in our defensive zone
                new SequenceNode(isDefender, ballInOurDefensiveZone, actionPress),

                // Non-defender: only press if opponent is inside my personal press range
                new SequenceNode(isNotDefender, opponentInPressRange, actionPress),

                // Default: stay in formation
                actionReturnForm
            )),
            // Final fallback
            actionReturnForm
        );
    }
    void Update()
    {
        if (root != null) root.Tick();
        if (postTackleAttackTimer > 0)
            postTackleAttackTimer -= Time.deltaTime;
        if (rb.velocity.magnitude > 0.01f)
            facing = rb.velocity.normalized;
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

    // ==================== MOVEMENT & ACTIONS ====================

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
        rb.velocity = Vector2.Lerp(rb.velocity, dir * maxSpeed, Time.deltaTime * 5f);
        return false;
    }

    BTStatus PressBallCarrier()
    {
        if (ball.currentHolder == null) return BTStatus.Failure;

        Vector2 target = ball.currentHolder.transform.position;
        float jockeyDistance = 1.4f;

        if (Vector2.Distance(transform.position, target) < jockeyDistance + 0.3f)
        {
            rb.velocity = Vector2.Lerp(rb.velocity, Vector2.zero, Time.deltaTime * 12f);
            debugState = "Jockeying";
            return BTStatus.Success;
        }

        MoveTowards(target, jockeyDistance);
        debugState = "Pressing!";
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
        Vector2 goal = (team == Blackboard.Team.A) ? Blackboard.Instance.goalAPosition : Blackboard.Instance.goalBPosition;
        if (MoveTowards(goal, ShootDistance))
        {
            debugState = "Dribble Done";
            return BTStatus.Success;
        }
        debugState = "Dribbling";
        return BTStatus.Running;
    }

    BTStatus TryTackle()
    {
        // If still recovering from last failed tackle can't try again
        if (isTackleStunned)
        {
            debugState = "Stunned!";
            return BTStatus.Failure;
        }

        if (ball.currentHolder == null) return BTStatus.Failure;

        float tackleRange = 0.9f;
        if (Vector2.Distance(transform.position, ball.transform.position) < tackleRange)
        {
            if (Random.value < TackleChance) // Success
            {
                ball.GiveTo(this);
                postTackleAttackTimer = 1.0f;
                debugState = "Tackle WON";
                return BTStatus.Success;
            }
            else // FAILED enter stun
            {
                tackleCooldownTimer =TackleStun;
                debugState = "Tackle FAILED Stunned";

                // Optional small push back to show commitment
                Vector2 awayFromBall = (transform.position - ball.transform.position).normalized;
                rb.velocity += awayFromBall * 2f;

                return BTStatus.Failure;
            }
        }

        debugState = "Moving to Tackle";
        return BTStatus.Failure;
    }

    void OnTriggerEnter2D(Collider2D col)
    {
        var ballCtrl = col.GetComponent<BallController>();
        if (ballCtrl != null && ballCtrl.currentHolder == null)
            ballCtrl.GiveTo(this);
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
}