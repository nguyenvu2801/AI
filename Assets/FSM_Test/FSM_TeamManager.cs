using UnityEngine;
using System.Collections.Generic;

public class FSMTeamManager : MonoBehaviour
{
    public Blackboard.Team team;
    public GameObject fsmPlayerPrefab;
    public Transform teamParent;
    public TeamTactic currentTactic;
    public Vector2 teamCenter = Vector2.zero;

    private int playerCount = 11;
    private List<FSMOpponentAgent> players = new List<FSMOpponentAgent>();
    private Dictionary<Role, RoleStats> roleStatsMap = new Dictionary<Role, RoleStats>();

    void Awake()
    {
        RoleStats[] allStats = Resources.LoadAll<RoleStats>("RoleStats");
        foreach (var stats in allStats)
            if (!roleStatsMap.ContainsKey(stats.role))
                roleStatsMap[stats.role] = stats;
    }

    public void SetupTeam(TeamTactic tactic, Vector2 center, Blackboard.Team t)
    {
        currentTactic = tactic;
        teamCenter = center;
        team = t;
        SpawnFormation();
    }

    public void SpawnFormation()
    {
        if (currentTactic?.formation == null) { Debug.LogError("No formation!"); return; }

        foreach (var p in players) if (p != null) Destroy(p.gameObject);
        players.Clear();

        var formationData = currentTactic.formation;

        for (int i = 0; i < playerCount; i++)
        {
            Vector2 relPos = formationData.positions[i];
            if (team == Blackboard.Team.B) relPos.x = -relPos.x;

            Vector3 worldPos = (Vector3)(teamCenter + relPos);
            var go = Instantiate(fsmPlayerPrefab, worldPos, Quaternion.identity, teamParent);
            var agent = go.GetComponent<FSMOpponentAgent>();
            if (agent == null) { Destroy(go); continue; }

            agent.team = team;
            agent.formationWorldPos = relPos;
            agent.role = formationData.roles[i];
            agent.roleStats = roleStatsMap.ContainsKey(agent.role) ? roleStatsMap[agent.role] : null;
            agent.ApplyRoleStats(currentTactic);
            players.Add(agent);
        }

        RegisterToBlackboard();
    }

    public void RegisterToBlackboard()
    {
        var list = team == Blackboard.Team.A
            ? Blackboard.Instance.teamAAgents
            : Blackboard.Instance.teamBAgents;
        list.Clear();
        foreach (var p in players) list.Add(p);
    }
}