using UnityEngine;

public class Goal : MonoBehaviour
{
    public Blackboard.Team scoringTeam;     // Team that SCORES when ball enters this goal

    void OnTriggerEnter2D(Collider2D col)
    {
        var ball = col.GetComponent<BallController>();
        if (ball == null) return;

        // Update score
        if (scoringTeam == Blackboard.Team.A)
            Blackboard.Instance.scoreA++;
        else
            Blackboard.Instance.scoreB++;

        // Option A: Most common – notify MatchManager to handle full reset
        var match = FindObjectOfType<MatchManager>();
        if (match != null)
        {
            match.ResetAfterGoal(scoringTeam);
        }
        else
        {
            // Fallback – at least reset ball if MatchManager is missing
            ResetBallOnly(ball);
        }
    }

    // Only resets ball (useful for debugging or fallback)
    private void ResetBallOnly(BallController ball)
    {
        ball.transform.position = Vector2.zero;
        ball.rb.velocity = Vector2.zero;
        ball.rb.angularVelocity = 0f;
    }
}