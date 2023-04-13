using System;
using UnityEngine;
using System.Linq;
using System.Collections.Generic;


public class Triangulator
{
    /// <summary>
    /// Makes a dict out of the given vertexOrder (where the index is the key and the value at that index is the corresponding value).
    /// </summary>
    /// <example>
    ///   [0,3,9,10]
    /// Returns {0: 0, 1: 3, 2: 9, 3: 10}
    /// </example>
    private static Dictionary<int, int> MakeVertexOrderToDict(List<int> originalVertexOrder)
    {
        return new Dictionary<int, int>(
            Enumerable.Range(0, originalVertexOrder.Count).ToList().Zip(originalVertexOrder, (first, second) => KeyValuePair.Create(first, second))
        );
    }

    /// <summary>
    /// Makes a dict out of the given vertexOrder(-length), where for each edge-pair=[from, x][x, to] the dict entry {x: {from: to}} follows.
    /// </summary>
    /// <example>
    ///   4
    /// Returns {0: {3: 1}, 1: {0: 2}, 2: {1: 3}, 3: {2: 0}}
    /// </example>
    private static Dictionary<int, Dictionary<int, int>> MakeVertexOrderToJumpDict(int vertexOrderLength)
    {
        Dictionary<int, Dictionary<int, int>> dict = new Dictionary<int, Dictionary<int, int>>();
        for (int i = 0; i < vertexOrderLength; i++)
        {
            dict[i] = new Dictionary<int, int>();
            dict[i][(i - 1 + vertexOrderLength) % vertexOrderLength] = (i + 1) % vertexOrderLength;
        }
        return dict;
    }

    /// <summary>
    /// Makes a dictionary out of a jump-pair list. Also for the special cases e.g. S, E.
    /// </summary>
    /// <example>
    ///     30     12---11      1
    ///      \     |     |     /
    ///       S----13---10----E
    ///      /     |     |     \
    ///     20     |     |      6
    ///         ---14    9---
    ///    allHits = [-1, 13, 10, -2][-1,30][-1,20][-2,1][-2,6] (indices in vertexOrder of hits, negative are vertices that are not in the original vertexOrder like S and E)
    ///    vertexIndices = {1: 1, 6: 6, 9: 9, 10: 31, 11: 11, 12: 12, 13: 32, ...}
    /// Returns {13: {12: -1, -1: 14}, 10: {9: -2, -2: 11},...  -1: {13: 30, 20: 13, 30: 20}, -2: {1: 10, 6: 1, 10: 6} }
    /// </example>
    private static Dictionary<int, Dictionary<int, int>> MakeJumpDictionary(List<(Vector3, int)> allHits, int vertexOrderLength, Dictionary<int, int> mergedVertexOrder, List<Vector3> vertices)
    {
        Dictionary<int, Dictionary<int, int>> jumpDict = Triangulator.MakeVertexOrderToJumpDict(vertexOrderLength);
        Dictionary<int, List<int>> specialCases = new Dictionary<int, List<int>>();
        for (int i = 0; i < allHits.Count; i++)
        {
            int cur = allHits[i].Item2;
            int jumpPartner = allHits[i % 2 == 0 ? i + 1 : i - 1].Item2;

            if (!jumpDict.ContainsKey(cur))
            {
                jumpDict.Add(cur, new Dictionary<int, int>());
            }
            List<KeyValuePair<int, int>> closesPrev = jumpDict[cur].AsEnumerable().ToList();
            int prev = jumpPartner;
            int next = jumpPartner;
            if (closesPrev.Count > 0)
            {
                closesPrev.Sort((a, b) =>
                    {
                        Vector3 toJumpPartner = vertices[mergedVertexOrder[jumpPartner]] - vertices[mergedVertexOrder[cur]];
                        float angleA = VectorUtilities.ClockwiseDotProductToReferenceVec(toJumpPartner, vertices[mergedVertexOrder[a.Key]] - vertices[mergedVertexOrder[cur]]);
                        float angleB = VectorUtilities.ClockwiseDotProductToReferenceVec(toJumpPartner, vertices[mergedVertexOrder[b.Key]] - vertices[mergedVertexOrder[cur]]);
                        return angleA.CompareTo(angleB);
                    }
                );
                prev = closesPrev[0].Key;
                next = closesPrev[0].Value;
            }
            jumpDict[cur][prev] = jumpPartner;
            jumpDict[cur][jumpPartner] = next;
        }

        return jumpDict;
    }


