using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class GridManager : MonoBehaviour
{
    public Tilemap blockingTilemap; 
    private Vector3Int gridMin;
    private Vector3Int gridMax;
    private HashSet<Vector3Int> walkableCells = new HashSet<Vector3Int>(); //ensures no duplicate positions are stored
    void Awake()
    {
        BuildGridFromTilemaps();
    }

    [ContextMenu("Build Grid From Tilemaps")]//in order to rebuild grid mannually
    public void BuildGridFromTilemaps()
    {
        walkableCells.Clear();

        // If gridMin/GridMax are left 0, compute from tilemaps bounds
        if (blockingTilemap != null)
        {
            var bounds = blockingTilemap.cellBounds;
            gridMin = bounds.min;
            gridMax = bounds.max;
        }
        // Loop over every cell in the rectangle
        for (int x = gridMin.x; x <= gridMax.x; x++)
        {
            for (int y = gridMin.y; y <= gridMax.y; y++)
            {
                Vector3Int cell = new Vector3Int(x, y, 0);
                bool blocked = (blockingTilemap != null && blockingTilemap.HasTile(cell));
                if (!blocked)
                {
                    walkableCells.Add(cell);
                }
            }
        }

        Debug.Log($"Grid built: walkable cells = {walkableCells.Count}");
    }

    public bool IsWalkable(Vector3Int cell)
    {
        return walkableCells.Contains(cell);
    }
    // Convert a world position (Vector3) to a cell coordinate (Vector3Int).
   
    public Vector3Int WorldToCell(Vector3 worldPos)
    {
        if (blockingTilemap != null) return blockingTilemap.WorldToCell(worldPos);
        return Vector3Int.RoundToInt(worldPos);
    }
    // Returns the center world position for a given cell.
    public Vector3 CellToWorldCenter(Vector3Int cell)
    {
        if (blockingTilemap != null) return blockingTilemap.GetCellCenterWorld(cell);
        return (Vector3)cell + Vector3.one * 0.5f;
    }

    // neighbors in 4 directions
    public IEnumerable<Vector3Int> GetNeighbors(Vector3Int cell)
    {
        var directions = new Vector3Int[] {
            new Vector3Int(1,0,0),
            new Vector3Int(-1,0,0),
            new Vector3Int(0,1,0),
            new Vector3Int(0,-1,0)
        };

        foreach (var d in directions)
        {
            var n = cell + d;
            if (IsWalkable(n)) yield return n;
        }
    }
}
