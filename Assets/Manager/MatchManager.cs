using UnityEngine;
using System.Collections.Generic;
using UnityEngine.UI;

public class MatchManager : MonoBehaviour
{
    public TeamManager teamA;
    public FSMTeamManager teamB;       
    public BallController ball;
    public Blackboard bb;
    public TeamTactic tacticA, tacticB;
    public float matchDuration = 90f;
    public float resetDelay = 1.2f;
    public Button rematch;
    void Start()
    {
        if (bb == null) bb = Blackboard.Instance;
        bb.timeRemaining = matchDuration;
        bb.teamATactic = tacticA;
        bb.teamBTactic = tacticB;

        teamA.SetupTeam(tacticA, Blackboard.Instance.TeamAcenter, Blackboard.Team.A);
        teamB.SetupTeam(tacticB, Blackboard.Instance.TeamBcenter, Blackboard.Team.B);

        var goals = FindObjectsOfType<Goal>();
        foreach (var goal in goals)
        {
            if (goal.scoringTeam == Blackboard.Team.A) bb.goalAPosition = goal.transform.position;
            else bb.goalBPosition = goal.transform.position;
        }
     
            if (rematch != null)
            {
                rematch.onClick.AddListener(Rematch);
            }
        
        ResetBallAndPlayers();
    }

    void Update()
    {
        bb.timeRemaining -= Time.deltaTime;
        if (bb.timeRemaining <= 0f) EndMatch();
        if (ball != null) bb.ballPosition = ball.transform.position;
    }

    public void ResetAfterGoal(Blackboard.Team scoringTeam)
        => StartCoroutine(ResetAfterDelay());

    System.Collections.IEnumerator ResetAfterDelay()
    {
        yield return new WaitForSeconds(resetDelay);
        ResetBallAndPlayers();
    }

    void ResetBallAndPlayers()
    {
        ball.currentHolder = null;
        if (ball != null)
        {
            ball.transform.position = Vector2.zero;
            ball.rb.velocity = Vector2.zero;
            ball.rb.angularVelocity = 0f;
        }
        teamA.SpawnFormation();
        teamA.RegisterPlayersToBlackboard();
        teamB.SpawnFormation();
        teamB.RegisterToBlackboard();
    }
    public void Rematch()
    {
        if (bb == null) return;

        bb.scoreA = 0;
        bb.scoreB = 0;
        bb.timeRemaining = matchDuration;   
        ResetBallAndPlayers();
        Time.timeScale = 1f;
        enabled = true;

        if (FindObjectOfType<UIManager>() is UIManager ui)
        {
            ui.UpdateScoreAndTimeImmediately(); 
        }
    }
    void EndMatch()
    {
        Debug.Log($"Match ended — A {bb.scoreA} : {bb.scoreB} B");
        Time.timeScale = 0f;
        enabled = false;
    }
}