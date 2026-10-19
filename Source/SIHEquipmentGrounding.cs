using Unity.Collections;
using UnityEngine;

/// <summary>Measured plane-boundary containment with clearance for the equipment base.</summary>
public static class SIHEquipmentGrounding
{
    public static bool ContainsFootprint(NativeArray<Vector2> boundary, Vector2 point, float radius)
    {
        if (!boundary.IsCreated || boundary.Length < 3 || radius <= 0 || float.IsInfinity(radius)) return false;
        bool inside = false;
        for (int i = 0, j = boundary.Length - 1; i < boundary.Length; j = i++)
        {
            Vector2 a = boundary[j], b = boundary[i], edge = b - a;
            float t = edge.sqrMagnitude > .000001f ? Mathf.Clamp01(Vector2.Dot(point-a, edge) / edge.sqrMagnitude) : 0;
            if ((point - (a + edge*t)).sqrMagnitude < radius*radius) return false;
            if ((a.y > point.y) != (b.y > point.y) &&
                point.x < (b.x-a.x)*(point.y-a.y)/(b.y-a.y)+a.x) inside = !inside;
        }
        return inside;
    }
}
