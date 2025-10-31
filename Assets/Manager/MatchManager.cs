// Assets/Scripts/Gameplay/MatchManager.cs
using UnityEngine;

public class MatchManager : MonoBehaviour
{
    public TeamManager teamA, teamB;
    public BallController ball;
    public Blackboard bb;
    public TeamTactic tacticA, tacticB;
    public float matchDuration = 90f; // seconds

    void Start()
    {
        // Register blackboard and set up
        if (bb == null) bb = Blackboard.Instance;
        bb.timeRemaining = matchDuration;
        bb.teamATactic = tacticA;
        bb.teamBTactic = tacticB;
        teamA.SetupTeam(tacticA, new Vector2(-6, 0), Blackboard.Team.A);
        teamB.SetupTeam(tacticB, new Vector2(6, 0), Blackboard.Team.B);

        // Place ball at center
        ball.transform.position = Vector2.zero;
        bb.ballObject = ball.gameObject;
        bb.ballPosition = ball.transform.position;
    }

    void Update()
    {
        bb.timeRemaining -= Time.deltaTime;
        if (bb.timeRemaining <= 0f)
        {
            EndMatch();
        }
    }

    void EndMatch()
    {
        Debug.Log("Match ended");
        // Stop AI maybe via a simple flag (not implemented here)
        enabled = false;
    }
}