    /// <summary>
    /// Combines the two given polygons (outer must be in clockwise order and inner in counter-clockwise order).
    /// Returns (combinedVertices, combinedVertexOrder)
    /// </summary>
    public static (List<Vector3>, List<int>) CombineVertices(List<Vector3> outerVertices, List<int> outerVertexOrder, List<Vector3> innerVertices, List<int> innerVertexOrder)
    {
        List<int> combinedOrder = new List<int>();
        int rightmostIndex = PolygonUtilities.GetRightmostIndex(innerVertices);
        // cast ray to the right
        Vector3 rayStart = innerVertices[rightmostIndex];
        Vector3 rayHitPos = Vector3.positiveInfinity;
        int upperIndexFromHit = -1;
        int lowerIndexFromHit = -1;
        for (int i = 0; i < outerVertexOrder.Count; i++)
        {
            Vector3 upper = outerVertices[outerVertexOrder[i]];
            Vector3 lower = outerVertices[outerVertexOrder[(i + 1) % outerVertexOrder.Count]];
            if (upper.z > rayStart.z && lower.z < rayStart.z && (upper.x > rayStart.x || lower.x > rayStart.x))
            {
                // "hit" (theoretically, can be obscured)
                Vector3 hit = VectorUtilities.GetXZPlaneIntersection(rayStart, Vector3.right, upper, lower - upper);
                if (Vector3.Distance(rayStart, hit) < Vector3.Distance(rayStart, rayHitPos))
                {
                    rayHitPos = hit;
                    upperIndexFromHit = i;
                    lowerIndexFromHit = (i + 1) % outerVertexOrder.Count;
                }
            }
        }
        if (upperIndexFromHit == -1 || lowerIndexFromHit == -1)
        {
            // hole data is corrupted -> skip hole
            throw new Exception("Hole data is corrupted!");
        }
        // find closest point in triangle (ray_start, ray_hit, outer_vertices[lowerIndexFromHit])
        // or in triangle (ray_start, outer_vertices[upperIndexFromHit], ray_hit) if outer_vertices[lowerIndexFromHit] is left of ray-start
        List<int> inTraingle = new List<int>();
        int closest = -1;
        if (outerVertices[outerVertexOrder[lowerIndexFromHit]].x >= rayStart.x)
        {
            inTraingle = VectorUtilities.PointsInTriangle(outerVertices, outerVertexOrder, rayStart, rayHitPos, outerVertices[outerVertexOrder[lowerIndexFromHit]]);
            // lowerIndexFromHit used as fallback-value
            closest = lowerIndexFromHit;
        }
        else
        {
            inTraingle = VectorUtilities.PointsInTriangle(outerVertices, outerVertexOrder, rayStart, outerVertices[outerVertexOrder[upperIndexFromHit]], rayHitPos);
            // upperIndexFromHit used as fallback-value
            closest = upperIndexFromHit;
        }
        foreach (int index in inTraingle)
        {
            if (Vector3.Distance(rayStart, outerVertices[outerVertexOrder[index]]) < Vector3.Distance(rayStart, outerVertices[outerVertexOrder[closest]]))
            {
                closest = index;
            }
        }
        combinedOrder.AddRange(outerVertexOrder.GetRange(0, closest + 1));
        combinedOrder.AddRange(innerVertexOrder.GetRange(rightmostIndex, innerVertexOrder.Count - rightmostIndex).Select(x => x + outerVertices.Count));
        combinedOrder.AddRange(innerVertexOrder.GetRange(0, rightmostIndex + 1).Select(x => x + outerVertices.Count));
        combinedOrder.AddRange(outerVertexOrder.GetRange(closest, outerVertexOrder.Count - closest));

        List<Vector3> combinedVertices = outerVertices.Concat(innerVertices).ToList();
        return (combinedVertices, combinedOrder);
    }


