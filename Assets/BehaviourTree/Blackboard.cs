using System.Collections;
using System.Collections.Generic;
using UnityEngine;
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
    public List<PlayerAgent> teamAAgents = new List<PlayerAgent>();
    public List<PlayerAgent> teamBAgents = new List<PlayerAgent>();

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }
    public PlayerAgent GetClosestToBall(Blackboard.Team t)
    {
        var agents = (t == Team.A) ? teamAAgents : teamBAgents;
        PlayerAgent closest = null;
        float minDist = float.MaxValue;
        foreach (var a in agents)
        {
            float dist = Vector2.Distance(a.transform.position, ballPosition);
            if (dist < minDist) { minDist = dist; closest = a; }
        }
        return closest;
    }
    public enum Team { A, B, None }
}