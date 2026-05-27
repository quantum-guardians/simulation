using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Grid-based spatial hash for wall segment queries.
/// Each wall segment is inserted into all cells that overlap its AABB (axis-aligned bounding box).
/// Since a single wall segment may span multiple cells, the Query method deduplicates
/// using a HashSet to avoid processing the same wall multiple times.
///
/// Unlike the agent spatial hash, this is rebuilt only when walk boundaries change
/// (i.e., after road graph modifications), not every frame.
/// </summary>
public sealed class SocialForceWallSpatialHash
{
    readonly Dictionary<Vector2Int, List<int>> _cells = new();
    readonly HashSet<int> _seen = new();
    float _cellSize = 1f;

    /// <summary>
    /// Rebuilds the wall spatial hash.
    /// Each wall segment is added to all cells overlapping its bounding box.
    /// Existing cell lists are cleared and reused.
    /// </summary>
    public void Rebuild(IReadOnlyList<SocialForceWallSegment> walls, float cellSize)
    {
        foreach (var list in _cells.Values)
            list.Clear();

        _cellSize = Mathf.Max(0.1f, cellSize);

        for (var i = 0; i < walls.Count; i++)
        {
            var w = walls[i];
            var min = Vector2.Min(w.A, w.B);
            var max = Vector2.Max(w.A, w.B);
            var cMin = CellOf(min);
            var cMax = CellOf(max);

            for (var y = cMin.y; y <= cMax.y; y++)
            {
                for (var x = cMin.x; x <= cMax.x; x++)
                {
                    var cell = new Vector2Int(x, y);
                    if (!_cells.TryGetValue(cell, out var list))
                    {
                        list = new List<int>(4);
                        _cells[cell] = list;
                    }

                    list.Add(i);
                }
            }
        }
    }

    /// <summary>
    /// Queries wall segment indices near a position within the given radius.
    /// Uses _seen HashSet for deduplication since wall segments may be stored in multiple cells.
    /// </summary>
    public void Query(Vector2 position, float radius, List<int> results)
    {
        results.Clear();
        _seen.Clear();
        var min = CellOf(position - Vector2.one * radius);
        var max = CellOf(position + Vector2.one * radius);

        for (var y = min.y; y <= max.y; y++)
        {
            for (var x = min.x; x <= max.x; x++)
            {
                if (_cells.TryGetValue(new Vector2Int(x, y), out var list))
                {
                    foreach (var idx in list)
                    {
                        if (_seen.Add(idx))
                            results.Add(idx);
                    }
                }
            }
        }
    }

    Vector2Int CellOf(Vector2 p) =>
        new(Mathf.FloorToInt(p.x / _cellSize), Mathf.FloorToInt(p.y / _cellSize));
}