    private static Func<(Vector3, int), (Vector3, int), int> GetSortFunc(List<Vector3> vertices, List<int> vertexOrder, Vector3 start)
    {
        // TODO: problem by direct (cut-)hit!!!
        // sort by distance to start-position
        return (a, b) =>
        {
            int result = (a.Item1 - start).magnitude.CompareTo((b.Item1 - start).magnitude);
            if (result == 0)
            {
                // same hit-position -> go clockwise to find first

                // all vectors point away from the hit-position (and are normalized)
                Vector3 toStart = (start - a.Item1).normalized;
                Vector3 toOtherA = a.Item1 == vertices[vertexOrder[a.Item2]] ? (vertices[vertexOrder[(a.Item2 + 1) % vertexOrder.Count]] - a.Item1).normalized : (vertices[vertexOrder[a.Item2]] - a.Item1).normalized;
                Vector3 toOtherB = a.Item1 == vertices[vertexOrder[b.Item2]] ? (vertices[vertexOrder[(b.Item2 + 1) % vertexOrder.Count]] - a.Item1).normalized : (vertices[vertexOrder[b.Item2]] - a.Item1).normalized;
                // calculate the clockwise "angle"
                float dotA = VectorUtilities.ClockwiseDotProductToReferenceVec(toStart, toOtherA);
                float dotB = VectorUtilities.ClockwiseDotProductToReferenceVec(toStart, toOtherB);
                // the one first (if you go clockwise) should be the first
                result = dotA.CompareTo(dotB);

                if (result == 0)  // same line (->same other)
                {
                    // edge pointing to the target is before the edge pointing away
                    dotA = a.Item1 == vertices[vertexOrder[a.Item2]] ? 1 : 0;
                    dotB = b.Item1 == vertices[vertexOrder[b.Item2]] ? 1 : 0;
                    result = dotA.CompareTo(dotB);
                }
            }
            return result;
        };
    }

    private static Func<(Vector3, int, (int, int), int), (Vector3, int, (int, int), int), int> GetIndexSortFunc(List<Vector3> vertices, List<int> vertexOrder)
    {
        // sort by indexInVertexOrder
        return (a, b) =>
        {
            int result = a.Item2.CompareTo(b.Item2);
            if (result == 0)
            {
                result = (a.Item1 - vertices[vertexOrder[a.Item2]]).magnitude.CompareTo(
                    (b.Item1 - vertices[vertexOrder[b.Item2]]).magnitude
                );
            }
            return result;
        };
    }

    /// <summary>
    /// Inserts a given hit (= Vec3 + oldIndex) into the vertexOrder (and vertices).
    /// Returns (new index in vertexOrder, wether it was a new vertex/insertion)
    /// </summary>
    private static (int, bool) InsertHitVertex(ref List<Vector3> vertices, ref List<int> vertexOrder, Vector3 hitPosition, int oldOrderIndex, int insertionStartIndex)
    {
        int newIndex = -1;
        // check if hitPosition is a new vertex
        if (hitPosition == vertices[vertexOrder[oldOrderIndex]])
        {
            // hit position = pre (not new -> no new insertion into vertexOrder)
            return (oldOrderIndex, false);
        }
        if (hitPosition == vertices[vertexOrder[(oldOrderIndex + 1) % vertexOrder.Count]])
        {
            // hit position = next (not new -> no new insertion into vertexOrder)
            return ((oldOrderIndex + 1) % vertexOrder.Count, false);
        }

        // hit position in between (pre and next)
        newIndex = oldOrderIndex + 1;
        // is it a new vertex?
        for (int i = insertionStartIndex; i < vertices.Count; i++)
        {
            if (hitPosition == vertices[i])
            {
                // hit was already inserted (into vertices)
                vertexOrder.Insert(newIndex, i);
                return (newIndex, true);
            }
        }
        // it is a new vertex
        vertexOrder.Insert(newIndex, vertices.Count);
        vertices.Add(hitPosition);
        return (newIndex, true);
    }


