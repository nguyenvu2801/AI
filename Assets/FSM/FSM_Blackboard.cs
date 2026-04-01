using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Shared singleton that all FSM agents read from.
/// Drop onto an empty GameObject in the scene.
/// </summary>
public class FSM_Blackboard : MonoBehaviour
{
    public static FSM_Blackboard Instance { get; private set; }
    public float resetTimer = 0f;

   
    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    public enum Team { A, B }

    [HideInInspector] public List<FSM_PlayerAgent> teamAAgents = new();
    [HideInInspector] public List<FSM_PlayerAgent> teamBAgents = new();

    [Header("Goal positions (world space)")]
    public Vector2 goalAPosition;
    public Vector2 goalBPosition;

    [Header("Score (runtime)")]
    public int scoreA = 0;
    public int scoreB = 0;

    /// <summary>Returns the agent on <paramref name="team"/> closest to the ball.</summary>
    public FSM_PlayerAgent GetClosestToBall(Team team)
    {
        var ballCtrl = Object.FindFirstObjectByType<FSM_BallController>();
        if (ballCtrl == null) return null;

        var agents = team == Team.A ? teamAAgents : teamBAgents;
        FSM_PlayerAgent closest = null;
        float bestDist = float.MaxValue;

        foreach (var agent in agents)
        {
            if (agent == null) continue;
            float d = Vector2.Distance(agent.transform.position, ballCtrl.transform.position);
            if (d < bestDist) { bestDist = d; closest = agent; }
        }
        return closest;
    }

  
    /// <summary>Returns the agent on <paramref name="team"/> closest to the opponent's goal.</summary>
    public FSM_PlayerAgent GetMostAdvancedPlayer(Team team)
    {
        Vector2 targetGoal = team == Team.A ? goalAPosition : goalBPosition;
        var agents = team == Team.A ? teamAAgents : teamBAgents;

        FSM_PlayerAgent best = null;
        float bestDist = float.MaxValue;

        foreach (var agent in agents)
        {
            if (agent == null) continue;
            float d = Vector2.Distance(agent.transform.position, targetGoal);
            if (d < bestDist) { bestDist = d; best = agent; }
        }
        return best;
    }

    /// <summary>Increments score and resets registration lists if needed.</summary>
    public void AddGoal(Team scoringTeam)
    {
        if (scoringTeam == Team.A) scoreA++;
        else scoreB++;

        Debug.Log($"Goal! Score -> A:{scoreA}  B:{scoreB}");
    }

    void Update()
    {
        teamAAgents.RemoveAll(a => a == null);
        teamBAgents.RemoveAll(a => a == null);
        if (resetTimer > 0) resetTimer -= Time.deltaTime;
    }

#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(goalAPosition, 0.5f);
        UnityEditor.Handles.Label(goalAPosition, "Goal A");

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(goalBPosition, 0.5f);
        UnityEditor.Handles.Label(goalBPosition, "Goal B");
    }
    public void TriggerGoalReset()
    {
        resetTimer = 7.0f; // Players will force-return for 2 seconds
    }
#endif
}