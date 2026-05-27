using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Builds SocialForceWallSegments from road geometry.
///
/// Phase 1: Edge walls (2 per road) clipped at junction circles.
/// Phase 1.5: Intersecting walls from different edges trimmed at crossing points.
/// Phase 2: Junction polygon walls connecting clipped endpoints around each node.
///          Gaps between left/right walls of the same edge remain open — those are road entrances.
/// </summary>
public static class WalkBoundaryBuilder
{
    public static void Build(
        IReadOnlyList<WalkSegment> segments,
        float roadHalfWidth,
        Dictionary<int, Vector3> nodeWorldPositions,
        List<SocialForceWallSegment> allWalls,
        Dictionary<(int min, int max), (int leftIdx, int rightIdx)> edgeWallMap,
        Dictionary<int, List<int>> junctionWallMap)
    {
        allWalls.Clear();
        edgeWallMap.Clear();
        junctionWallMap.Clear();

        var processedEdges = new HashSet<(int min, int max)>();
        var junctionR = roadHalfWidth;

        // ── Phase 1: Edge walls ──
        foreach (var seg in segments)
        {
            var key = seg.FromNode < seg.ToNode ? (seg.FromNode, seg.ToNode) : (seg.ToNode, seg.FromNode);
            if (!processedEdges.Add(key)) continue;

            var minKey = Mathf.Min(key.Item1, key.Item2);
            var maxKey = Mathf.Max(key.Item1, key.Item2);

            if (!nodeWorldPositions.TryGetValue(minKey, out var npA) ||
                !nodeWorldPositions.TryGetValue(maxKey, out var npB)) continue;

            var a = ToXZ(npA);
            var b = ToXZ(npB);
            var d = b - a;
            if (d.sqrMagnitude < 1e-6f) continue;

            var fwd = d.normalized;
            var left = new Vector2(-fwd.y, fwd.x);
            var right = -left;

            var la = a + left * junctionR;
            var lb = b + left * junctionR;
            var ra = a + right * junctionR;
            var rb = b + right * junctionR;

            var li = allWalls.Count;
            allWalls.Add(new SocialForceWallSegment(la, lb, -left, minKey, maxKey));
            var ri = allWalls.Count;
            allWalls.Add(new SocialForceWallSegment(ra, rb, -right, minKey, maxKey));

            edgeWallMap[key] = (li, ri);
        }

        // ── Phase 1.5: Clip crossing walls ──
        foreach (var kv in nodeWorldPositions)
        {
            var nodeId = kv.Key;
            var center = ToXZ(kv.Value);

            var incident = new List<int>();
            foreach (var seg in segments)
            {
                if (seg.FromNode != nodeId && seg.ToNode != nodeId) continue;
                var k = seg.FromNode < seg.ToNode ? (seg.FromNode, seg.ToNode) : (seg.ToNode, seg.FromNode);
                if (!edgeWallMap.TryGetValue(k, out var pair)) continue;
                if (pair.leftIdx >= 0) incident.Add(pair.leftIdx);
                if (pair.rightIdx >= 0) incident.Add(pair.rightIdx);
            }

            var clipCount = 0;
            for (var i = 0; i < incident.Count; i++)
            {
                for (var j = i + 1; j < incident.Count; j++)
                {
                    var wi = incident[i];
                    var wj = incident[j];
                    if (wi < 0 || wi >= allWalls.Count || wj < 0 || wj >= allWalls.Count) continue;
                    if (SameEdge(allWalls[wi], allWalls[wj])) continue;

                    if (SegmentsIntersect(allWalls[wi].A, allWalls[wi].B,
                                          allWalls[wj].A, allWalls[wj].B, out var pt))
                    {
                        ClipJunctionEnd(ref allWalls, wi, center, pt);
                        ClipJunctionEnd(ref allWalls, wj, center, pt);
                        clipCount++;
                    }
                }
            }
            Debug.Log($"[WalkBoundary] Node {nodeId}: {incident.Count} walls, {clipCount} clips");
        }

        // ── Phase 2: Junction polygon walls ──
        // Group wall endpoints by position, keeping ALL edge keys at each point.
        // Connect consecutive groups that DON'T share any edge key.
        // Groups that share an edge = road entrance → no wall needed.
        foreach (var kv in nodeWorldPositions)
        {
            var nodeId = kv.Key;
            var center = ToXZ(kv.Value);

            // Collect junction-side endpoints with their edge keys.
            var raw = new List<(Vector2 pt, int emin, int emax)>();
            foreach (var seg in segments)
            {
                if (seg.FromNode != nodeId && seg.ToNode != nodeId) continue;
                var k = seg.FromNode < seg.ToNode ? (seg.FromNode, seg.ToNode) : (seg.ToNode, seg.FromNode);
                if (!edgeWallMap.TryGetValue(k, out var pair)) continue;
                CollectJunctionEnd(allWalls, pair.leftIdx, center, raw);
                CollectJunctionEnd(allWalls, pair.rightIdx, center, raw);
            }

            // Group by position.
            var groups = GroupByPosition(raw);
            if (groups.Count < 2) continue;

            // Sort groups by angle around center.
            groups.Sort((a, b) =>
            {
                var angA = Mathf.Atan2(a.point.y - center.y, a.point.x - center.x);
                var angB = Mathf.Atan2(b.point.y - center.y, b.point.x - center.x);
                return angA.CompareTo(angB);
            });

            // Connect consecutive groups: wall only if they don't share any edge.
            var jw = new List<int>();
            for (var i = 0; i < groups.Count; i++)
            {
                var next = (i + 1) % groups.Count;
                var g1 = groups[i];
                var g2 = groups[next];

                if (SharesAnyEdge(g1, g2)) continue; // road entrance gap

                var p1 = g1.point;
                var p2 = g2.point;
                if ((p1 - p2).sqrMagnitude < 1e-6f) continue;

                var mid = (p1 + p2) * 0.5f;
                var n = (center - mid);
                var normal = n.sqrMagnitude > 1e-6f ? n.normalized : Vector2.up;

                var idx = allWalls.Count;
                allWalls.Add(new SocialForceWallSegment(p1, p2, normal, nodeId, nodeId));
                jw.Add(idx);
            }

            if (jw.Count > 0)
                junctionWallMap[nodeId] = jw;
        }
    }

