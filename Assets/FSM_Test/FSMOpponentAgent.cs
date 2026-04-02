using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(Rigidbody2D))]
public class FSMOpponentAgent : MonoBehaviour, IFootballAgent
{
    public enum State
    {
        Idle, ReturnToZone, MarkOpponent, InterceptBall,
        ChaseLooseBall, Defending, Pressing,
        HasBall_Dribble, HasBall_Shoot, HasBall_Pass, Stunned
    }

    [Header("Identity")]
    public Blackboard.Team team = Blackboard.Team.B;
    public Role role = Role.Midfielder;

    [Header("Stats")]
    public RoleStats roleStats;

    [Header("Formation")]
    public Vector2 formationWorldPos;

    [Header("FSM Tuning")]
    [Range(0.1f, 1.5f)] public float ballPredictionTime = 0.45f;
    [Range(2f, 12f)] public float markingRadius = 7f;
    [Range(0.05f, 0.3f)] public float tickInterval = 0.12f;

    [Header("Debug")]
    public State currentState = State.Idle;
    public string debugNote = "";

    public Rigidbody2D rb { get; private set; }
    public Vector2 facing = Vector2.right;

    Blackboard.Team IFootballAgent.team => team;
    Role IFootballAgent.role => role;
    Rigidbody2D IFootballAgent.rb => rb;
    Vector2 IFootballAgent.facing => facing;

    private float maxSpeed, kickPower, shootDistance, tackleChance, pressDistance;
    private float stunTimer, shootCooldown, passCooldown, stateTickTimer, dribbleSinceTimer;

    private BallController ball;
    private IFootballAgent markedOpponent;

