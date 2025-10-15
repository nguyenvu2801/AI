using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Astar : MonoBehaviour
{
    public GridManager gridManager;
    //represent 1 cell when finding the path
    public class Node
    {
        public Vector3Int cell; //cell pos
        public int g; //real cost from the start to end spot
        public int f; //sum to calculate most efficient path
        public Node parent; //track path
    }

    int Heuristic(Vector3Int a, Vector3Int b)
    {
        return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y); // Manhattan(used for estimated distance)
    }

    public List<Vector3> FindPath(Vector3 startWorld, Vector3 goalWorld)
    {
        if (gridManager == null) return null;
        // from wolrd cord to cell cord
        Vector3Int start = gridManager.WorldToCell(startWorld);
        Vector3Int goal = gridManager.WorldToCell(goalWorld);
        // if not had start or end point return null
        if (!gridManager.IsWalkable(start) || !gridManager.IsWalkable(goal)) return null;
        //init to checked cell and finalized
        var open = new Dictionary<Vector3Int, Node>();
        var closed = new HashSet<Vector3Int>();
        // create node
        Node startNode = new Node { cell = start, g = 0, f = Heuristic(start, goal), parent = null };
        open[start] = startNode;

        while (open.Count > 0)
        {
            // get node with lowest f
            Node current = null;
            foreach (var n in open.Values)
            {
                if (current == null || n.f < current.f) current = n;
            }

            if (current.cell == goal)// if goal reached reconstruct path
            {
                
                var pathCells = new List<Vector3Int>();
                var node = current;
                while (node != null)
                {
                    pathCells.Add(node.cell);
                    node = node.parent;
                }
                pathCells.Reverse();
                //convert the grid cells back to world positions:
                var worldPath = new List<Vector3>();
                foreach (var c in pathCells)
                    worldPath.Add(gridManager.CellToWorldCenter(c));
                return worldPath;
            }
            //Marks the node as visited so it won’t be checked again.
            open.Remove(current.cell);
            closed.Add(current.cell);
            //explore neigbourhood
            foreach (var neighbor in gridManager.GetNeighbors(current.cell))
            {
                if (closed.Contains(neighbor)) continue;

                int tentativeG = current.g + 1;

                if (!open.TryGetValue(neighbor, out Node neighborNode))
                {
                    neighborNode = new Node { cell = neighbor };
                    open[neighbor] = neighborNode;
                }

                if (tentativeG < neighborNode.g || neighborNode.parent == null)
                {
                    neighborNode.g = tentativeG;
                    neighborNode.f = tentativeG + Heuristic(neighbor, goal);
                    neighborNode.parent = current;
                }
            }
        }

        // no path
        return null;
    }
}
