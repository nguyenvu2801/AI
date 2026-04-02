using System.Collections;
using System.Collections.Generic;
using UnityEngine;
/// <summary>
/// Shared blackboard for AI agents.
/// Stores global match state and common queries.
/// </summary>
public class Blackboard : MonoBehaviour
{
    public static Blackboard Instance { get; private set; }

    public Vector2 ballPosition;
    public GameObject ballObject;
    public Team currentBallPossessionTeam; // small enum below
    public TeamTactic teamATactic;
    public TeamTactic teamBTactic;
    public Vector2 goalAPosition;
    public Vector2 goalBPosition;
    public Vector2 TeamAcenter;
    public Vector2 TeamBcenter;
    public float timeRemaining;
    public int scoreA;
    public int scoreB;

    // convenience caches:
    public List<IFootballAgent> teamAAgents = new List<IFootballAgent>();
    public List<IFootballAgent> teamBAgents = new List<IFootballAgent>();

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }
    public IFootballAgent GetClosestToBall(Team t)
    {
        var list = t == Team.A ? teamAAgents : teamBAgents;
        IFootballAgent closest = null;
        float best = float.MaxValue;
        foreach (var a in list)
        {
            float d = Vector2.Distance(a.transform.position, ballPosition);
            if (d < best) { best = d; closest = a; }
        }
        return closest;
    }
    public enum Team { A, B, None }
}