    /// <summary>
    /// Makes several things:
    ///     1) sends rays from each start-end pair and adds the hit-positions into the vertexOrder (and the vertices)
    ///     2) adds the newVertices to vertices (and returns the new indices as dictionary, e.g. {-1: 20, -2: 21})
    ///     3) makes a jump-pair list out of all hits ([even, odd, even, odd, ...] where each even is paired with the next odd)
    /// Returns (jump-pair list, new vertices dictionary)
    /// </summary>
    public static (List<(Vector3, int)>, Dictionary<int, int>) AddRayHitsToPolygon(ref List<Vector3> vertices, ref List<int> vertexOrder, Dictionary<int, List<int>> rayStartToEnds, Dictionary<int, Vector3> newVertices)
    {
        Dictionary<(int, int), List<(Vector3, int, (int, int), int)>> rayHitLists = new Dictionary<(int, int), List<(Vector3, int, (int, int), int)>>();
        List<(Vector3, int, (int, int), int)> allHits = new List<(Vector3, int, (int, int), int)>();
        // find all hits with new edges (e.g. from S to E) and add them to allVertices and combinedVertexOrder
        // since we want to remember the new indices to use them later, we add a lookup (the zip); to do that:
        // (hitPos, prevIndexFromHit) -> (hitPos, prevIndexFromHit, (from, to), indexInOwnList)
        // after the insertion we have (hitPos, indexOfHitInCombinedVertexOrder, (-1, -1), -1)
        foreach ((int from, List<int> toList) in rayStartToEnds)
        {
            Vector3 fromVec = newVertices.ContainsKey(from) ? newVertices[from] : vertices[vertexOrder[from]];
            foreach (int to in toList)
            {
                Vector3 toVec = newVertices.ContainsKey(to) ? newVertices[to] : vertices[vertexOrder[to]];

                List<(Vector3, int)> tempList = PolygonUtilities.FindAllRayHitsInRangeInXZPlane(fromVec, toVec, vertices, vertexOrder);
                Func<(Vector3, int), (Vector3, int), int> sortFunc = Triangulator.GetSortFunc(vertices, vertexOrder, fromVec);
                tempList.Sort((a, b) => sortFunc(a, b));

                if (!newVertices.ContainsKey(to))
                {
                    // remove all equal end-hits expect the very last
                    (Vector3, int) last = tempList[tempList.Count - 1];
                    tempList.RemoveAll(x => x.Item1 == last.Item1);
                    tempList.Add(last);
                }

                rayHitLists[(from, to)] = tempList.Zip(Enumerable.Range(0, tempList.Count), (first, second) => (first.Item1, first.Item2, (from, to), second)).ToList();
                allHits.AddRange(rayHitLists[(from, to)]);
            }
        }

        // sort by indexInOrder
        Func<(Vector3, int, (int, int), int), (Vector3, int, (int, int), int), int> indexSortFunc = Triangulator.GetIndexSortFunc(vertices, vertexOrder);
        allHits.Sort((a, b) => indexSortFunc(a, b));

        // add all hits in right order (to vertices and to vertexOrder)
        int insertionCount = 0;
        int startOfInsertionIndex = vertices.Count;
        for (int i = 0; i < allHits.Count; i++)
        {
            (int newIndex, bool isNewInsertion) = Triangulator.InsertHitVertex(ref vertices, ref vertexOrder, allHits[i].Item1, allHits[i].Item2 + insertionCount, startOfInsertionIndex);
            if (isNewInsertion)
            {
                insertionCount++;
            }
            // update new indices in singular lists (-> to get (hitPos, indexOfHitInCombinedVertexOrder, (-1, -1), -1) )
            rayHitLists[allHits[i].Item3][allHits[i].Item4] = (allHits[i].Item1, newIndex, (-1, -1), -1);
        }

        // now we can add all new vertices
        Dictionary<int, int> specialCaseVertexIndices = new Dictionary<int, int>();
        foreach ((int key, Vector3 vertex) in newVertices.OrderByDescending((pair) => pair.Key))
        {
            specialCaseVertexIndices[key] = vertices.Count;
            vertices.Add(vertex);
        }

        allHits.Clear();
        // add (start and) end of each ray if needed (needed when start/end is a newVertex/wasn't in vertex order)
        foreach ((int from, List<int> toList) in rayStartToEnds)
        {
            foreach (int to in toList)
            {
                if (newVertices.ContainsKey(from))
                {
                    rayHitLists[(from, to)].Insert(0, (newVertices[from], from, (-1, -1), -1));
                }
                if (newVertices.ContainsKey(to))
                {
                    rayHitLists[(from, to)].Add((newVertices[to], to, (-1, -1), -1));
                }
                allHits.AddRange(rayHitLists[(from, to)]);
            }
        }

        return (allHits.ConvertAll(x => (x.Item1, x.Item2)), specialCaseVertexIndices);
    }



