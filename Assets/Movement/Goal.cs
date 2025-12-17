using UnityEngine;

public class Goal : MonoBehaviour
{
    public Blackboard.Team scoringTeam; 
    void OnTriggerEnter2D(Collider2D col)
    {
        var ball = col.GetComponent<BallController>();
        if (ball != null)
        {
            if (scoringTeam == Blackboard.Team.A) Blackboard.Instance.scoreA++;
            else Blackboard.Instance.scoreB++;
            // reset ball:
            ball.rb.position = Vector2.zero;
            ball.rb.velocity = Vector2.zero;
            // optionally give ball to opposite team goalkeeper
        }
    }
}