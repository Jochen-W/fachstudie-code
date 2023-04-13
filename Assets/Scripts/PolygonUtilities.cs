using UnityEngine;
using System.Collections.Generic;


public class PolygonUtilities
{
    /// <summary>
    /// Returns wether the given vertices are in a clockwise order/winding direction.
    /// </summary>
    public static bool IsInClockwiseWindingOrder(List<Vector3> vertices, List<int> vertexOrder)
    {
        // tl = top-left, br = bottom-right
        (Vector3 tl, Vector3 br) = PolygonUtilities.CalculateBbox(vertices);
        Vector3 ray_origin = new Vector3(tl.x + (br.x - tl.x) * 0.5f, 0, tl.z + 1);
        // ray_direction = (0,0, -1) = "down on 2D plane"
        Vector3 hit_pos = Vector3.positiveInfinity;
        int hit_vertex = -1;
        // get the closest ray intersection
        for (int i = 0; i < vertexOrder.Count; i++)
        {
            Vector3 cur = vertices[vertexOrder[i]];
            Vector3 next = vertices[vertexOrder[(i + 1) % vertexOrder.Count]];
            if ((cur.x < ray_origin.x && next.x > ray_origin.x) || (cur.x > ray_origin.x && next.x < ray_origin.x))
            {
                // "hit" (theoretically, can be obscured)
                Vector3 intersection = VectorUtilities.GetXZPlaneIntersection(ray_origin, Vector3.back, cur, next - cur);
                if (Vector3.Distance(ray_origin, intersection) < Vector3.Distance(ray_origin, hit_pos))
                {
                    hit_pos = intersection;
                    hit_vertex = i;
                }
            }
            else if (cur.x == ray_origin.x || next.x == ray_origin.x)
            {
                Debug.Log("Direct Hit -> Full reset");
                // reset origin and start over again
                System.Random random = new System.Random();
                ray_origin = new Vector3(tl.x + (br.x - tl.x) * ((float)random.NextDouble() * 0.5f + 0.25f), 0, tl.z + 1);
                hit_pos = Vector3.positiveInfinity;
                hit_vertex = -1;
                i = -1;  // since i++ at the end of the loop

            }
        }
        // if the hit_vertex is on the right of the ray, it must be in clockwise order
        return VectorUtilities.IsConvex(vertices[vertexOrder[hit_vertex]] - ray_origin, Vector3.back);
    }

    /// <summary>
    /// Returns a list of all indices who are on one line (with the previous and the next vertex).
    /// </summary>
    public static HashSet<int> AdjacentVerticesOnOneLine(List<Vector3> vertices, List<int> vertexOrder)
    {
        // TODO: come up with more sophisticated math/algorithm
        HashSet<int> onOneLine = new HashSet<int>();
        for (int i = 0; i < vertexOrder.Count; i++)
        {
            Vector3 p_prev = vertices[vertexOrder[(i - 1 + vertexOrder.Count) % vertexOrder.Count]];
            Vector3 p = vertices[vertexOrder[i]];
            Vector3 p_next = vertices[vertexOrder[(i + 1) % vertexOrder.Count]];

            Vector3 to_prev = p_prev - p;
            Vector3 to_next = p_next - p;

            if (VectorUtilities.IsOnOneLine(to_prev, to_next))
            {
                onOneLine.Add(i);
            }
            else
            {
                // special case: where A-------C-B is not on one line (angle-wise),
                // but C is so close to B that it visually is on one line
                if (VectorUtilities.DistancePointToLineSegment(p_prev, p, p_next) < 0.0001)
                {
                    onOneLine.Add((i + 1) % vertexOrder.Count);
                }
            }
        }
        return onOneLine;
    }

    /// <summary>
    /// Returns the bounding-box (top-left, bottom-right) of a given list of vertices.
    /// </summary>
    private static (Vector3, Vector3) CalculateBbox(List<Vector3> vertices)
    {
        int top = 0;
        int right = 0;
        int bottom = 0;
        int left = 0;

        for (int i = 1; i < vertices.Count; i++)
        {
            Vector3 p = vertices[i];
            if (p.z > vertices[top].z)
            {
                top = i;
            }
            if (p.x > vertices[right].x)
            {
                right = i;
            }
            if (p.z < vertices[bottom].z)
            {
                bottom = i;
            }
            if (p.x < vertices[left].x)
            {
                left = i;
            }
        }

        return (
            new Vector3(vertices[left].x, 0, vertices[top].z),
            new Vector3(vertices[right].x, 0, vertices[bottom].z)
        );
    }

