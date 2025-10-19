using System.Collections;
using System.Collections.Generic;
using UnityEditor.Experimental.GraphView;
using UnityEngine;

public class Player : MonoBehaviour
{
    public Blackboard blackboard;
    public Astar pathfinder;
    public MovementController mover;
    public GridManager gridManager;
    public float enemyDangerDistance = 3f;

    private BTNode root;

    void Awake()
    {
        if (blackboard == null) blackboard = GetComponent<Blackboard>();
        BuildTree();
    }

    void Update()
    {
        root.Tick(blackboard);
        if (Time.frameCount % 30 == 0) blackboard.RememberVisited(transform.position);
    }

    void BuildTree()
    {
        // Conditions (unchanged)
        var enemyNearby = new ConditionNode(bb =>
        {
            float minDist = float.MaxValue;
            Vector3 pos = transform.position;
            foreach (var e in blackboard.enemyPositions)
            {
                float d = Vector3.Distance(pos, e);
                if (d < minDist) { minDist = d; bb.lastSeenEnemy = e; }
            }
            return minDist <= enemyDangerDistance;
        });

        // Action: EvadeEnemy (updated to avoid reset)
        var evade = new ActionNode(bb =>
        {
            if (mover.IsFollowingPath) // Assume this checks if path is active; implement if needed
            {
                return NodeState.Running;
            }

            Vector3 fleeDir = (transform.position - bb.lastSeenEnemy).normalized;
            if (fleeDir.sqrMagnitude < 0.01f) fleeDir = Random.insideUnitCircle.normalized;

            Vector3 desired = transform.position + fleeDir * (enemyDangerDistance * 2f);

            var path = pathfinder.FindPath(transform.position, desired);
            if (path != null && path.Count > 0)
            {
                mover.FollowPath(path);
                bb.isSafe = false;
                return NodeState.Running;
            }
            return NodeState.Failure;
        });

        var emergencySequence = new SequenceNode(enemyNearby, evade);

        // KeySequence
        var hasKeyCond = new ConditionNode(bb => bb.hasKey == false);

        var findKey = new ActionNode(bb =>
        {
            if (mover.IsFollowingPath)
            {
                return NodeState.Running;
            }

            if (bb.keyPosition == Vector3.zero)
            {
                var target = FindNearestUnvisited();
                if (target != null)
                {
                    var path = pathfinder.FindPath(transform.position, target.Value);
                    if (path != null)
                    {
                        mover.FollowPath(path);
                        return NodeState.Running;
                    }
                }
                return NodeState.Failure;
            }
            else
            {
                var path = pathfinder.FindPath(transform.position, bb.keyPosition);
                if (path != null)
                {
                    mover.FollowPath(path);
                    return NodeState.Running;
                }
                return NodeState.Failure;
            }
        });

        var keySequence = new SequenceNode(hasKeyCond, findKey);

        // DoorSequence
        var hasKeyTrue = new ConditionNode(bb => bb.hasKey == true);
        var moveToDoor = new ActionNode(bb =>
        {
            if (mover.IsFollowingPath)
            {
                return NodeState.Running;
            }

            var path = pathfinder.FindPath(transform.position, bb.doorPosition);
            if (path != null)
            {
                mover.FollowPath(path);
                return NodeState.Running;
            }
            return NodeState.Failure;
        });
        var doorSequence = new SequenceNode(hasKeyTrue, moveToDoor);

        // Exploration (updated to avoid reset)
        var explore = new ActionNode(bb =>
        {
            if (mover.IsFollowingPath)
            {
                return NodeState.Running;
            }

            var target = FindNearestUnvisited();
            if (target != null)
            {
                var path = pathfinder.FindPath(transform.position, target.Value);
                if (path != null)
                {
                    mover.FollowPath(path);
                    return NodeState.Running;
                }
            }
            // Fallback: random wander
            var randomOffset = new Vector3(Random.Range(-5, 5), Random.Range(-5, 5), 0);
            var pos = transform.position + randomOffset;
            var p = pathfinder.FindPath(transform.position, pos);
            if (p != null)
            {
                mover.FollowPath(p);
                return NodeState.Running;
            }
            return NodeState.Failure;
        });

        var explorationSequence = new SequenceNode(explore);

        // Root selector (unchanged)
        root = new SelectorNode(emergencySequence, keySequence, doorSequence, explorationSequence);
    }

    Vector3? FindNearestUnvisited()
    {
        // search grid cells within some radius for a cell not in blackboard.visitedTiles
        int searchRadius = 10;
        Vector3Int centerCell = gridManager.WorldToCell(transform.position);
        float bestDist = float.MaxValue;
        Vector3? bestWorld = null;

        for (int dx = -searchRadius; dx <= searchRadius; dx++)
        {
            for (int dy = -searchRadius; dy <= searchRadius; dy++)
            {
                Vector3Int c = new Vector3Int(centerCell.x + dx, centerCell.y + dy, 0);
                if (!gridManager.IsWalkable(c)) continue;
                Vector3 w = gridManager.CellToWorldCenter(c);
                // approximate visited check by rounded positions
                Vector3 rounded = new Vector3(Mathf.Round(w.x), Mathf.Round(w.y), 0);
                if (blackboard.visitedTiles.Contains(rounded)) continue;
                float d = Vector3.Distance(transform.position, w);
                if (d < bestDist)
                {
                    bestDist = d;
                    bestWorld = w;
                }
            }
        }
        return bestWorld;
    }
}
