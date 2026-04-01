using UnityEngine;
using System.Collections.Generic;

public class FSM_TeamManager : MonoBehaviour
{
    public FSM_Blackboard.Team team;
    public GameObject playerPrefab;
    public Transform teamParent;

    private int playerCount = 11;

    public TeamTactic currentTactic;
    public Vector2 teamCenter = Vector2.zero;

    private List<FSM_PlayerAgent> players = new List<FSM_PlayerAgent>();
    private Dictionary<Role, RoleStats> roleStatsMap = new Dictionary<Role, RoleStats>();

    void Awake()
    {
        RoleStats[] allStats = Resources.LoadAll<RoleStats>("RoleStats");

        foreach (var stats in allStats)
        {
            if (!roleStatsMap.ContainsKey(stats.role))
                roleStatsMap[stats.role] = stats;
            else
                Debug.LogWarning("Duplicate RoleStats for role " + stats.role);
        }

    }

    // Register to FSM Blackboard
    public void RegisterPlayersToBlackboard()
    {
        var bb = FSM_Blackboard.Instance;

        if (team == FSM_Blackboard.Team.A)
        {
            bb.teamAAgents.Clear();
            bb.teamAAgents.AddRange(players);
        }
        else if (team == FSM_Blackboard.Team.B)
        {
            bb.teamBAgents.Clear();
            bb.teamBAgents.AddRange(players);
        }
    }

    // Setup team
    public void SetupTeam(TeamTactic tactic, Vector2 center, FSM_Blackboard.Team t)
    {
        currentTactic = tactic;
        teamCenter = center;
        team = t;

        SpawnFormation();
    }

    // Spawn players based on formation
    public void SpawnFormation()
    {
        if (currentTactic == null || currentTactic.formation == null)
        {
            Debug.LogError("No formation assigned to tactic for team " + team);
            return;
        }

        var formationData = currentTactic.formation;

        if (formationData.positions.Length != playerCount ||
            formationData.roles.Length != playerCount)
        {
            Debug.LogError("Formation " + formationData.formationName +
                " has incorrect player count");
            return;
        }

        // Clear old players
        foreach (var player in players)
        {
            if (player != null)
                Destroy(player.gameObject);
        }
        players.Clear();

        // Spawn new players
        for (int i = 0; i < playerCount; i++)
        {
            Vector2 relPos = formationData.positions[i];

            // Mirror for Team B
            if (team == FSM_Blackboard.Team.B)
                relPos.x = -relPos.x;

            Vector3 worldPos;

            if (teamParent == null)
                worldPos = teamCenter + relPos;
            else
                worldPos = teamParent.TransformPoint(teamCenter + relPos);

            GameObject go = Instantiate(playerPrefab, worldPos, Quaternion.identity, teamParent);

            FSM_PlayerAgent agent = go.GetComponent<FSM_PlayerAgent>();

            if (agent == null)
            {
                Debug.LogError("PlayerPrefab missing FSM_PlayerAgent");
                Destroy(go);
                continue;
            }

            agent.team = team;
            agent.formationWorldPos = relPos;
            agent.role = formationData.roles[i];

            if (roleStatsMap.ContainsKey(agent.role))
                agent.roleStats = roleStatsMap[agent.role];
            else
                agent.roleStats = null;

            agent.ApplyRoleStats(currentTactic);

            players.Add(agent);
        }

        RegisterPlayersToBlackboard();

        Debug.Log("Spawned " + playerCount + " players for team " + team);
    }

    // Change tactic mid-match
    public void ChangeTactic(TeamTactic newTactic)
    {
        if (newTactic == null || newTactic.formation == null)
        {
            Debug.LogError("Invalid tactic for team " + team);
            return;
        }

        if (currentTactic == newTactic) return;

        currentTactic = newTactic;

        Debug.Log("Team " + team + " changed tactic to " + newTactic.name);

        SpawnFormation();

        // Optional: update blackboard if you later extend tactics there
    }

    public List<FSM_PlayerAgent> GetPlayers()
    {
        return players;
    }
}