using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(Rigidbody2D))]
public class FSMOpponentAgent : MonoBehaviour, IFootballAgent
{
    public enum State
    {
        Idle, ReturnToZone, MarkOpponent, InterceptBall,
        ChaseLooseBall, Defending, Pressing,
        HasBall_AttackDecision,
        Stunned,
        SupportRun,
        DeepDefend
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
    private Vector2 dribbleWobbleOffset = Vector2.zero;

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
        if (stunTimer > 0f)
        {
            stunTimer -= Time.deltaTime;
            rb.velocity = Vector2.zero;
            currentState = State.Stunned;
            return;
        }

        if (shootCooldown > 0f) shootCooldown -= Time.deltaTime;
        if (passCooldown > 0f) passCooldown -= Time.deltaTime;
        if (dribbleSinceTimer > 0f) dribbleSinceTimer -= Time.deltaTime;

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

        if (weHaveBall)
        {
            currentState = State.HasBall_AttackDecision;
            return;
        }

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

        if (distToCarrier <= pressDistance * 0.85f && role != Role.Striker)
        {
            currentState = State.Pressing;
            return;
        }

        switch (role)
        {
            case Role.Goalkeeper:
                currentState = State.ReturnToZone;
                break;

            case Role.Defender:
                markedOpponent = FindMostDangerousOpponent();

                Vector2 ourGoal = OurDefendGoal();
                float distToOwnGoal = Vector2.Distance(transform.position, ourGoal);
                const float deepDefendThreshold = 6f;

                if (distToOwnGoal < deepDefendThreshold && ball.currentHolder != null && ball.currentHolder.team != team)
                {
                    float distToBallCarrier = Vector2.Distance(transform.position, ball.currentHolder.transform.position);
                    if (distToBallCarrier < pressDistance * 1.4f)
                    {
                        currentState = State.DeepDefend;
                        return;
                    }
                }

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

        if (teamHasBall)
        {
            currentState = ShouldSupportRun() ? State.SupportRun : State.ReturnToZone;
            return;
        }
    }

    void ExecuteState()
    {
        switch (currentState)
        {
            case State.Idle: rb.velocity = Vector2.Lerp(rb.velocity, Vector2.zero, Time.deltaTime * 6f); break;
            case State.Stunned: rb.velocity = Vector2.zero; break;
            case State.ReturnToZone: ExecuteReturnToZone(); break;
            case State.InterceptBall: ExecuteInterceptBall(); break;
            case State.MarkOpponent: ExecuteMarkOpponent(); break;
            case State.SupportRun: ExecuteSupportRun(); break;
            case State.Defending: ExecuteDefending(); break;
            case State.Pressing: ExecutePressing(); break;
            case State.HasBall_AttackDecision: ExecuteAttackWithUtility(); break;
            case State.DeepDefend: ExecuteDeepDefend(); break;
        }
    }

    void ExecuteDeepDefend()
    {
        if (ball.currentHolder == null || ball.currentHolder.team == team)
        {
            currentState = State.Defending;
            return;
        }

        float dist = Vector2.Distance(transform.position, ball.currentHolder.transform.position);

        if (dist <= 1.4f)
        {
            AttemptTackle();
            debugNote = "Deep Tackle Attempt";
        }
        else
        {
            Vector2 target = ball.currentHolder.transform.position;

            if (dist > 6f)
                target = GetGoalSideInterceptPoint(ball.currentHolder.transform.position, 1.2f);

            MoveTo(target, 1.8f);
            debugNote = "Deep Defend - Chase & Tackle";
        }
    }

    void ExecuteAttackWithUtility()
    {
        if (ball.currentHolder != (IFootballAgent)this) return;
        float distToGoal = Vector2.Distance(transform.position, OurAttackGoal());
        if (distToGoal < 6f)
        {
            ExecuteShoot();
            debugNote = "BLANK GOAL SHOT";
            return;
        }
        float uShoot = CalculateShootUtility();
        float uPass = CalculatePassUtility();
        float uDribble = CalculateDribbleUtility();

        debugNote = $"UTIL: S={uShoot:F2} P={uPass:F2} D={uDribble:F2}";

        if (uShoot > uPass && uShoot > uDribble && uShoot > 0.28f)
        {
            ExecuteShoot();
        }
        else if (uPass > uShoot && uPass > uDribble && uPass > 0.32f)
        {
            if (!ExecutePass())
                ExecuteDribble();
        }
        else
        {
            ExecuteDribble();
        }
    }

