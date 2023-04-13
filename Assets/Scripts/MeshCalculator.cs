using UnityEngine;
using System;
using System.Linq;
using System.Collections.Generic;

public class MeshCalculator
{
    public static readonly int MAX_TEXT_ARR_DEPTH = 1024;  // 2048

    private static System.Random rnd = new System.Random();

    public static int ModUvDepth(int uvDepth, int churchWindowTextureIndex)
    {
        int index = ((Math.Abs(uvDepth) % MeshCalculator.MAX_TEXT_ARR_DEPTH) + (Math.Abs(uvDepth) < MeshCalculator.MAX_TEXT_ARR_DEPTH ? 0 : 1)) * Math.Sign(uvDepth);
        // make sure the returned index is a non-church index
        if (index == churchWindowTextureIndex)
        {
            index--;
        }
        return index;
    }

    public static int ClampUvDepth(int uvDepth)
    {
        return Mathf.Clamp(uvDepth, 0, MeshCalculator.MAX_TEXT_ARR_DEPTH);
    }


    public static Mesh CalculateFacadeMesh(ref List<Vector3> allVertices, ref List<int> topTriangles, ref List<int> bottomTriangles, ref List<Vector3> extraRoofVertices,
                                        List<int> holeSizes, bool isGroundLevel, bool addConnectionRing,
                                        float minHeight, float roofStartHeight, float totalHeight, float height_multiplier, Vector3 center,
                                        Facade facade, int uvDepthWindow, int uvDepthDoor, int churchWindowTextureIndex, int levels, string colorHtmlString)
    {
        // use all vertices for a full ring; use at least two rings (1x on min-level and a duplicate on top-level)
        // for ground-level-buildings we need 3x rings (to prevent floating buildings): below ground, ground level, top level
        int allVerticesCount = allVertices.Count;

        int nrOfRings = (isGroundLevel ? 3 : 2) + (addConnectionRing ? 1 : 0);
        // the "+ holeSizes.Count" is there so we have no uv-seam:
        /*
          Vertices:         v1  v2  v3  v4  v5 ... vN  v1
          UVs (with seam):  0   1   2   3   4     N-1  0
          UVs (seam-less):  0   1   2   3   4     N-1  N
        */
        List<Vector3> buildingVertices = new List<Vector3>((allVerticesCount * holeSizes.Count) * nrOfRings);
        // buildingVertices = [(below ground), ground level, top level, (connection-ring), seam-less copies]

        int wallIndicesCount = allVerticesCount * (nrOfRings - 1) * 6;
        List<int> buildingTriangles = new List<int>(wallIndicesCount + topTriangles.Count + bottomTriangles.Count);

        // add below ground ring (if needed)
        if (isGroundLevel)
        {
            buildingVertices.AddRange(allVertices.ConvertAll(v => v - Vector3.up * 10));
        }
        // add ground level (or floating) ring
        buildingVertices.AddRange(allVertices.ConvertAll(v => v + Vector3.up * (minHeight * height_multiplier)));
        // add roof start height ring
        buildingVertices.AddRange(allVertices.ConvertAll(v => v + Vector3.up * (roofStartHeight * height_multiplier)));
        // add roof ring (if needed); the roof ring is the first |allVertices| vertices in extraRoofVertices =.GetRange(0, allVertices.Count)
        // but we need all of it when we have topTriangles
        if (addConnectionRing || topTriangles.Count > 0)
        {
            buildingVertices.AddRange(extraRoofVertices.ConvertAll(v => v + Vector3.up * (totalHeight * height_multiplier)));
        }


        // add triangles for rings
        /* Triangulation like this (viewed from the inside):
                r--(r+1)     r--(r+1)
                |   |    =>  | \ |
                0---1        0---1
        */
        for (int ringIndex = 0; ringIndex < nrOfRings - 1; ringIndex++)
        {
            int walkingSum = 0;
            int ringOffset = ringIndex * allVerticesCount;
            foreach (int holeSize in holeSizes)
            {
                foreach (var t in Enumerable.Range(0, holeSize).ToList().ConvertAll(x =>
                    // x, right of x      , above of x          , right of x        , right and above of x                 , above of x
                    (x, (x + 1) % holeSize, x + allVerticesCount, (x + 1) % holeSize, (x + 1) % holeSize + allVerticesCount, x + allVerticesCount)
                ))
                {
                    if (t.Item2 == 0)
                    {
                        // special case: add and use the 2nd set of start-vertices of each hole (seamless -> s. above)
                        buildingTriangles.Add(t.Item1 + walkingSum + ringOffset);
                        buildingTriangles.Add(buildingVertices.Count());
                        buildingTriangles.Add(t.Item3 + walkingSum + ringOffset);

                        buildingTriangles.Add(buildingVertices.Count());
                        buildingTriangles.Add(buildingVertices.Count() + holeSizes.Count);
                        buildingTriangles.Add(t.Item6 + walkingSum + ringOffset);

                        // buildingVertices = [(below ground), ground level, top level, (connection-ring), seam-less copies]
                        buildingVertices.Add(buildingVertices[t.Item2 + walkingSum + ringOffset]);
                    }
                    else
                    {
                        // normal case
                        buildingTriangles.Add(t.Item1 + walkingSum + ringOffset);
                        buildingTriangles.Add(t.Item2 + walkingSum + ringOffset);
                        buildingTriangles.Add(t.Item3 + walkingSum + ringOffset);

                        buildingTriangles.Add(t.Item4 + walkingSum + ringOffset);
                        buildingTriangles.Add(t.Item5 + walkingSum + ringOffset);
                        buildingTriangles.Add(t.Item6 + walkingSum + ringOffset);
                    }
                }
                walkingSum += holeSize;
            }
        }
        {
            // add vertex for last ring
            int walkingSum = 0;
            int ringOffset = (nrOfRings - 1) * allVerticesCount;
            foreach (int holeSize in holeSizes)
            {
                buildingVertices.Add(buildingVertices[walkingSum + ringOffset]);
                walkingSum += holeSize;
            }
        }
        // add top triangles
        buildingTriangles.AddRange(topTriangles.ConvertAll(x => x + allVerticesCount * (nrOfRings - 1)));  // we can ignore the 2nd set
        // add bottom triangles
        bottomTriangles.Reverse();
        buildingTriangles.AddRange(bottomTriangles);  // we can ignore the 2nd set

        // use the center as reference point for all vertices
        buildingVertices = buildingVertices.ConvertAll(v => v - center);

        // build the uv-coordinates:
        // first half (0,0), second half (0, maxLvl); so that untouched vertices have the same uv-coords as their neighboring vertices
        // -> area/triangle has no skewed window, instead it has a solid color matching to the rest of the facade
        Vector4[] buildingUVs = Enumerable.Repeat(
                                    new Vector4(
                                        0,
                                        0,
                                        facade.facadeRule.isChurch ? churchWindowTextureIndex : MeshCalculator.ModUvDepth(uvDepthWindow, churchWindowTextureIndex),
                                        MeshCalculator.ModUvDepth(uvDepthDoor, churchWindowTextureIndex)
                                    ), Mathf.CeilToInt(buildingVertices.Count / 2f)
                                ).Concat(Enumerable.Repeat(
                                    new Vector4(
                                        0,
                                        levels,
                                        facade.facadeRule.isChurch ? churchWindowTextureIndex : MeshCalculator.ModUvDepth(uvDepthWindow, churchWindowTextureIndex),
                                        MeshCalculator.ModUvDepth(uvDepthDoor, churchWindowTextureIndex)
                                    ), Mathf.FloorToInt(buildingVertices.Count / 2f)
                                )).ToArray();
        float[] perRowHeight = (new float[] { -1, 0, (float)levels, (float)levels }).Skip(isGroundLevel ? 0 : 1).Take((isGroundLevel ? 3 : 2) + (addConnectionRing ? 1 : 0)).ToArray();
        // same ring-like traversal as above, but now for uv's
        {
            float walkingUv = 0;
            int walkingSum = 0;
            // int ringOffset = (isGroundLevel ? 1 : 0) * allVerticesCount;
            for (int i = 0; i < holeSizes.Count; i++)
            {
                /* Triangulation like this (viewed from the inside):
                        r--(r+1)     r--(r+1)
                        |   |    =>  | \ |
                        0---1        0---1
                */
                foreach (var j in Enumerable.Range(0, holeSizes[i]))
                {
                    for (int k = 0; k < perRowHeight.Length; k++)
                    {
                        // buildingVertices = [(below ground), ground level, top level, (connection-ring), seam-less copies]
                        buildingUVs[j + walkingSum + k * allVerticesCount].x = walkingUv;
                        buildingUVs[j + walkingSum + k * allVerticesCount].y = perRowHeight[k];
                    }

                    float width = (allVertices[j + walkingSum] - allVertices[((j + 1) % holeSizes[i]) + walkingSum]).magnitude;
                    // 2x normal, since 1/2 is window and 1/2 is wall
                    // 2x1.75 for paired, since the middle 1/4+1/4 (between the windows) gets halved
                    float width_factor = facade.layout == WindowLayout.SINGLE ? 2f : 2f * 1.75f;
                    if (facade.facadeRule.hasWindows && width > facade.windowWidth * width_factor)
                    {
                        walkingUv += Mathf.Floor(width / (facade.windowWidth * width_factor));
                    }
                }
                // extra uvs
                for (int k = 0; k < perRowHeight.Length; k++)
                {
                    // buildingVertices = [(below ground), ground level, top level, (connection-ring), seam-less copies]
                    buildingUVs[allVerticesCount * nrOfRings + k * holeSizes.Count + i].x = walkingUv;
                    buildingUVs[allVerticesCount * nrOfRings + k * holeSizes.Count + i].y = perRowHeight[k];
                }

                walkingSum += holeSizes[i];
            }
        }

        // now build the mesh with all the calculated information
        Mesh mesh = new Mesh();
        mesh.vertices = buildingVertices.ToArray();
        mesh.triangles = buildingTriangles.ToArray();
        mesh.SetUVs(0, buildingUVs.ToArray());
        // 0,0,0 -> no color, use random color, alpha encodes isGroundLevel
        Color color = new Color();
        if (!ColorUtility.TryParseHtmlString(colorHtmlString, out color))
        {
            color = new Color(0, 0, 0, 0);
        }
        color.a = isGroundLevel ? 1 : 0;
        mesh.colors = Enumerable.Repeat(color, buildingVertices.Count).ToArray();
        // mesh.uv = buildingUVs.ToArray();
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }


    public static Mesh CalculateRoofMesh(ref List<Vector3> allVertices, ref List<List<int>> roofTriangles, ref List<Vector3> extraRoofVertices,
                                    int extraRoofRings, bool addRoofBase, bool isFlatRoof,
                                    float roofStartHeight, float totalHeight, float height_multiplier, Vector3 center, string colorHtmlString)
    {
        int allVerticesCount = allVertices.Count;

        // generate a mesh out of each roofTriangle list (but we can use the same ??? or 1x mesh but multiple same vertices?)
        // and extraRoofRings if needed

        // build pre-roof-vertices list (extraRoofVertices + allVertices if roofBase is needed) so we later have no height-/center-issues
        List<Vector3> preRoofVertices = new List<Vector3>(allVertices.Count * (addRoofBase ? 1 : 0) + extraRoofVertices.Count);
        // add roof start height ring (if needed)
        if (addRoofBase) preRoofVertices.AddRange(allVertices.ConvertAll(v => v + Vector3.up * (roofStartHeight * height_multiplier)));
        // add roofVertices
        preRoofVertices.AddRange(extraRoofVertices.ConvertAll(v => v + Vector3.up * (totalHeight * height_multiplier)));
        // use the center as reference point for all vertices
        preRoofVertices = preRoofVertices.ConvertAll(v => v - center);


        List<Vector3> roofVertices = new List<Vector3>(roofTriangles.Sum(l => l.Count));

        // 1st) add roofRings
        List<(int, int, int, int, int, int)> ringIndices = Enumerable.Range(0, allVerticesCount).ToList().ConvertAll(
            /* Triangulation like this (viewed from the inside):
                    r--(r+1)     r--(r+1)
                    |   |    =>  | \ |
                    0---1        0---1
            */
            x => (x, (x + 1) % allVerticesCount, x + allVerticesCount, (x + 1) % allVerticesCount, (x + 1) % allVerticesCount + allVerticesCount, x + allVerticesCount)
        );
        for (int ringIndex = 0; ringIndex < extraRoofRings; ringIndex++)
        {
            int ringOffset = ringIndex * allVerticesCount;
            ringIndices.ForEach(
                t =>
                {
                    roofVertices.Add(preRoofVertices[t.Item1 + ringOffset]);
                    roofVertices.Add(preRoofVertices[t.Item2 + ringOffset]);
                    roofVertices.Add(preRoofVertices[t.Item3 + ringOffset]);

                    roofVertices.Add(preRoofVertices[t.Item4 + ringOffset]);
                    roofVertices.Add(preRoofVertices[t.Item5 + ringOffset]);
                    roofVertices.Add(preRoofVertices[t.Item6 + ringOffset]);
                }
            );
        }

        // 2nd) add roofTriangles
        foreach (List<int> area in roofTriangles)
        {
            foreach (int index in area)
            {
                roofVertices.Add(preRoofVertices[index]);
            }
        }

        // trivial
        List<int> roofIndices = Enumerable.Range(0, roofVertices.Count).ToList();

        // build mesh now and then use the normals to evaluate the UVs
        Mesh roofMesh = new Mesh();
        roofMesh.vertices = roofVertices.ToArray();
        roofMesh.triangles = roofIndices.ToArray();
        Color color = new Color();
        if (!ColorUtility.TryParseHtmlString(colorHtmlString, out color))
        {
            color = new Color(1, 1, 1, 0);
        }
        roofMesh.colors = Enumerable.Repeat(color, roofVertices.Count).ToArray();
        roofMesh.RecalculateBounds();
        roofMesh.RecalculateNormals();

        float uvScale = height_multiplier * 16.0f;
        if (isFlatRoof)
        {
            // rotate randomly so not all flat roof-tops are in line
            float rndRotateAngle = (float)(MeshCalculator.rnd.Next(-180, 180) * Mathf.PI / 360.0);
            roofMesh.SetUVs(0, roofVertices.ConvertAll(v =>
            {
                float x = v.x * uvScale;
                float y = v.z * uvScale;
                return new Vector3(
                    x * Mathf.Cos(rndRotateAngle) - y * Mathf.Sin(rndRotateAngle),
                    x * Mathf.Sin(rndRotateAngle) + y * Mathf.Cos(rndRotateAngle),
                    1   // isFlatRoof
                );
            }));
            return roofMesh;
        }
        // .................................................... 0 = isNotFlatRoof
        Vector3[] roofUvs = Enumerable.Repeat(new Vector3(0, 0, 0), roofVertices.Count).ToArray();

        // project normals onto 2D plane
        List<Vector3> projectedNormals = roofMesh.normals.ToList().ConvertAll(v =>
        {
            Vector3 result = (new Vector3(v.x, 0, v.z)).normalized;
            return result == Vector3.zero ? Vector3.up : result;
        });
        // group similar normals
        Dictionary<Vector3, List<int>> distinctDict = new Dictionary<Vector3, List<int>>();
        for (int i = 0; i < projectedNormals.Count; i++)
        {
            Vector3 key = projectedNormals[i];
            foreach ((Vector3 keyNormal, List<int> _) in distinctDict)
            {
                if (Vector3.Dot(projectedNormals[i], keyNormal) > 0.95)
                {
                    key = keyNormal;
                    break;
                }
            }
            if (!distinctDict.ContainsKey(key))
            {
                distinctDict[key] = new List<int>();
            }
            distinctDict[key].Add(i);
        }
        // calculate/find min distanced vertex (the index)
        Dictionary<Vector3, int> minDistanceDict = new Dictionary<Vector3, int>();
        foreach ((Vector3 n, List<int> indices) in distinctDict)
        {
            // pick distancePoint so, that we get the top most centered vertex as closest vertex
            // the closes vertex has the UV-coords of (0.5, 1)
            Vector3 distancePoint = roofMesh.vertices[indices[0]] - 100f * n + Vector3.up * 100;
            Vector3 cross = Vector3.Cross(n, Vector3.up);

            int minIndex = indices[0];
            float minDist = VectorUtilities.DistancePointToInfiniteLine(distancePoint, cross, roofMesh.vertices[indices[0]]);
            foreach (int index in indices)
            {
                float dist = VectorUtilities.DistancePointToInfiniteLine(distancePoint, cross, roofMesh.vertices[index]);
                if (dist < minDist)
                {
                    minDist = dist;
                    minIndex = index;
                }
            }
            minDistanceDict[n] = minIndex;
        }
        // now calculate the UVs
        foreach ((Vector3 n, List<int> indices) in distinctDict)
        {
            // if(n == Vector3.up) continue;

            foreach (int index in indices)
            {
                Vector3 diffVec = roofMesh.vertices[index] - roofMesh.vertices[minDistanceDict[n]];
                diffVec = Quaternion.Euler(0, Vector3.SignedAngle(projectedNormals[index], Vector3.forward, Vector3.up), 0) * diffVec;
                // the closes vertex has the UV-coords of (0.5, 1)
                roofUvs[index].x = 0.5f + diffVec.x * uvScale;
                roofUvs[index].y = 1 + Mathf.Sqrt(diffVec.z * diffVec.z + diffVec.y * diffVec.y) * uvScale;
            }
        }

        roofMesh.SetUVs(0, roofUvs);
        return roofMesh;
    }
}