    private float wobblePhase;
    private const float WobbleAmt = 0.15f, WobbleFreq = 8f;

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
        wobblePhase = Random.Range(0f, Mathf.PI * 2f);
        currentState = State.ReturnToZone;
    }

    void RegisterToBlackboard()
    {
        var bb = Blackboard.Instance;
        if (bb == null) return;
        if (team == Blackboard.Team.A) bb.teamAAgents.Add(this);
        else bb.teamBAgents.Add(this);
    }

    void Update()
    {
        if (stunTimer > 0f) { stunTimer -= Time.deltaTime; rb.velocity = Vector2.zero; }
        if (shootCooldown > 0f) shootCooldown -= Time.deltaTime;
        if (passCooldown > 0f) passCooldown -= Time.deltaTime;
        if (dribbleSinceTimer > 0f) dribbleSinceTimer -= Time.deltaTime;

        if (stunTimer > 0f) { currentState = State.Stunned; return; }

        stateTickTimer -= Time.deltaTime;
        if (stateTickTimer <= 0f)
        {
            stateTickTimer = tickInterval + Random.Range(-0.02f, 0.02f);
            EvaluateState();
        }

        ExecuteState();

        if (rb.velocity.sqrMagnitude > 0.01f)
            facing = rb.velocity.normalized;
    }

    void EvaluateState()
    {
        bool weHaveBall = ball.currentHolder == (IFootballAgent)this;
        bool ballLoose = ball.currentHolder == null;
        bool teamHasBall = ball.currentHolder != null && ball.currentHolder.team == team;

        if (weHaveBall) { ChooseAttackState(); return; }

        if (ballLoose)
        {
            Vector2 predicted = PredictBallPosition(ballPredictionTime);
            currentState = (IsClosestOnTeamToPredicted(predicted) ||
                            Vector2.Distance(transform.position, ball.transform.position) < 3.5f)
                ? State.InterceptBall
                : State.ReturnToZone;
            return;
        }

        float distToCarrier = Vector2.Distance(transform.position, ball.currentHolder.transform.position);

        if (distToCarrier <= pressDistance * 0.85f) { currentState = State.Pressing; return; }

        switch (role)
        {
            case Role.Goalkeeper:
                currentState = State.ReturnToZone;
                break;
            case Role.Defender:
                markedOpponent = FindMostDangerousOpponent();
                currentState = markedOpponent != null ? State.MarkOpponent : State.Defending;
                break;
            case Role.Midfielder:
                markedOpponent = FindMostDangerousOpponent();
                currentState = IsInMyZone(ball.transform.position, 10f) ? State.Pressing : State.MarkOpponent;
                break;
            case Role.Striker:
                currentState = distToCarrier < 9f ? State.Pressing : State.ReturnToZone;
                break;
        }

        if (teamHasBall) currentState = State.ReturnToZone;
    }

    void ExecuteState()
    {
        switch (currentState)
        {
            case State.Idle: rb.velocity = Vector2.Lerp(rb.velocity, Vector2.zero, Time.deltaTime * 6f); break;
            case State.Stunned: rb.velocity = Vector2.zero; break;
            case State.ReturnToZone: ExecuteReturnToZone(); break;
            case State.InterceptBall: ExecuteInterceptBall(); break;
            case State.ChaseLooseBall: MoveTo(ball.transform.position, 0.4f); debugNote = "ChaseLoose"; break;
            case State.MarkOpponent: ExecuteMarkOpponent(); break;
            case State.Defending: ExecuteDefending(); break;
            case State.Pressing: ExecutePressing(); break;
            case State.HasBall_Dribble: ExecuteDribble(); break;
            case State.HasBall_Shoot: ExecuteShoot(); break;
            case State.HasBall_Pass: ExecutePass(); break;
        }
    }

    void ChooseAttackState()
    {
        float distToGoal = Vector2.Distance(transform.position, OurAttackGoal());
        int oppsClose = CountOpponentsNearPosition(transform.position, 3.5f);

        if (oppsClose >= 3 && passCooldown <= 0f) { currentState = State.HasBall_Pass; return; }
        if (distToGoal < shootDistance && shootCooldown <= 0f && oppsClose <= 2) { currentState = State.HasBall_Shoot; return; }
        if (passCooldown <= 0f && FindBestPassTarget().target != null) { currentState = State.HasBall_Pass; return; }
        currentState = State.HasBall_Dribble;
    }

    void ExecuteReturnToZone()
    {
        if (ball.currentHolder == (IFootballAgent)this) { ChooseAttackState(); return; }

        Vector2 target = formationWorldPos;
        if (ball.currentHolder != null && ball.currentHolder.team == team)
            target += (OurAttackGoal() - formationWorldPos).normalized * Mathf.Min(pressDistance * 0.5f, 5f);

        MoveTo(target);
        debugNote = "ReturnZone";
    }

    void ExecuteInterceptBall()
    {
        MoveTo(PredictBallPosition(ballPredictionTime), 0.5f);
        debugNote = "Intercept";
    }

    void ExecuteMarkOpponent()
    {
        if (markedOpponent == null) { currentState = State.Defending; return; }

        Vector2 oppPos = markedOpponent.transform.position;
        Vector2 markDir = (OurDefendGoal() - oppPos).normalized;
        float markDist = Mathf.Clamp(Vector2.Distance(transform.position, oppPos) * 0.55f, 1.2f, 3f);

        MoveTo(oppPos + markDir * markDist);
        debugNote = $"Mark->{markedOpponent.transform.name}";
    }

    void ExecuteDefending()
    {
        if (ball.currentHolder == null) return;

        Vector2 carrier = ball.currentHolder.transform.position;
        Vector2 laneDir = (OurDefendGoal() - carrier).normalized;
        Vector2 blockSpot = carrier + laneDir * Mathf.Clamp(Vector2.Distance(carrier, OurDefendGoal()) * 0.35f, 2f, 6f);

        MoveTo(blockSpot);
        debugNote = "BlockLane";
    }

    void ExecutePressing()
    {
        if (ball.currentHolder == null || ball.currentHolder.team == team) { currentState = State.ReturnToZone; return; }

        float dist = Vector2.Distance(transform.position, ball.currentHolder.transform.position);
        if (dist <= 1.2f) AttemptTackle();
        else { MoveTo(ball.currentHolder.transform.position, 1.1f); debugNote = "Pressing"; }
    }

    void ExecuteDribble()
    {
        if (ball.currentHolder != (IFootballAgent)this) return;

        Vector2 goal = OurAttackGoal();
        Vector2 dir = (goal - (Vector2)transform.position).normalized;
        Vector2 perp = new Vector2(-dir.y, dir.x);
        Vector2 target = goal + perp * (Mathf.Sin(Time.time * 3.5f) * 1.8f);

        MoveTo(target, 2f);
        dribbleSinceTimer = 0.5f;
        debugNote = "Dribble";
    }

    void ExecuteShoot()
    {
        if (ball.currentHolder != (IFootballAgent)this) return;

        Vector2 aimOffset = Random.value > 0.5f ? new Vector2(0f, 1.1f) : new Vector2(0f, -1.1f);
        ball.KickTowards(OurAttackGoal() + aimOffset, kickPower);
        shootCooldown = 0.6f;
        debugNote = "SHOOT";
        currentState = State.Idle;
    }

    void ExecutePass()
    {
        if (ball.currentHolder != (IFootballAgent)this) return;

        FSMPassResult result = FindBestPassTarget();
        if (result.target == null) { currentState = State.HasBall_Dribble; return; }

        ball.ReleaseWithForce((result.target.transform.position - transform.position).normalized, kickPower * result.powerRatio);
        passCooldown = 0.8f;
        debugNote = $"Pass->{result.target.transform.name}";
        currentState = State.Idle;
    }

    void AttemptTackle()
    {
        if (ball.currentHolder == null || ball.currentHolder.team == team) return;

        float chance = Mathf.Clamp(tackleChance
            * (ball.currentHolder.rb.velocity.magnitude > 3f ? 0.6f : 1f)
            * (rb.velocity.magnitude < 1.5f ? 1.25f : 1f), 0.2f, 0.85f);

        if (Random.value < chance)
        {
            ball.GiveTo(this);
            debugNote = "Tackle WIN";
            ChooseAttackState();
        }
        else
        {
            stunTimer = 1.1f;
            currentState = State.Stunned;
            debugNote = "Tackle FAIL";
        }
    }

    public void OnGainBall(BallController b) { ChooseAttackState(); debugNote = "GainedBall"; }

    void OnTriggerEnter2D(Collider2D col)
    {
        var b = col.GetComponent<BallController>();
        if (b != null && b.currentHolder == null) { b.GiveTo(this); ChooseAttackState(); }
    }

    void MoveTo(Vector2 target, float acceptDist = 0.1f)
    {
        Vector2 dir = target - rb.position;
        if (dir.magnitude < acceptDist) { rb.velocity = Vector2.zero; return; }

        dir.Normalize();
        wobblePhase += Time.deltaTime * WobbleFreq;
        Vector2 wobbled = (dir + new Vector2(-dir.y, dir.x) * (Mathf.Sin(wobblePhase) * WobbleAmt)).normalized;
        rb.velocity = Vector2.Lerp(rb.velocity, wobbled * maxSpeed * (1f + Mathf.Sin(wobblePhase * 0.6f) * 0.06f), Time.deltaTime * 5f);
    }

    Vector2 PredictBallPosition(float t) => (Vector2)ball.transform.position + ball.rb.velocity * t;

    bool IsClosestOnTeamToPredicted(Vector2 pos)
    {
        float myDist = Vector2.Distance(transform.position, pos);
        foreach (var m in GetTeammates())
            if (m != null && Vector2.Distance(m.transform.position, pos) < myDist - 0.3f) return false;
        return true;
    }

    IFootballAgent FindMostDangerousOpponent()
    {
        IFootballAgent best = null;
        float bestDist = float.MaxValue;
        foreach (var opp in GetOpponents())
        {
            if (opp == null) continue;
            float d = Vector2.Distance(opp.transform.position, OurDefendGoal());
            if (d < bestDist && d < markingRadius) { bestDist = d; best = opp; }
        }
        return best;
    }

    struct FSMPassResult { public IFootballAgent target; public float powerRatio; }

    FSMPassResult FindBestPassTarget()
    {
        Vector2 goal = OurAttackGoal();
        IFootballAgent best = null;
        float bestScore = float.MaxValue, bestPower = 1f;

        foreach (var m in GetTeammates())
        {
            if (m == null) continue;
            float distToGoal = Vector2.Distance(m.transform.position, goal);
            float passDist = Vector2.Distance(transform.position, m.transform.position);
            int pressure = CountOpponentsNearPosition(m.transform.position, 4f);
            Vector2 passDir = (m.transform.position - transform.position).normalized;
            Vector2 goalDir = (goal - (Vector2)transform.position).normalized;

            if (Vector2.Dot(passDir, goalDir) < 0.15f && CountOpponentsNearPosition(transform.position, 3f) < 2) continue;

            float score = distToGoal * 2f + passDist * 0.5f + pressure * 3.5f;
            if (score < bestScore)
            {
                bestScore = score;
                best = m;
                bestPower = Mathf.Clamp(passDist / 8f + 0.8f, 0.9f, 1.8f);
            }
        }
        return new FSMPassResult { target = best, powerRatio = bestPower };
    }

    bool IsInMyZone(Vector2 pos, float radius) => Vector2.Distance(formationWorldPos, pos) < radius;

    Vector2 OurAttackGoal() => team == Blackboard.Team.A ? Blackboard.Instance.goalAPosition : Blackboard.Instance.goalBPosition;
    Vector2 OurDefendGoal() => team == Blackboard.Team.A ? Blackboard.Instance.goalBPosition : Blackboard.Instance.goalAPosition;

    List<IFootballAgent> GetOpponents() => team == Blackboard.Team.A ? Blackboard.Instance.teamBAgents : Blackboard.Instance.teamAAgents;
    List<IFootballAgent> GetTeammates() => team == Blackboard.Team.A ? Blackboard.Instance.teamAAgents : Blackboard.Instance.teamBAgents;

    int CountOpponentsNearPosition(Vector2 pos, float radius)
    {
        int count = 0;
        foreach (var opp in GetOpponents())
            if (opp != null && Vector2.Distance(pos, opp.transform.position) <= radius) count++;
        return count;
    }

    public void ApplyRoleStats(TeamTactic tactic = null)
    {
        if (roleStats == null) { Debug.LogWarning($"{name}: no RoleStats!"); return; }
        maxSpeed = roleStats.maxSpeed * (tactic?.speedMultiplier ?? 1f);
        kickPower = roleStats.kickPower * (tactic?.kickPowerMultiplier ?? 1f);
        shootDistance = roleStats.shootDistance * (tactic?.shootDistanceMultiplier ?? 1f);
        tackleChance = roleStats.tackleChance * (tactic?.tackleChanceMultiplier ?? 1f);
        pressDistance = roleStats.pressDistance * (tactic?.pressDistanceMultiplier ?? 1f);
    }

    void OnDrawGizmos()
    {
        UnityEditor.Handles.color = Color.cyan;
        UnityEditor.Handles.Label(transform.position + Vector3.up * 0.5f, $"[FSM] {currentState}\n{debugNote}");
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(transform.position, transform.position + (Vector3)facing * 0.6f);
        if (markedOpponent != null && currentState == State.MarkOpponent)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawLine(transform.position, markedOpponent.transform.position);
        }
        Gizmos.color = new Color(1f, 0.4f, 0f, 0.15f);
        Gizmos.DrawWireSphere(transform.position, pressDistance);
        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(formationWorldPos, 0.3f);
    }
}