    float CalculateShootUtility()
    {
        Vector2 goal = OurAttackGoal();
        float dist = Vector2.Distance(transform.position, goal);
      
      

        if (dist > shootDistance * 1.15f || shootCooldown > 0f) return 0f;
        if (!HasClearLineOfSight(transform.position, goal, 1.9f)) return 0f;

        int nearbyOpp = CountOpponentsNearPosition(transform.position, 6f);
        float pressure = Mathf.Clamp01((6f - nearbyOpp) / 6f);
        float distScore = Mathf.Clamp01(1f - (dist / (shootDistance * 1.25f)));
        float facingScore = Mathf.Max(0f, Vector2.Dot(facing, (goal - (Vector2)transform.position).normalized));

        float utility = distScore * 0.5f + pressure * 0.3f + facingScore * 0.3f;
        if (dist < 7f) utility += 0.4f;

        return Mathf.Clamp01(utility);
    }

    float CalculatePassUtility()
    {
        if (passCooldown > 0f) return 0f;

        Vector2 goal = OurAttackGoal();
        var teammates = GetTeammates();
        IFootballAgent best = null;
        float bestScore = float.MaxValue;

        foreach (var mate in teammates)
        {
            if (mate == null || mate == (IFootballAgent)this) continue;
            if (!HasClearLineOfSight(transform.position, mate.transform.position, 1.4f)) continue;

            float distToGoal = Vector2.Distance(mate.transform.position, goal);
            float passDist = Vector2.Distance(transform.position, mate.transform.position);
            int pressureOnMate = CountOpponentsNearPosition(mate.transform.position, 5f);

            float score = distToGoal * 2.4f + passDist * 0.55f + pressureOnMate * 4f;

            if (score < bestScore)
            {
                bestScore = score;
                best = mate;
            }
        }

        if (best == null) return 0f;

        float myDistToGoal = Vector2.Distance(transform.position, goal);
        float mateDistToGoal = Vector2.Distance(best.transform.position, goal);

        if (myDistToGoal < 4f || myDistToGoal < mateDistToGoal - 5f) return 0f;

        Vector2 passDir = (best.transform.position - transform.position).normalized;
        Vector2 goalDir = (goal - (Vector2)transform.position).normalized;

        if (Vector2.Dot(passDir, goalDir) < 0.35f) return 0f;
        if (CountOpponentsNearPosition(best.transform.position, 3.5f) >= 3) return 0f;

        float utility = 0.6f + Mathf.Max(0f, (myDistToGoal - mateDistToGoal) / 15f) * 0.35f;
        return Mathf.Clamp01(utility);
    }

    float CalculateDribbleUtility()
    {
        int nearbyOpp = CountOpponentsNearPosition(transform.position, 5f);
        float pressure = nearbyOpp / 5.5f;
        Vector2 goalDir = (OurAttackGoal() - (Vector2)transform.position).normalized;
        float clearance = GetDirectionClearance(goalDir, 12f);
        float spaceScore = clearance / 12f;

        return Mathf.Clamp01((1f - pressure) * 0.7f + spaceScore * 0.45f);
    }

    void ExecuteDribble()
    {
        if (ball.currentHolder != (IFootballAgent)this) return;

        Vector2 bestTarget = GetBestDribbleTarget(11f);
        MoveTo(bestTarget, 2.2f);
        dribbleSinceTimer = 0.5f;
        debugNote = "Smart Dribble";
    }
    void ExecuteSupportRun()
    {

        Vector2 ballPos = ball.transform.position;
        Vector2 goalDir = (OurAttackGoal() - ballPos).normalized;
        Vector2 lateral = new Vector2(-goalDir.y, goalDir.x);

        float side = (GetInstanceID() % 2 == 0) ? 1f : -1f;

        // Strikers run much further and more aggressively
        float forwardDistance = (role == Role.Striker) ? 11.5f : 6f;
        float lateralDistance = (role == Role.Striker) ? 5.5f : 4f;

        Vector2 supportSpot = ballPos + goalDir * forwardDistance + lateral * side * lateralDistance;

        // Almost no lerp for strikers (they should commit to the run)
        float lerpBack = (role == Role.Striker) ? 0.12f : 0.35f;
        supportSpot = Vector2.Lerp(supportSpot, formationWorldPos, lerpBack);

        MoveTo(supportSpot, 0.8f);
        debugNote = "SupportRun";
    }
    void ExecuteShoot()
    {
        if (ball.currentHolder != (IFootballAgent)this) return;

        if (!HasClearLineOfSight(transform.position, OurAttackGoal(), 1.9f))
        {
            debugNote = "Shoot Blocked";
            currentState = State.HasBall_AttackDecision;
            return;
        }

        Vector2 aimOffset = Random.value > 0.5f ? new Vector2(0f, 1.2f) : new Vector2(0f, -1.2f);
        ball.KickTowards(OurAttackGoal() + aimOffset, kickPower);
        shootCooldown = 0.65f;
        debugNote = "SHOOT";
    }