    /// <summary>
    /// Returns all (closed-loop) vertex-orders of all sub-polygons in the main polygon (that got cut by the main roof-edges).
    /// A main roof-edge is e.g. the top edge by a gabled-roof.
    /// </summary>
    /// <example>
    ///   Most simple example for hipped:
    ///     0--------1
    ///     | \    / |
    ///     |  S--E  |
    ///     | /    \ |
    ///     3--------2
    ///   specialCaseJumpDictionary = {S=-1: {E=-2: 0, 3: E=-2, 0: 3}, E=-2: {S=-1: 2, 1: S=-1, 2: 1}}
    ///   specialCaseVertexIndices = {S=-1: 4, E=-2: 5}
    /// Returns: [0,1,E,S], [1,2,E], [2,3,S,E], [3,0,S]
    /// </example>
    public static IEnumerable<List<int>> GetSubPolygonVertexOrders(List<Vector3> allVertices, List<int> originalVertexOrder, Dictionary<int, int> specialCaseVertexIndices, List<(Vector3, int)> allHits)
    {
        // make vertex order to a dict instead of a lookup-array to merge it with the special cases (they have negative indices)
        Dictionary<int, int> vertexOrderAsDict = Triangulator.MakeVertexOrderToDict(originalVertexOrder);
        specialCaseVertexIndices.ToList().ForEach(x => vertexOrderAsDict.Add(x.Key, x.Value));
        // build the jumpDict (used for each "findNext" + keeps track of (un-)visited edges)
        // a entry looks like this: {cur: {prev1: next1}, {prev2: next2}}
        Dictionary<int, Dictionary<int, int>> jumpDict = Triangulator.MakeJumpDictionary(allHits, originalVertexOrder.Count, vertexOrderAsDict, allVertices);

        while (jumpDict.Count > 0)  // not all visited
        {
            List<int> vertexOrder = new List<int>();
            (int prev, int cur, int next) = (jumpDict.First().Value.First().Key, jumpDict.First().Key, jumpDict.First().Value.First().Value);

            while (true)
            {
                vertexOrder.Add(vertexOrderAsDict[cur]);
                // keep track of visited (by removing visited vertices/edges)
                jumpDict[cur].Remove(prev);
                if (jumpDict[cur].Count == 0)
                {
                    jumpDict.Remove(cur);
                }
                // can we stop here (is circle start reached)?
                if (vertexOrderAsDict[next] == vertexOrder[0])
                {
                    // yes => stop
                    break;
                }
                // no => find next(s next)
                (prev, cur, next) = (cur, next, jumpDict[next][cur]);
            }
            // add last
            yield return vertexOrder;
        }
    }

    public static List<int> Triangulate(List<Vector3> vertices, List<int> vertexOrder)
    {
        List<int> triangles = new List<int>((vertexOrder.Count - 2) * 3);
        List<int> indexList = new List<int>();

        bool fullCycleWithoutTriangle = true;

        for (int i = 0; i < vertexOrder.Count; i++)
        {
            indexList.Add(i);
        }
        while (indexList.Count > 3)
        {
            fullCycleWithoutTriangle = true;
            for (int i = 0; i < indexList.Count; i++)
            {
                int order_index_p_prev = indexList[(i - 1 + indexList.Count) % indexList.Count];
                int order_index_p = indexList[i];
                int order_index_p_next = indexList[(i + 1) % indexList.Count];

                int index_p_prev = vertexOrder[order_index_p_prev];
                int index_p = vertexOrder[order_index_p];
                int index_p_next = vertexOrder[order_index_p_next];

                Vector3 p_prev = vertices[index_p_prev];
                Vector3 p = vertices[index_p];
                Vector3 p_next = vertices[index_p_next];

                Vector3 to_prev = p_prev - p;
                Vector3 to_next = p_next - p;

                if (!VectorUtilities.IsConvex(to_prev, to_next))
                {
                    continue;  // reflex corner
                }

                // Does test ear contain any polygon vertices?
                bool isEar = true;
                for (int j = 0; j < indexList.Count - 3; j++)
                {
                    int other_index = vertexOrder[indexList[(i + 2 + j) % indexList.Count]];
                    // skip if index is a mutually visible index to one of the corner-points
                    if (other_index == index_p_prev || other_index == index_p || other_index == index_p_next)
                    {
                        continue;
                    }

                    Vector3 other = vertices[other_index];
                    if (VectorUtilities.IsPointInTriangle(other, p_prev, p, p_next))
                    {
                        isEar = false;
                        break;
                    }
                }

                if (isEar)
                {
                    triangles.Add(vertexOrder[order_index_p_prev]);
                    triangles.Add(vertexOrder[order_index_p]);
                    triangles.Add(vertexOrder[order_index_p_next]);

                    indexList.RemoveAt(i);
                    fullCycleWithoutTriangle = false;
                    break;
                }
            }

            if (fullCycleWithoutTriangle)
            {
                Debug.LogError("Triangulation failed!");
                // throw new Exception("Triangulation failed!");
                return triangles;
            }
        }
        // add remaining triangle
        triangles.Add(vertexOrder[indexList[0]]);
        triangles.Add(vertexOrder[indexList[1]]);
        triangles.Add(vertexOrder[indexList[2]]);

        return triangles;
    }

}