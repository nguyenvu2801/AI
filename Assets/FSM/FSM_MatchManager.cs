using UnityEngine;
using System.Collections;

public class FSM_MatchManager : MonoBehaviour
{
    [Header("Core References")]
    public FSM_Blackboard bb;
    public FSM_BallController ball;

    [Header("Teams")]
    public Transform teamASpawnRoot;
    public Transform teamBSpawnRoot;

    public FSM_TeamManager teamA;
    public FSM_TeamManager teamB;

    public TeamTactic tacticA;
    public TeamTactic tacticB;

    public Vector2 centerA;
    public Vector2 centerB;

    [Header("Match Settings")]
    public float matchDuration = 90f;
    public float resetDelay = 1.5f;

    private float timeRemaining;
    private bool matchEnded = false;

    void Start()
    {
        if (bb == null) bb = FSM_Blackboard.Instance;

        timeRemaining = matchDuration;

        // Spawn teams FIRST
        teamA.SetupTeam(tacticA, centerA, FSM_Blackboard.Team.A);
        teamB.SetupTeam(tacticB, centerB, FSM_Blackboard.Team.B);

        CacheGoalPositions();
        ResetPositions();
    }

    void Update()
    {
        if (matchEnded) return;

        timeRemaining -= Time.deltaTime;

        if (timeRemaining <= 0f)
        {
            EndMatch();
        }
    }
    void CacheGoalPositions()
    {
        var goals = FindObjectsOfType<Collider2D>();

        foreach (var g in goals)
        {
            if (g.CompareTag(ball.goalTagA))
                bb.goalAPosition = g.transform.position;

            if (g.CompareTag(ball.goalTagB))
                bb.goalBPosition = g.transform.position;
        }
    }

    public void OnGoalScored(FSM_Blackboard.Team scoringTeam)
    {
        StartCoroutine(ResetAfterDelay());
    }

    IEnumerator ResetAfterDelay()
    {
        yield return new WaitForSeconds(resetDelay);
        ResetPositions();
    }

    void ResetPositions()
    {
        ResetBall();
        ResetPlayers(teamASpawnRoot);
        ResetPlayers(teamBSpawnRoot);
    }

    void ResetBall()
    {
        if (ball == null) return;
        ball.ResetBall();
    }

    void ResetPlayers(Transform root)
    {
        if (root == null) return;

        foreach (Transform child in root)
        {
            var agent = child.GetComponent<FSM_PlayerAgent>();
            if (agent == null) continue;

            agent.transform.position = agent.formationWorldPos;
            agent.rb.velocity = Vector2.zero;
        }
    }

    void EndMatch()
    {
        matchEnded = true;
        Debug.Log("Match End - Score A: " + bb.scoreA + " | B: " + bb.scoreB);
    }
}