    bool ExecutePass()
    {
        if (ball.currentHolder != (IFootballAgent)this) return false;

        var result = FindBestPassTarget();
        if (result.target == null) return false;
        if (!HasClearLineOfSight(transform.position, result.target.transform.position, 1.4f))
            return false;

        ball.ReleaseWithForce((result.target.transform.position - transform.position).normalized,
                              kickPower * result.powerRatio);
        passCooldown = 0.85f;
        debugNote = $"Pass->{result.target.transform.name}";
        return true;
    }
    bool ShouldSupportRun()
    {

        if (role == Role.Goalkeeper || role == Role.Defender) return false;

        float distToGoal = Vector2.Distance(transform.position, OurAttackGoal());
        if (distToGoal < 5f) return false;

        // Strikers ignore pressure or use much higher threshold
        if (role == Role.Striker)
        {
            int pressureI = CountOpponentsNearPosition(transform.position, 4f);
            return pressureI < 3;           
        }

        // Midfielders keep original logic
        int pressure = CountOpponentsNearPosition(transform.position, 4f);
        return pressure < 2;
    }
    void ExecuteReturnToZone()
    {
        if (ball.currentHolder == (IFootballAgent)this) return;

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
        if (markedOpponent == null)
        {
            currentState = State.Defending;
            return;
        }

        Vector2 markTarget = GetGoalSideMarkPosition(markedOpponent.transform.position);
        MoveTo(markTarget, 2.6f);
        debugNote = "Smart Mark (goal-side)";
    }

    void ExecuteDefending()
    {
        if (ball.currentHolder == null) return;

        Vector2 interceptPoint = GetGoalSideInterceptPoint(ball.currentHolder.transform.position, 3.5f);
        MoveTo(interceptPoint);
        debugNote = "Smart Block";
    }

    void ExecutePressing()
    {
        if (ball.currentHolder == null || ball.currentHolder.team == team)
        {
            currentState = State.ReturnToZone;
            return;
        }

        float dist = Vector2.Distance(transform.position, ball.currentHolder.transform.position);

        if (dist <= 1.25f)
            AttemptTackle();
        else
        {
            Vector2 interceptPoint = GetGoalSideInterceptPoint(ball.currentHolder.transform.position);
            MoveTo(interceptPoint, 1.15f);
            debugNote = "Smart Press";
        }
    }

    void AttemptTackle()
    {
        if (ball.currentHolder == null || ball.currentHolder.team == team) return;

        float chance = Mathf.Clamp(tackleChance
            * (ball.currentHolder.rb.velocity.magnitude > 3f ? 0.62f : 1f)
            * (rb.velocity.magnitude < 1.8f ? 1.22f : 1f), 0.22f, 0.87f);

        if (Random.value < chance)
        {
            ball.GiveTo(this);
            debugNote = "Tackle WIN";
            currentState = State.HasBall_AttackDecision;
        }
        else
        {
            stunTimer = 3f;
            currentState = State.Stunned;
            debugNote = "Tackle FAIL";
        }
    }

    Vector2 GetGoalSideMarkPosition(Vector2 opponentPos, float offset = 2.8f)
    {
        Vector2 ourGoal = OurDefendGoal();
        Vector2 dirToGoal = (ourGoal - opponentPos).normalized;
        return opponentPos + dirToGoal * offset;
    }

    Vector2 GetGoalSideInterceptPoint(Vector2 carrierPos, float offset = 1.9f)
    {
        Vector2 ourGoal = OurDefendGoal();
        Vector2 dirToGoal = (ourGoal - carrierPos).normalized;
        return carrierPos + dirToGoal * offset;
    }

