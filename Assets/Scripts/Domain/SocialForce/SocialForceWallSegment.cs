using UnityEngine;

/// <summary>
/// A directed line segment representing a walkable-area boundary (wall, road edge, etc.).
/// All coordinates are in world XZ space.
///
/// Stores precomputed inward normal (points toward walkable area) and tangent (along the wall).
/// These are used by SocialForceSimulator to compute wall repulsion, body compression,
/// and sliding friction forces.
/// </summary>
public readonly struct SocialForceWallSegment
{
    /// <summary>Start point of the wall segment in XZ space.</summary>
    public readonly Vector2 A;

    /// <summary>End point of the wall segment in XZ space.</summary>
    public readonly Vector2 B;

    /// <summary>Unit normal vector pointing from the wall toward the walkable interior.</summary>
    public readonly Vector2 InwardNormal;

    /// <summary>Unit tangent vector along the wall direction (B - A normalized).</summary>
    public readonly Vector2 Tangent;

    /// <summary>Edge key: (min(fromNode, toNode), max(fromNode, toNode)) of the road this wall belongs to.</summary>
    public readonly int EdgeMinNode;
    public readonly int EdgeMaxNode;

    public SocialForceWallSegment(Vector2 a, Vector2 b, Vector2 inwardNormal,
        int edgeMinNode = 0, int edgeMaxNode = 0)
    {
        A = a;
        B = b;
        InwardNormal = inwardNormal.sqrMagnitude > 1e-6f ? inwardNormal.normalized : Vector2.up;
        var d = B - A;
        Tangent = d.sqrMagnitude > 1e-6f ? d.normalized : new Vector2(-InwardNormal.y, InwardNormal.x);
        EdgeMinNode = edgeMinNode;
        EdgeMaxNode = edgeMaxNode;
    }

    /// <summary>
    /// Returns the closest point on this line segment to the given point p.
    /// Clamps the projection parameter to [0, 1] so the result is always on the segment.
    /// </summary>
    public Vector2 ClosestPoint(Vector2 p)
    {
        var ab = B - A;
        var lenSq = ab.sqrMagnitude;
        if (lenSq < 1e-6f)
            return A;

        var t = Mathf.Clamp01(Vector2.Dot(p - A, ab) / lenSq);
        return A + ab * t;
    }
}
