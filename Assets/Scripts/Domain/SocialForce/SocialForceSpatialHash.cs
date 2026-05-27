using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Grid-based spatial hash for agent-agent neighbor lookups.
/// Divides world XZ space into uniform cells. Each cell stores agent indices.
/// Cell lists are reused across frames via Clear() to avoid per-frame allocations.
///
/// Cell size is set to the neighbor search radius so that any query covers at most a 3x3 block of cells.
/// </summary>
public sealed class SocialForceSpatialHash
{
    readonly Dictionary<Vector2Int, List<int>> _cells = new();
    float _cellSize = 1f;

    /// <summary>
    /// Rebuilds the spatial hash from scratch each frame.
    /// Existing cell lists are cleared (not recreated) to avoid GC pressure.
    /// </summary>
    /// <param name="agents">Agent list to index.</param>
    /// <param name="cellSize">Grid cell size in meters (typically NeighborSearchRadius).</param>
    public void Rebuild(IReadOnlyList<SocialForceAgentState> agents, float cellSize)
    {
        foreach (var list in _cells.Values)
            list.Clear();

        _cellSize = Mathf.Max(0.1f, cellSize);

        for (var i = 0; i < agents.Count; i++)
        {
            var c = CellOf(agents[i].Position);
            if (!_cells.TryGetValue(c, out var list))
            {
                list = new List<int>(8);
                _cells[c] = list;
            }

            list.Add(i);
        }
    }

    /// <summary>
    /// Queries all agents within the specified radius of a position.
    /// Returns agent indices covering a square region of cells that encloses the circle.
    /// The caller should filter by exact distance if precision is needed.
    /// </summary>
    public void Query(Vector2 position, float radius, List<int> results)
    {
        results.Clear();
        var min = CellOf(position - Vector2.one * radius);
        var max = CellOf(position + Vector2.one * radius);

        for (var y = min.y; y <= max.y; y++)
        {
            for (var x = min.x; x <= max.x; x++)
            {
                if (_cells.TryGetValue(new Vector2Int(x, y), out var list))
                    results.AddRange(list);
            }
        }
    }

    Vector2Int CellOf(Vector2 p) =>
        new(Mathf.FloorToInt(p.x / _cellSize), Mathf.FloorToInt(p.y / _cellSize));
}