    private bool HasClearLineOfSight(Vector2 start, Vector2 end, float clearanceRadius = 1.6f)
    {
        Vector2 dir = end - start;
        float dist = dir.magnitude;
        if (dist < 0.6f) return true;
        dir.Normalize();

        var opponents = GetOpponents();
        foreach (var opp in opponents)
        {
            if (opp == null) continue;
            Vector2 toOpp = (Vector2)opp.transform.position - start;
            float oppDist = toOpp.magnitude;
            if (oppDist > dist + 3f || oppDist < 0.5f) continue;

            float proj = Vector2.Dot(toOpp, dir);
            if (proj < 0f || proj > dist) continue;

            float perp = (toOpp - dir * proj).magnitude;
            if (perp <= clearanceRadius) return false;
        }
        return true;
    }

    private Vector2 Rotate(Vector2 v, float angleDeg)
    {
        float rad = angleDeg * Mathf.Deg2Rad;
        float c = Mathf.Cos(rad), s = Mathf.Sin(rad);
        return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
    }

    private float GetDirectionClearance(Vector2 dir, float maxLook = 12f, float sideClearance = 2.3f)
    {
        float clearance = maxLook;
        var opponents = GetOpponents();
        Vector2 pos = transform.position;

        foreach (var opp in opponents)
        {
            if (opp == null) continue;
            Vector2 toOpp = (Vector2)opp.transform.position - pos;
            float distToOpp = toOpp.magnitude;
            if (distToOpp > maxLook + 4f) continue;

            float proj = Vector2.Dot(toOpp, dir);
            if (proj < 0f) continue;

            float perpDist = (toOpp - dir * proj).magnitude;
            if (perpDist < sideClearance)
                clearance = Mathf.Min(clearance, proj);
        }
        return clearance;
    }

    private Vector2 GetBestDribbleTarget(float lookAhead = 11f)
    {
        Vector2 goalDir = (OurAttackGoal() - (Vector2)transform.position).normalized;
        float bestScore = -Mathf.Infinity;
        Vector2 bestOffset = goalDir * lookAhead;

        for (int i = -5; i <= 5; i++)
        {
            float angle = i * 10f;
            Vector2 testDir = Rotate(goalDir, angle);
            float clearance = GetDirectionClearance(testDir, lookAhead + 4f);
            float alignment = Vector2.Dot(testDir, goalDir);
            float score = alignment * 1.15f + (clearance / lookAhead) * 0.95f;

            if (score > bestScore)
            {
                bestScore = score;
                bestOffset = testDir * lookAhead;
            }
        }

        if (Time.time % 0.42f < Time.deltaTime)
            dribbleWobbleOffset = Random.insideUnitCircle * 1.7f;

        return (Vector2)transform.position + bestOffset + dribbleWobbleOffset;
    }

    public void OnGainBall(BallController b)
    {
        currentState = State.HasBall_AttackDecision;
        debugNote = "GainedBall";
    }

    void MoveTo(Vector2 target, float acceptDist = 0.1f)
    {
        Vector2 dir = target - rb.position;
        if (dir.magnitude < acceptDist)
        {
            rb.velocity = Vector2.zero;
            return;
        }

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
            if (m != null && Vector2.Distance(m.transform.position, pos) < myDist - 0.35f) return false;
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
            if (d < bestDist && d < markingRadius)
            {
                bestDist = d;
                best = opp;
            }
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
            if (m == null || m == (IFootballAgent)this) continue;
            if (!HasClearLineOfSight(transform.position, m.transform.position, 1.4f)) continue;

            float distToGoal = Vector2.Distance(m.transform.position, goal);
            float passDist = Vector2.Distance(transform.position, m.transform.position);
            int pressure = CountOpponentsNearPosition(m.transform.position, 4f);

            Vector2 passDir = (m.transform.position - transform.position).normalized;
            Vector2 goalDir = (goal - (Vector2)transform.position).normalized;

            if (Vector2.Dot(passDir, goalDir) < 0.2f) continue;

            float score = distToGoal * 2.1f + passDist * 0.55f + pressure * 3.8f;

            if (score < bestScore)
            {
                bestScore = score;
                best = m;
                bestPower = Mathf.Clamp(passDist / 8.5f + 0.85f, 0.9f, 1.85f);
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
        if (roleStats == null)
        {
            Debug.LogWarning($"{name}: no RoleStats!");
            return;
        }

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