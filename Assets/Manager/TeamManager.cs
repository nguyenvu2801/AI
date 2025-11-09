using UnityEngine;
using System.Collections.Generic;

public class TeamManager : MonoBehaviour
{
    public Blackboard.Team team;
    public GameObject playerPrefab;
    public Transform teamParent;
    private int playerCount = 11;
    public TeamTactic currentTactic;

    public Vector2 teamCenter = Vector2.zero;
    private List<PlayerAgent> players = new List<PlayerAgent>();
    private Dictionary<Role, RoleStats> roleStatsMap = new Dictionary<Role, RoleStats>();
    void Awake()
    {
        // Load all RoleStats from Resources/RoleStats folder (adjust path if different)
        RoleStats[] allStats = Resources.LoadAll<RoleStats>("RoleStats");
        foreach (var stats in allStats)
        {
            if (!roleStatsMap.ContainsKey(stats.role))
            {
                roleStatsMap[stats.role] = stats;
            }
            else
            {
                Debug.LogWarning($"Duplicate RoleStats for role {stats.role}");
            }
        }
    }
    public void SetupTeam(TeamTactic tactic, Vector2 center, Blackboard.Team t)
    {
        currentTactic = tactic;
        teamCenter = center;
        team = t;
        SpawnFormation();
    }

    void SpawnFormation()
    {
        if (currentTactic?.formation == null)
        {
            Debug.LogError($"No formation assigned to tactic '{currentTactic?.name}' for team {team}");
            return;
        }

        var formationData = currentTactic.formation;
        if (formationData.positions.Length != playerCount || formationData.roles.Length != playerCount)
        {
            Debug.LogError($"Formation '{formationData.formationName}' has {formationData.positions.Length} positions, expected {playerCount}");
            return;
        }

        // Clear existing players if respawning
        foreach (var player in players) Destroy(player.gameObject);
        players.Clear();

        for (int i = 0; i < playerCount; i++)
        {
            Vector2 relPos = formationData.positions[i];

            // Mirror X for Team B (assumes positions defined for Team A: negative X = defense)
            if (team == Blackboard.Team.B)
            {
                relPos.x = -relPos.x;
            }

            Vector3 worldPos = teamParent == null
                ? (Vector3)(teamCenter + relPos)
                : teamParent.TransformPoint((Vector3)(teamCenter + relPos)); // If teamParent not at origin

            var go = Instantiate(playerPrefab, worldPos, Quaternion.identity, teamParent);
            var agent = go.GetComponent<PlayerAgent>();
            if (agent == null)
            {
                Debug.LogError("PlayerPrefab missing PlayerAgent!");
                Destroy(go);
                continue;
            }

            agent.team = team;
            agent.formationPosition = new Vector3(relPos.x, relPos.y, 0f); // Mirrored relative pos for AI
            agent.role = formationData.roles[i];
            agent.roleStats = roleStatsMap.ContainsKey(agent.role) ? roleStatsMap[agent.role] : null;
            players.Add(agent);
        }

        Debug.Log($"Spawned {playerCount} players in {formationData.formationName} for {team}");
    }

    public List<PlayerAgent> GetPlayers() { return players; }
}
