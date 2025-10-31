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

    public enum Team { A, B, None }
}