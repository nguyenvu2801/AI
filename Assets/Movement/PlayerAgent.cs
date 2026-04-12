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

    private const float TackleStun = 3f;

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
            Vector2 goal = GetOpponentGoalPosition();
            return Vector2.Distance(transform.position, goal) < ShootDistance;
        });

        var ballLoose = new ConditionNode(() => ball.currentHolder == null);
        var opponentHasBall = new ConditionNode(() => ball.currentHolder != null && ball.currentHolder.team != team);
        var teamHasBall = new ConditionNode(() => ball.currentHolder != null && ball.currentHolder.team == team);
        var isClosestToBall = new ConditionNode(() => Blackboard.Instance.GetClosestToBall(team) == (IFootballAgent)this);

        var ballInOurDefensiveZone = new ConditionNode(() =>
        {
            Vector2 ourGoal = GetOurGoalPosition();
            return Vector2.Distance(ball.transform.position, ourGoal) < 15f;
        });

        var isDefender = new ConditionNode(() => role == Role.Defender);
        var isNotDefender = new ConditionNode(() => role != Role.Defender);
        var recentlyWonTackle = new ConditionNode(() => postTackleAttackTimer > 0f);

        var opponentInPressRange = new ConditionNode(() =>
        {
            if (ball.currentHolder == null || ball.currentHolder.team == team) return false;
            return Vector2.Distance(transform.position, ball.currentHolder.transform.position) <= pressDistance;
        });

        var defenderShouldEngage = new ConditionNode(() =>
        {
            if (ball.currentHolder == null || ball.currentHolder.team == team) return false;
            if (role != Role.Defender) return false;

            Vector2 ourGoal = GetOurGoalPosition();
            float holderToGoal = Vector2.Distance(ball.currentHolder.transform.position, ourGoal);
            float selfToHolder = Vector2.Distance(transform.position, ball.currentHolder.transform.position);

            return holderToGoal <= 14f || selfToHolder <= pressDistance * 2.2f;
        });
        #endregion

        #region Action
        var actionSupport = new ActionNode(SupportMove);
        var actionShoot = new ActionNode(TryShoot);
        var actionPass = new ActionNode(TryPass);
        var actionDribble = new ActionNode(DoDribble);
        var actionTackle = new ActionNode(TryTackle);
        var actionPress = new ActionNode(PressBallCarrier);
        var actionAttackWithUtility = new ActionNode(AttackWithUtility);
        var actionMark = new ActionNode(MarkOpponent);

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
        root = new SelectorNode(
            new SequenceNode(hasBall, actionAttackWithUtility),

            new SequenceNode(recentlyWonTackle, new SelectorNode(
                actionPass,
                actionDribble,
                actionShoot
            )),

            new SequenceNode(teamHasBall, actionSupport),

            new SequenceNode(ballLoose, ballNearby, actionChaseBall),
            new SequenceNode(ballLoose, isClosestToBall, actionChaseBall),

            new SequenceNode(opponentHasBall, new SelectorNode(
                new SequenceNode(isDefender, defenderShouldEngage, actionTackle),
                new SequenceNode(opponentInPressRange, actionTackle),
                new SequenceNode(isDefender, ballInOurDefensiveZone, actionPress),
                new SequenceNode(isNotDefender, opponentInPressRange, actionPress),
                new SequenceNode(isDefender, actionMark),
                actionReturnForm
            )),

            actionReturnForm
        );
        #endregion
    }
    #endregion

    void Update()
    {
        if (root != null) root.Tick();

        if (postTackleAttackTimer > 0f)
            postTackleAttackTimer -= Time.deltaTime;

        if (rb.velocity.magnitude > 0.01f)
            facing = rb.velocity.normalized;

        if (emergencyPassCooldown > 0f)
            emergencyPassCooldown -= Time.deltaTime;

        if (tackleCooldownTimer > 0f)
        {
            tackleCooldownTimer -= Time.deltaTime;
            if (tackleCooldownTimer > 0f)
                rb.velocity = Vector2.zero;
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
        Vector2 oppGoal = GetOpponentGoalPosition();
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

    BTStatus AttackWithUtility()
    {
        if (ball.currentHolder != (IFootballAgent)this) return BTStatus.Failure;

        float uShoot = CalculateShootUtility();
        float uPass = CalculatePassUtility();
        float uDribble = CalculateDribbleUtility();

        debugState = $"UTIL: Shoot={uShoot:F2} | Pass={uPass:F2} | Dribble={uDribble:F2}";

        if (uShoot > uPass && uShoot > uDribble && uShoot > 0.25f)
        {
            return TryShoot();
        }
        else if (uPass > uShoot && uPass > uDribble && uPass > 0.3f)
        {
            BTStatus passResult = TryPass();
            return passResult == BTStatus.Failure ? DoDribble() : passResult;
        }
        else
        {
            return DoDribble();
        }
    }

    float CalculateShootUtility()
    {
        Vector2 goal = GetOpponentGoalPosition();
        float dist = Vector2.Distance(transform.position, goal);

        if (dist > ShootDistance * 1.1f) return 0f;
        if (!HasClearLineOfSight(transform.position, goal, 1.8f)) return 0f;

        int nearbyOpp = CountOpponentsNearby(6f);
        float pressureScore = Mathf.Clamp01((5f - nearbyOpp) / 5f);
        Vector2 toGoal = (goal - (Vector2)transform.position).normalized;
        float facingScore = Mathf.Max(0f, Vector2.Dot(facing, toGoal));
        float distScore = Mathf.Clamp01(1f - (dist / (ShootDistance * 1.2f)));

        float utility = distScore * 0.45f + pressureScore * 0.35f + facingScore * 0.3f;
        if (dist < 6f) utility += 0.35f;

        return Mathf.Clamp01(utility);
    }

    float CalculatePassUtility()
    {
        Vector2 goalPos = GetOpponentGoalPosition();
        var teammates = team == Blackboard.Team.A ? Blackboard.Instance.teamAAgents : Blackboard.Instance.teamBAgents;

        IFootballAgent bestTeammate = null;
        float bestScore = float.MaxValue;

        foreach (var teammate in teammates)
        {
            if (teammate == (IFootballAgent)this || teammate == null) continue;
            if (!HasClearLineOfSight(transform.position, teammate.transform.position, 1.3f)) continue;

            float distToGoalTeammate = Vector2.Distance(teammate.transform.position, goalPos);
            float passDistance = Vector2.Distance(transform.position, teammate.transform.position);
            int oppNearReceiver = CountOpponentsNearPosition(teammate.transform.position, 5f);
            float score = distToGoalTeammate * 2.5f + passDistance * 0.6f + oppNearReceiver * 4f;

            if (score < bestScore)
            {
                bestScore = score;
                bestTeammate = teammate;
            }
        }

        if (bestTeammate == null) return 0f;

        float distToGoalMe = Vector2.Distance(transform.position, goalPos);
        float distToGoalBest = Vector2.Distance(bestTeammate.transform.position, goalPos);

        if (distToGoalMe < 4f) return 0f;
        if (distToGoalMe < distToGoalBest - 4f) return 0f;

        Vector2 passDirection = (bestTeammate.transform.position - transform.position).normalized;
        Vector2 goalDirection = (goalPos - (Vector2)transform.position).normalized;

        if (Vector2.Dot(passDirection, goalDirection) < 0.35f) return 0f;
        if (CountOpponentsNearPosition(bestTeammate.transform.position, 3.5f) >= 3) return 0f;

        float receiverAdvantage = Mathf.Max(0f, (distToGoalMe - distToGoalBest) / 12f);
        float receiverSafety = 1f - (CountOpponentsNearPosition(bestTeammate.transform.position, 5f) / 4f);
        float utility = 0.55f + receiverAdvantage * 0.3f + receiverSafety * 0.25f;

        return Mathf.Clamp01(utility);
    }

    float CalculateDribbleUtility()
    {
        int nearbyOpp = CountOpponentsNearby(5f);
        float pressure = nearbyOpp / 5f;

        Vector2 goalDir = (GetOpponentGoalPosition() - (Vector2)transform.position).normalized;
        float clearance = GetDirectionClearance(goalDir, 11f);
        float spaceScore = Mathf.Clamp01(clearance / 11f);

        float utility = (1f - pressure) * 0.65f + spaceScore * 0.45f;
        return Mathf.Clamp01(utility);
    }

    BTStatus TryShoot()
    {
        Vector2 goal = GetOpponentGoalPosition();

        if (!HasClearLineOfSight(transform.position, goal, 1.8f))
        {
            debugState = "Shoot Blocked";
            return BTStatus.Failure;
        }

        ball.KickTowards(goal, kickPower);
        debugState = "Shoot";
        return BTStatus.Success;
    }

    BTStatus TryPass()
    {
        Vector2 goalPos = GetOpponentGoalPosition();
        var teammates = team == Blackboard.Team.A ? Blackboard.Instance.teamAAgents : Blackboard.Instance.teamBAgents;

        IFootballAgent bestTeammate = null;
        float bestScore = float.MaxValue;

        foreach (var teammate in teammates)
        {
            if (teammate == (IFootballAgent)this || teammate == null) continue;
            if (!HasClearLineOfSight(transform.position, teammate.transform.position, 1.3f)) continue;

            float distToGoalTeammate = Vector2.Distance(teammate.transform.position, goalPos);
            float passDistance = Vector2.Distance(transform.position, teammate.transform.position);
            float score = distToGoalTeammate * 2.5f + passDistance * 0.6f + CountOpponentsNearPosition(teammate.transform.position, 5f) * 4f;

            if (score < bestScore)
            {
                bestScore = score;
                bestTeammate = teammate;
            }
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

        Vector2 dribbleTarget = GetBestDribbleTarget(10f);
        if (MoveTowards(dribbleTarget, 2f)) return BTStatus.Success;

        debugState = "Dribbling - Smart Space";
        return BTStatus.Running;
    }

    BTStatus TryTackle()
    {
        if (isTackleStunned) return BTStatus.Failure;
        if (ball.currentHolder == null || ball.currentHolder.team == team) return BTStatus.Failure;

        float tackleRange = 1.1f;
        float dist = Vector2.Distance(transform.position, ball.currentHolder.transform.position);

        if (dist > tackleRange + 0.3f)
        {
            MoveTowards(ball.currentHolder.transform.position, tackleRange);
            debugState = "Closing In To Tackle";
            return BTStatus.Running;
        }

        if (dist <= tackleRange)
        {
            float finalTackleChance = TackleChance;

            if (ball.currentHolder.rb.velocity.magnitude > 3f) finalTackleChance *= 0.65f;
            if (rb.velocity.magnitude < 2f) finalTackleChance *= 1.2f;

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

    BTStatus MarkOpponent()
    {
        var opponents = team == Blackboard.Team.A ? Blackboard.Instance.teamBAgents : Blackboard.Instance.teamAAgents;
        IFootballAgent marked = null;
        float bestScore = float.MaxValue;

        Vector2 ourGoal = GetOurGoalPosition();

        foreach (var opp in opponents)
        {
            if (opp == null) continue;

            float distToGoal = Vector2.Distance(opp.transform.position, ourGoal);
            float distToSelf = Vector2.Distance(transform.position, opp.transform.position);

            float score = distToGoal * 1.5f + distToSelf * 0.8f;
            if (score < bestScore)
            {
                bestScore = score;
                marked = opp;
            }
        }

        if (marked == null)
        {
            debugState = "Marking - No Target";
            return BTStatus.Failure;
        }

        Vector2 blockDir = ((Vector2)marked.transform.position - ourGoal).normalized;
        Vector2 markTarget = (Vector2)marked.transform.position - blockDir * 1.8f;

        if(MoveTowards(markTarget, 0.6f)) { return BTStatus.Success; };
        debugState = "Marking Lane";
        return BTStatus.Running;
        
    }
    #endregion

    #region Utility AI Helpers
    private Vector2 GetOurGoalPosition()
    {
        return team == Blackboard.Team.A ? Blackboard.Instance.goalBPosition : Blackboard.Instance.goalAPosition;
    }

    private Vector2 GetOpponentGoalPosition()
    {
        return team == Blackboard.Team.A ? Blackboard.Instance.goalAPosition : Blackboard.Instance.goalBPosition;
    }

    private bool HasClearLineOfSight(Vector2 start, Vector2 end, float clearanceRadius = 1.5f)
    {
        Vector2 dir = end - start;
        float dist = dir.magnitude;
        if (dist < 0.5f) return true;

        dir.Normalize();

        var opponents = team == Blackboard.Team.A ? Blackboard.Instance.teamBAgents : Blackboard.Instance.teamAAgents;
        foreach (var opp in opponents)
        {
            if (opp == null) continue;

            Vector2 toOpp = (Vector2)opp.transform.position - start;
            float oppDist = toOpp.magnitude;

            if (oppDist > dist + 2f || oppDist < 0.5f) continue;

            float proj = Vector2.Dot(toOpp, dir);
            if (proj < 0f || proj > dist) continue;

            Vector2 closestOnLine = dir * proj;
            float perp = (toOpp - closestOnLine).magnitude;

            if (perp <= clearanceRadius) return false;
        }

        return true;
    }

    private Vector2 Rotate(Vector2 v, float angleDeg)
    {
        float rad = angleDeg * Mathf.Deg2Rad;
        float c = Mathf.Cos(rad);
        float s = Mathf.Sin(rad);
        return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
    }

    private float GetDirectionClearance(Vector2 dir, float maxLook = 12f, float sideClearance = 2.2f)
    {
        float clearance = maxLook;
        var opponents = team == Blackboard.Team.A ? Blackboard.Instance.teamBAgents : Blackboard.Instance.teamAAgents;
        Vector2 pos = transform.position;

        foreach (var opp in opponents)
        {
            if (opp == null) continue;

            Vector2 toOpp = (Vector2)opp.transform.position - pos;
            float distToOpp = toOpp.magnitude;
            if (distToOpp > maxLook + 3f) continue;

            float proj = Vector2.Dot(toOpp, dir);
            if (proj < 0f) continue;

            Vector2 projPoint = dir * proj;
            float perpDist = (toOpp - projPoint).magnitude;

            if (perpDist < sideClearance)
                clearance = Mathf.Min(clearance, proj);
        }

        return clearance;
    }

    private Vector2 GetBestDribbleTarget(float lookAhead = 10f)
    {
        Vector2 goal = GetOpponentGoalPosition();
        Vector2 goalDir = (goal - (Vector2)transform.position).normalized;

        float bestScore = -Mathf.Infinity;
        Vector2 bestOffset = goalDir * lookAhead;

        for (int i = -5; i <= 5; i++)
        {
            float angleOffset = i * 10f;
            Vector2 testDir = Rotate(goalDir, angleOffset);
            float clearance = GetDirectionClearance(testDir, lookAhead + 3f);
            float progress = Vector2.Dot(testDir, goalDir);
            float score = progress * 1.1f + (clearance / lookAhead) * 0.9f;

            if (score > bestScore)
            {
                bestScore = score;
                bestOffset = testDir * lookAhead;
            }
        }

        if (Time.time % 0.4f < Time.deltaTime)
            dribbleWobbleOffset = Random.insideUnitCircle * 1.8f;

        return (Vector2)transform.position + bestOffset + dribbleWobbleOffset;
    }
    #endregion

    #region Helper
   

    int CountOpponentsNearby(float radius) => CountOpponentsNearPosition(transform.position, radius);
    int CountOpponentsNearPosition(Vector2 pos, float radius)
    {
        var opponents = team == Blackboard.Team.A
            ? Blackboard.Instance.teamBAgents
            : Blackboard.Instance.teamAAgents;

        int count = 0;
        foreach (var opp in opponents)
        {
            if (opp != null && Vector2.Distance(pos, opp.transform.position) <= radius)
                count++;
        }

        return count;
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
            if (d < bestDist)
            {
                bestDist = d;
                closest = t;
            }
        }

        return closest;
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

#if UNITY_EDITOR
        UnityEditor.Handles.color = Color.white;
        UnityEditor.Handles.Label(transform.position + Vector3.up * 0.5f, debugState);
#endif
    }
    #endregion
}
