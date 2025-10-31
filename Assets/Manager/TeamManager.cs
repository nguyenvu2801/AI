using UnityEngine;
using System.Collections.Generic;

public class TeamManager : MonoBehaviour
{
    public Blackboard.Team team;
    public GameObject playerPrefab;
    public Transform teamParent;
    public int playerCount = 5;
    public TeamTactic currentTactic;
    public Vector2 teamCenter = Vector2.zero;
    private List<PlayerAgent> players = new List<PlayerAgent>();

    public void SetupTeam(TeamTactic tactic, Vector2 center, Blackboard.Team t)
    {
        currentTactic = tactic;
        teamCenter = center;
        team = t;
        SpawnDefaultFormation();
    }

    void SpawnDefaultFormation()
    {
        // Simple formation around center: spread players horizontally, no Y offset to avoid drift issues
        for (int i = 0; i < playerCount; i++)
        {
            var go = Instantiate(playerPrefab, (Vector3)teamCenter + new Vector3((i - (playerCount - 1) / 2f) * 1.2f, 0f, 0), Quaternion.identity, teamParent); // Fix: Set Y=0
            var agent = go.GetComponent<PlayerAgent>();
            agent.team = team;
            agent.formationPosition = go.transform.localPosition; // Use localPosition for relative
            players.Add(agent);
            // Assign roles roughly
            if (i == 0) agent.role = Role.Goalkeeper;
            else if (i == 1) agent.role = Role.Defender;
            else if (i == 2) agent.role = Role.Midfielder;
            else if (i == 3) agent.role = Role.Winger;
            else agent.role = Role.Striker;
        }
    }

    public List<PlayerAgent> GetPlayers() { return players; }
}
