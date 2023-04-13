using UnityEngine;
using System.Collections.Generic;


public class VectorUtilities
{

    /// <summary>
    /// Returns the clockwise dot-product of two vectors:
    /// </summary>
    /// <example>
    ///   with up-vector as reference-vector:
    ///        4 0
    ///         ↑
    ///     3 ← x → 1
    ///         ↓
    ///         2
    /// </example>
    public static float ClockwiseDotProductToReferenceVec(Vector3 referenceVec, Vector3 vector)
    {
        float dot = Vector3.Dot(referenceVec.normalized, vector.normalized);
        float dotSide = Vector3.Dot(Vector3.Cross(referenceVec.normalized, Vector3.down.normalized).normalized, vector.normalized);
        return dotSide >= 0 ? 1 - dot : 3 + dot;
    }
    public static Vector3 GetXZPlaneIntersection(Vector3 start, Vector3 direction, Vector3 otherStart, Vector3 otherDirection)
    {
        // From https://blog.dakwamine.fr/?p=1943
        float denominator = otherDirection.x * direction.z - otherDirection.z * direction.x;
        if (denominator == 0)
        {
            throw new System.Exception("No intersection found!\nEither a direction is zero or the two directions are parallel!");
        }
        float factor = ((start.x - otherStart.x) * direction.z - (start.z - otherStart.z) * direction.x) / denominator;

        return new Vector3(
            otherStart.x + otherDirection.x * factor,
            0,
            otherStart.z + otherDirection.z * factor
        );
    }

    /// <summary>
    /// Returns wether Vector a and Vector b are on a line (=[anti-]parallel).
    /// </summary>
    public static bool IsOnOneLine(Vector3 a, Vector3 b)
    {
        return VectorUtilities.SimpleCross(a, b) == 0f;
    }


    /// <summary>
    /// Returns wether Vector a is on the right side (clockwise) of Vector b.
    /// </summary>
    public static bool IsConvex(Vector3 a, Vector3 b)
    {
        return VectorUtilities.SimpleCross(a, b) > 0f;
    }
    private static float SimpleCross(Vector3 a, Vector3 b)
    {
        return a.x * b.z - a.z * b.x;
    }

    /// <summary>
    /// Returns wether 'other' is inside the triangle p_prev -> p -> p_next (in clockwise order).
    /// Points on an edge are not inside a triangle!
    /// </summary>
    public static bool IsPointInTriangle(Vector3 other, Vector3 p_prev, Vector3 p, Vector3 p_next)
    {
        Vector3 to_prev = p_prev - p_next;
        Vector3 to_p = p - p_prev;
        Vector3 to_next = p_next - p;

        Vector3 from_prev = other - p_prev;
        Vector3 from_p = other - p;
        Vector3 from_next = other - p_next;

        bool out1 = !VectorUtilities.IsConvex(from_prev, to_p);
        bool out2 = !VectorUtilities.IsConvex(from_p, to_next);
        bool out3 = !VectorUtilities.IsConvex(from_next, to_prev);

        return !(out1 || out2 || out3);
    }


    public static List<int> PointsInTriangle(List<Vector3> vertices, List<int> vertexOrder, Vector3 p_prev, Vector3 p, Vector3 p_next)
    {
        List<int> inTriangle = new List<int>();
        for (int i = 0; i < vertexOrder.Count; i++)
        {
            if (VectorUtilities.IsPointInTriangle(vertices[vertexOrder[i]], p_prev, p, p_next))
            {
                inTriangle.Add(i);
            }
        }
        return inTriangle;
    }

    public static Vector3 VectorInfiniteLineToPoint(Vector3 startPosition, Vector3 direction, Vector3 point)
    {
        Vector3 vectorToProject = point - startPosition;
        Vector3 projected = startPosition + Vector3.Project(vectorToProject, direction.normalized);
        return point - projected;
    }

    public static float DistancePointToInfiniteLine(Vector3 startPosition, Vector3 direction, Vector3 point)
    {
        Vector3 vectorToProject = point - startPosition;
        Vector3 projected = startPosition + Vector3.Project(vectorToProject, direction.normalized);
        return Vector3.Distance(projected, point);
    }

    public static float DistancePointToLineSegment(Vector3 startPosition, Vector3 endPosition, Vector3 point)
    {
        Vector3 vectorToProject = point - startPosition;
        Vector3 projected = startPosition + Vector3.Project(vectorToProject, (endPosition - startPosition).normalized);
        return Mathf.Min(Vector3.Distance(projected, point), Mathf.Min(Vector3.Distance(startPosition, point), Vector3.Distance(endPosition, point)));
    }

}
