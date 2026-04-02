using UnityEngine;

public class Goal : MonoBehaviour
{
    public Blackboard.Team scoringTeam;

    void OnTriggerEnter2D(Collider2D col)
    {
        if (col.GetComponent<BallController>() == null) return;

        Blackboard.Instance.scoreA += scoringTeam == Blackboard.Team.A ? 1 : 0;
        Blackboard.Instance.scoreB += scoringTeam == Blackboard.Team.B ? 1 : 0;

        Debug.Log($"GOAL for {scoringTeam}! Score: A {Blackboard.Instance.scoreA} - B {Blackboard.Instance.scoreB}");

        FindObjectOfType<MatchManager>()?.ResetAfterGoal(scoringTeam);
    }
}