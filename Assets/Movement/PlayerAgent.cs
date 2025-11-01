using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerAgent : MonoBehaviour
{
    public Blackboard.Team team = Blackboard.Team.A;
    public Role role = Role.Midfielder;
    public Rigidbody2D rb;
    public float maxSpeed = 4f;
    public float kickPower = 6f;
    public Vector2 formationPosition; // relative to team center
    [HideInInspector] public Vector2 facing = Vector2.right;

    // BT
    private BTNode root;
    private BallController ball;
    [HideInInspector] public string debugState = "Idle";

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.gravityScale = 0f; // Fix: Disable gravity for top-down
    }

    void Start()
    {
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
        var actionShoot = new ActionNode(() => { return TryShoot(); });
        var actionPass = new ActionNode(() => { return TryPass(); });
        var actionDribble = new ActionNode(() => { return DoDribble(); });
        var actionTackle = new ActionNode(() => { return TryTackle(); });
        var actionMoveToBall = new ActionNode(() => { MoveTowards(ball.transform.position); return BTStatus.Running; });
        var actionReturnForm = new ActionNode(() => { MoveTowards((Vector2)transform.parent.position + formationPosition); return BTStatus.Running; });

        // Expanded BT: Handle possession, then chase loose (near/far, but only if closest), then tackle opponent, else formation
        root = new SelectorNode(
            new SequenceNode(hasBall, new SelectorNode(
                new SequenceNode(inShootingRange, actionShoot),
                actionPass,
                actionDribble
            )),
            new SequenceNode(ballLoose, ballNearby, actionMoveToBall), // Chase near loose ball
            new SequenceNode(ballLoose, isClosestToBall, actionMoveToBall), // Only closest chases far loose ball
            new SequenceNode(opponentHasBall, ballNearby, actionTackle), // Tackle if opponent has and nearby
            actionReturnForm
        );
    }

    void Update()
    {
        if (root != null) root.Tick();
        // Face the direction of velocity
        if (rb.velocity.magnitude > 0.01f) facing = rb.velocity.normalized;
    }

    // Movement helpers
    void MoveTowards(Vector2 target)
    {
        Vector2 force = Steering.Seek(rb.position, target, rb, maxSpeed, 0.6f);
        rb.AddForce(force, ForceMode2D.Force);
        // Clamp speed
        if (rb.velocity.magnitude > maxSpeed) rb.velocity = rb.velocity.normalized * maxSpeed;
        debugState = "Moving";
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
        // Find nearest teammate in front
        var teammates = (team == Blackboard.Team.A) ? Blackboard.Instance.teamAAgents : Blackboard.Instance.teamBAgents;
        PlayerAgent best = null;
        float bestScore = float.NegativeInfinity;
        foreach (var t in teammates)
        {
            if (t == this) continue;
            float score = Vector2.Dot((t.transform.position - transform.position).normalized, (Quaternion.Euler(0, 0, 0) * Vector2.right)) - Vector2.Distance(transform.position, t.transform.position);
            // Small heuristic: prefer closer teammates
            if (score > bestScore) { bestScore = score; best = t; }
        }
        if (best != null)
        {
            ball.ReleaseWithForce((best.transform.position - transform.position).normalized, kickPower * 0.9f);
            debugState = "Pass";
            return BTStatus.Success;
        }
        return BTStatus.Failure;
    }

    BTStatus DoDribble()
    {
        Vector2 goal = (team == Blackboard.Team.A) ? Blackboard.Instance.goalAPosition : Blackboard.Instance.goalBPosition;
        MoveTowards(goal);
        debugState = "Dribble";
        return BTStatus.Running;
    }

    BTStatus TryTackle()
    {
        // Basic implementation: 50% chance to steal if very close
        if (ball.currentHolder != null && Vector2.Distance(transform.position, ball.transform.position) < 0.8f)
        {
            if (Random.value > 0.5f)
            {
                ball.GiveTo(this);
                debugState = "TackleSuccess";
                return BTStatus.Success;
            }
        }
        debugState = "TackleAttempt";
        return BTStatus.Failure;
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

    void OnDrawGizmos()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(transform.position, transform.position + (Vector3)facing * 0.6f);
        UnityEditor.Handles.Label(transform.position + Vector3.up * 0.4f, debugState);
    }
}