    struct PointGroup
    {
        public Vector2 point;
        public HashSet<(int min, int max)> edges;
    }

    static bool SameEdge(SocialForceWallSegment a, SocialForceWallSegment b) =>
        a.EdgeMinNode == b.EdgeMinNode && a.EdgeMaxNode == b.EdgeMaxNode;

    static void CollectJunctionEnd(List<SocialForceWallSegment> walls, int idx,
        Vector2 center, List<(Vector2 pt, int emin, int emax)> pts)
    {
        if (idx < 0 || idx >= walls.Count) return;
        var w = walls[idx];
        var pt = (w.A - center).sqrMagnitude <= (w.B - center).sqrMagnitude ? w.A : w.B;
        pts.Add((pt, w.EdgeMinNode, w.EdgeMaxNode));
    }

    static List<PointGroup> GroupByPosition(List<(Vector2 pt, int emin, int emax)> raw)
    {
        var result = new List<PointGroup>();
        const float eps = 1e-3f;
        foreach (var r in raw)
        {
            var found = false;
            for (var i = 0; i < result.Count; i++)
            {
                if ((result[i].point - r.pt).sqrMagnitude < eps * eps)
                {
                    result[i].edges.Add((r.emin, r.emax));
                    found = true;
                    break;
                }
            }
            if (!found)
            {
                var g = new PointGroup { point = r.pt, edges = new HashSet<(int, int)>() };
                g.edges.Add((r.emin, r.emax));
                result.Add(g);
            }
        }
        return result;
    }

    static bool SharesAnyEdge(PointGroup a, PointGroup b)
    {
        foreach (var e in a.edges)
            if (b.edges.Contains(e))
                return true;
        return false;
    }

    static List<(Vector2 pt, int edgeMin, int edgeMax)> Deduplicate(
        List<(Vector2 pt, int edgeMin, int edgeMax)> pts)
    {
        var result = new List<(Vector2, int, int)>();
        const float eps = 1e-3f;
        foreach (var p in pts)
        {
            var dup = false;
            for (var i = 0; i < result.Count; i++)
            {
                if ((result[i].Item1 - p.pt).sqrMagnitude < eps * eps)
                {
                    dup = true;
                    break;
                }
            }
            if (!dup) result.Add(p);
        }
        return result;
    }

    static bool SegmentsIntersect(Vector2 a1, Vector2 a2, Vector2 b1, Vector2 b2, out Vector2 pt)
    {
        pt = Vector2.zero;
        var d1 = a2 - a1;
        var d2 = b2 - b1;
        var cross = d1.x * d2.y - d1.y * d2.x;
        if (Mathf.Abs(cross) < 1e-8f) return false;

        var t = ((b1.x - a1.x) * d2.y - (b1.y - a1.y) * d2.x) / cross;
        var u = ((b1.x - a1.x) * d1.y - (b1.y - a1.y) * d1.x) / cross;

        if (t < 0f || t > 1f || u < 0f || u > 1f) return false;

        pt = a1 + d1 * t;
        return true;
    }

    static void ClipJunctionEnd(ref List<SocialForceWallSegment> walls, int idx,
        Vector2 center, Vector2 pt)
    {
        var w = walls[idx];
        var dA = (w.A - center).sqrMagnitude;
        var dB = (w.B - center).sqrMagnitude;

        if (dA <= dB)
            walls[idx] = new SocialForceWallSegment(pt, w.B, w.InwardNormal, w.EdgeMinNode, w.EdgeMaxNode);
        else
            walls[idx] = new SocialForceWallSegment(w.A, pt, w.InwardNormal, w.EdgeMinNode, w.EdgeMaxNode);
    }

    static Vector2 ToXZ(Vector3 p) => new(p.x, p.z);
}