    public static int GetRightmostIndex(List<Vector3> vertices)
    {
        int right = 0;
        for (int i = 1; i < vertices.Count; i++)
        {
            Vector3 p = vertices[i];
            if (p.x > vertices[right].x)
            {
                right = i;
            }
        }
        return right;
    }

    public static List<(Vector3, int)> FindAllRayHitsInXZPlane(Vector3 rayStart, Vector3 rayDirection, List<Vector3> vertices, List<int> vertexOrder)
    {
        List<(Vector3, int)> hits = new List<(Vector3, int)>();
        Vector3 normalOfHalfPlane = Vector3.Cross(rayDirection, Vector3.up);

        for (int i = 0; i < vertexOrder.Count; i++)
        {
            Vector3 curr = vertices[vertexOrder[i]] - rayStart;
            Vector3 next = vertices[vertexOrder[(i + 1) % vertexOrder.Count]] - rayStart;
            float angleCurr = Vector3.Dot(normalOfHalfPlane, curr);
            float angleNext = Vector3.Dot(normalOfHalfPlane, next);

            if (Mathf.Sign(angleCurr) != Mathf.Sign(angleNext))
            {
                Vector3 hitPos = VectorUtilities.GetXZPlaneIntersection(rayStart, rayDirection, vertices[vertexOrder[i]], next - curr);
                hits.Add((hitPos, i));
            }
        }
        return hits;
    }

    public static List<(Vector3, int)> FindAllRayHitsInRangeInXZPlane(Vector3 rayStart, Vector3 rayEnd, List<Vector3> vertices, List<int> vertexOrder)
    {
        Vector3 rayDirection = (rayEnd - rayStart).normalized;

        List<(Vector3, int)> hits = new List<(Vector3, int)>();
        Vector3 normalOfHalfPlane = Vector3.Cross(rayDirection, Vector3.up);

        for (int i = 0; i < vertexOrder.Count; i++)
        {
            // points equal to rayStart/End?
            if (vertices[vertexOrder[i]] == rayStart || vertices[vertexOrder[i]] == rayEnd)
            {
                hits.Add((vertices[vertexOrder[i]], i));
                continue;
            }
            if (vertices[vertexOrder[(i + 1) % vertexOrder.Count]] == rayStart || vertices[vertexOrder[(i + 1) % vertexOrder.Count]] == rayEnd)
            {
                hits.Add((vertices[vertexOrder[(i + 1) % vertexOrder.Count]], i));
                continue;
            }

            Vector3 curr = vertices[vertexOrder[i]] - rayStart;
            Vector3 next = vertices[vertexOrder[(i + 1) % vertexOrder.Count]] - rayStart;
            // is it even possible?
            float angleCurr = Vector3.Dot(normalOfHalfPlane, curr);
            float angleNext = Vector3.Dot(normalOfHalfPlane, next);
            if (Mathf.Sign(angleCurr) != Mathf.Sign(angleNext))
            {
                // calculate hit position
                Vector3 hitPos = VectorUtilities.GetXZPlaneIntersection(rayStart, rayDirection, vertices[vertexOrder[i]], next - curr);
                // dot < 0 is enough to be sure they point in opposite direction
                if (Vector3.Dot(hitPos - rayStart, hitPos - rayEnd) < 0)  // is in between S (rayStart) and E (rayEnd); or equal to one of them
                {
                    hits.Add((hitPos, i));
                }
            }
        }
        return hits;
    }

    public static float CalculateAreaOfPolygon(List<Vector3> vertices, List<int> vertexOrder)
    {
        // from https://www.topcoder.com/thrive/articles/Geometry%20Concepts%20part%201:%20Basic%20Concepts#PolygonArea
        float area = 0.0f;
        Vector3 a = vertices[vertexOrder[0]];

        for (int i = 1; i < vertexOrder.Count - 1; i++)
        {
            // trivial triangulation
            Vector3 b = vertices[vertexOrder[i]];
            Vector3 c = vertices[vertexOrder[i + 1]];
            // can't use Vector3.Cross(b-a, c-a).magnitude, since we wouldn't get the sign
            area += (b - a).x * (c - a).y - (c - a).x * (b - a).y;
        }
        return Mathf.Abs(area) * 0.5f;
    }

    public static bool PointInPolygon(Vector3 point, List<Vector3> vertices, List<int> vertexOrder)
    {
        Vector3 rayDir = (point - vertices[vertexOrder[0]]).normalized;
        return PolygonUtilities.FindAllRayHitsInRangeInXZPlane(point - rayDir * 100, point, vertices, vertexOrder).Count % 2 == 1;
    }
}
