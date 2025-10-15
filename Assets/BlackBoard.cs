using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BlackBoard : MonoBehaviour
{
    [Header("Objective")]
    public bool hasKey = false;
    public Vector3 keyPosition;
    public Vector3 doorPosition;

    [Header("Perception")]
    public List<Vector3> enemyPositions = new List<Vector3>();
    public Vector3 lastSeenEnemy;

    [Header("Memory")]
    public List<Vector3> visitedTiles = new List<Vector3>();
    public Stack<Vector3> pathStack = new Stack<Vector3>();

    [Header("Safety")]
    public bool isSafe = true;

    // helper
    public void RememberVisited(Vector3 worldPos)
    {
        Vector3 center = new Vector3(Mathf.Round(worldPos.x), Mathf.Round(worldPos.y), 0); // approximate
        if (!visitedTiles.Contains(center))
            visitedTiles.Add(center);
    }
}
