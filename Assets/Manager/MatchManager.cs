using UnityEngine;
using System.Collections.Generic;

public class MatchManager : MonoBehaviour
{
    public TeamManager teamA, teamB;
    public BallController ball;
    public Blackboard bb;
    public TeamTactic tacticA, tacticB;
    public float matchDuration = 90f;

    // Optional: small delay before reset (feels better)
    public float resetDelay = 1.2f;     //  you can set this in inspector

    private void Start()
    {
        if (bb == null) bb = Blackboard.Instance;
        bb.timeRemaining = matchDuration;
        bb.teamATactic = tacticA;
        bb.teamBTactic = tacticB;

        teamA.SetupTeam(tacticA, Blackboard.Instance.TeamAcenter, Blackboard.Team.A);
        teamB.SetupTeam(tacticB, Blackboard.Instance.TeamBcenter, Blackboard.Team.B);

        ResetBallAndPlayers();  // initial placement

        // cache goals
        var goals = FindObjectsOfType<Goal>();
        foreach (var goal in goals)
        {
            if (goal.scoringTeam == Blackboard.Team.A)
                bb.goalAPosition = (Vector2)goal.transform.position;
            else if (goal.scoringTeam == Blackboard.Team.B)
                bb.goalBPosition = (Vector2)goal.transform.position;
        }
    }

    void Update()
    {
        bb.timeRemaining -= Time.deltaTime;
        if (bb.timeRemaining <= 0f)
        {
            EndMatch();
        }

        // Keep ball position up to date (used by many agents)
        if (ball != null)
            bb.ballPosition = ball.transform.position;
    }

    public void ResetAfterGoal(Blackboard.Team scoringTeam)
    {
        // You can use scoringTeam later (e.g. give possession, kick-off direction)
        StartCoroutine(ResetAfterDelay());
    }

    private System.Collections.IEnumerator ResetAfterDelay()
    {
        yield return new WaitForSeconds(resetDelay);

        ResetBallAndPlayers();
    }

    private void ResetBallAndPlayers()
    {
        // Reset ball
        if (ball != null)
        {
            ball.transform.position = Vector2.zero;
            ball.rb.velocity = Vector2.zero;
            ball.rb.angularVelocity = 0f;
        }
        if (teamA != null)
        {
            teamA.SpawnFormation();
            teamA.RegisterPlayersToBlackboard();  
        }

        if (teamB != null)
        {
            teamB.SpawnFormation();
            teamB.RegisterPlayersToBlackboard();   
        }
    }

    private void EndMatch()
    {
        Debug.Log($"Match ended  A {bb.scoreA} – B {bb.scoreB}");
        enabled = false;
    }
}