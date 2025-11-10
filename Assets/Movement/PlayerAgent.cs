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
        rb.gravityScale = 0f; // Fix: Disable gravity for top-down
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
        // Conditions
        var hasBall = new ConditionNode(() => ball.currentHolder == this);
        var ballNearby = new ConditionNode(() => Vector2.Distance(transform.position, ball.transform.position) < 3f); // Increased threshold for better reactivity
        var inShootingRange = new ConditionNode(() => {
            Vector2 goal = (team == Blackboard.Team.A) ? Blackboard.Instance.goalAPosition : Blackboard.Instance.goalBPosition;
            return Vector2.Distance(transform.position, goal) < 12f;  // Adjust threshold if field size changes
        });
        var ballLoose = new ConditionNode(() => ball.currentHolder == null);
        var opponentHasBall = new ConditionNode(() => ball.currentHolder != null && ball.currentHolder.team != team);
        var isClosestToBall = new ConditionNode(() => Blackboard.Instance.GetClosestToBall(team) == this); // Use Blackboard helper for coordination

        // Actions
        var actionPressMove = new ActionNode(() => PressMove());
        var actionShoot = new ActionNode(() => { return TryShoot(); });
        var actionPass = new ActionNode(() => { return TryPass(); });
        var actionDribble = new ActionNode(() => { return DoDribble(); });
        var actionTackle = new ActionNode(() => { return TryTackle(); });
        var actionMoveToBall = new ActionNode(() => {
            if (ball.currentHolder == this || MoveTowards(ball.transform.position))
            {
                debugState = "HasBall";
                return BTStatus.Success;
            }
            return BTStatus.Running;
        });
        var actionReturnForm = new ActionNode(() => { 
            if (MoveTowards(formationWorldPos))
            {
                debugState = "Idle";
                return BTStatus.Success;
            }
            return BTStatus.Running; 
        });

        // Expanded BT: Handle possession, then chase loose (near/far, but only if closest), then tackle opponent, else formation
        root = new SelectorNode(
            new SequenceNode(hasBall, new SelectorNode(
                new SequenceNode(inShootingRange, actionShoot),
                actionPass,
                actionDribble
            )),
            new SequenceNode(ballLoose, ballNearby, actionMoveToBall), // Chase near loose ball
            new SequenceNode(ballLoose, isClosestToBall, actionMoveToBall), // Only closest chases far loose ball
           new SequenceNode(opponentHasBall, new SelectorNode(
            new SequenceNode(ballNearby, actionTackle),
            actionPressMove
        )),
        actionReturnForm // Default: formation
        );
    }

    void Update()
    {
        if (root != null) root.Tick();
        // Face the direction of velocity
        if (rb.velocity.magnitude > 0.01f) facing = rb.velocity.normalized;
    }

    // Movement helpers
    bool MoveTowards(Vector2 target,float AcceptDistance = 0.1f)
    {
        Vector2 pos = rb.position;
        Vector2 dir = target - pos;
        float dist = dir.magnitude;

        if (dist < AcceptDistance)
        {
            rb.velocity = Vector2.zero;
            return true;
        }

        dir.Normalize();
        Vector2 desiredVelocity = dir * maxSpeed;
        rb.velocity = Vector2.Lerp(rb.velocity, desiredVelocity, Time.deltaTime * 5f);

        debugState = "Moving";
        return false;
    }

    // Ball-actions
    public void OnGainBall(BallController b)
    {
        debugState = "HasBall";
    }

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
        // Find nearest teammate in front
        var teammates = (team == Blackboard.Team.A) ? Blackboard.Instance.teamAAgents : Blackboard.Instance.teamBAgents;
        PlayerAgent best = null;
        float bestScore = Vector2.Distance(transform.position, goalPos) * 1.4f;
        foreach (var t in teammates)
        {
            if (t == this) continue;
            float score = Vector2.Distance(t.transform.position,goalPos) * 1.4f + Vector2.Distance(transform.position, t.transform.position);
            // Small heuristic: prefer closer teammates
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
            debugState = "Idle";
            return BTStatus.Success;
        };
        
        debugState = "Dribble";
        return BTStatus.Running;
    }

    BTStatus TryTackle()
    {
        // Basic implementation: 50% chance to steal if very close
        if (ball.currentHolder != null && Vector2.Distance(transform.position, ball.transform.position) < 0.8f)
        {
            if (Random.value > 1 - TackleChance)
            {
                ball.GiveTo(this);
                debugState = "TackleSuccess";
                return BTStatus.Success;
            }
        }
        debugState = "TackleAttempt";
        return BTStatus.Failure;
    }
    BTStatus PressMove()
    {
        Vector2 ballPos = ball.transform.position;
        Vector2 dir = (ballPos - formationWorldPos).normalized;
        float distToBall = Vector2.Distance(formationWorldPos, ballPos);
        float pushDist = Mathf.Min(distToBall * 0.7f, pressDistance); // Push 70% towards ball, capped by role stat
        Vector2 pressTarget = formationWorldPos + dir * pushDist;

        if (MoveTowards(pressTarget))
        {
            return BTStatus.Success;
        }
        debugState = "Press";
        return BTStatus.Running;
    }
    // OnCollision: if colliding and not holding ball, maybe gain possession
    void OnTriggerEnter2D(Collider2D col)
    {
        var ballCtrl = col.GetComponent<BallController>();
        if (ballCtrl != null && ballCtrl.currentHolder == null)
        {
            // Pick up ball
            ballCtrl.GiveTo(this);
        }
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
        UnityEditor.Handles.Label(transform.position + Vector3.up * 0.4f, debugState);
    }
}