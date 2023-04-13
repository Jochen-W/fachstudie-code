using UnityEngine;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using GeoJSON.Net.Feature;
using GeoJSON.Net.Geometry;

public class BuildingGenerator : MonoBehaviour
{
    // public static GraphicsFormat smallestSupportedTextureFormat = GraphicsFormat.R8G8B8A8_UNorm;
    public LatLonInfo latLonInfo;
    public ConfigInfo configInfo;
    public ShaderMapping shaderMapping;
    public GameObject buildingPrefab;
    public Shader buildingShader;
    public GameObject roofPrefab;
    public Shader roofShader;
    public GameObject airVentInstanceHandlerPrefab;
    public Texture2D stairTexture;
    public Texture2D roofTex;
    public Texture2D flatRoofTex;
    public GameObject loadingUI;
    public int textureResolution = 256;

    private float height_multiplier = -1;

    private struct PerBuildingID
    {
        public Facade facade;
        public Vector3 sharedCenter;

        public PerBuildingID(Facade f, Vector3 c)
        {
            facade = f;
            sharedCenter = c;
        }
    }

    private async void Awake()
    {
        // BuildingGenerator.FindSmallestSupportedTextureFormats();
        loadingUI.SetActive(true);

        List<Task> tasks = new List<Task>();

        height_multiplier = latLonInfo.GetHeightMultiplier(configInfo);
        (int tile_x, int tile_y) = latLonInfo.AsTileXY(configInfo);

        for (int y = -configInfo.radius; y < configInfo.radius + 1; y++)
        {
            for (int x = -configInfo.radius; x < configInfo.radius + 1; x++)
            {
                FeatureCollection coll = await CachedRequestMaker.GetBuildingTileData(configInfo, tile_x + x, tile_y + y);
                if (coll is null)
                {
                    Debug.Log($"No buildings available for this tile: {configInfo.zoom}/{tile_x + x}/{tile_y + y}");
                    continue;
                }
                await InstantiateCollection(coll, (x, y));
            }
        }
        // Task.WaitAll(tasks.ToArray());

        // wait for initial culling/render
        await Task.Delay(1000);
        loadingUI.SetActive(false);
    }

    private async Task InstantiateCollection(FeatureCollection collection, (int, int) tileOffset)
    {
        // Window, WindowLayout -> depth, mainTex, normalMap
        Dictionary<(Window, WindowLayout), (int, RenderTexture)> texArrayPrecursor = new Dictionary<(Window, WindowLayout), (int, RenderTexture)>();
        Dictionary<Door, (int, RenderTexture)> texArrayPrecursorDoors = new Dictionary<Door, (int, RenderTexture)>();
        // keep track of the generated church window texture, since we only need one
        //  and we don't want to get the index for non-church windows (s. MeshCalculator.ModUvDepth)
        int churchWindowTextureIndex = -1;

        (int tile_x, int tile_y) = latLonInfo.AsTileXY(configInfo);
        Texture2D heightMap = await CachedRequestMaker.GetTextureTileData(configInfo, tile_x + tileOffset.Item1, tile_y + tileOffset.Item2, TileType.ELEVATION);

        Material sharedBuildingMaterial = new Material(buildingShader);
        sharedBuildingMaterial.SetFloat("_HeightMultiplier", latLonInfo.GetHeightMultiplier(configInfo));
        sharedBuildingMaterial.SetFloat("_TileSize", configInfo.tileSizeUnity);
        sharedBuildingMaterial.SetFloat("_InverseTileSize", 1.0f / configInfo.tileSizeUnity);
        sharedBuildingMaterial.SetTexture("_HeightMap", heightMap);

        Material sharedRoofMaterial = new Material(roofShader);
        sharedRoofMaterial.SetFloat("_HeightMultiplier", latLonInfo.GetHeightMultiplier(configInfo));
        sharedRoofMaterial.SetFloat("_TileSize", configInfo.tileSizeUnity);
        sharedRoofMaterial.SetFloat("_InverseTileSize", 1.0f / configInfo.tileSizeUnity);
        sharedRoofMaterial.SetTexture("_HeightMap", heightMap);
        sharedRoofMaterial.SetTexture("_MainTex", roofTex);
        sharedRoofMaterial.SetTexture("_MainTexFlat", flatRoofTex);


        GameObject airVent = Instantiate(airVentInstanceHandlerPrefab);
        InstanceHandler instanceHandler = airVent.GetComponentInChildren<InstanceHandler>();


        // use this dictionary to filter out hull-buildings (buildings that build the convex hull of all building-parts)
        Dictionary<string, (HashSet<Vector3>, float)> hullLookup = new Dictionary<string, (HashSet<Vector3>, float)>();
        foreach (Feature feature in collection.Features)
        {
            if (feature.Geometry.Type == GeoJSON.Net.GeoJSONObjectType.Polygon && feature.Properties.ContainsKey("building") && !feature.Properties.ContainsKey("minHeight"))
            {
                string buildingId = (string)feature.Properties["building"];
                if (!hullLookup.ContainsKey(buildingId))
                {
                    hullLookup.Add(buildingId, (new HashSet<Vector3>(), 0));
                }
                List<Vector3> outerVertices = ToScenePositions(((Polygon)feature.Geometry).Coordinates[0], false);
                foreach (Vector3 vertex in outerVertices)
                {
                    hullLookup[buildingId].Item1.Add(vertex);
                }
                float area = PolygonUtilities.CalculateAreaOfPolygon(outerVertices, Enumerable.Range(0, outerVertices.Count).ToList());
                hullLookup[buildingId] = (hullLookup[buildingId].Item1, hullLookup[buildingId].Item2 + area);
            }
        }

        // use the same ground height for all building-parts of a building (the first part determines this point)
        Dictionary<string, PerBuildingID> perBuildingData = new Dictionary<string, PerBuildingID>();

        // go through all features (=building or building-part)
        foreach (Feature feature in collection.Features)
        {
            bool hasNonFlatRoofShape = feature.Properties.ContainsKey("roofShape") && (string)feature.Properties["roofShape"] != "flat";
            // we want ground as reference for all heights
            // for minHeight this always holds true
            float minHeight = feature.Properties.ContainsKey("minHeight") ? Convert.ToSingle(feature.Properties["minHeight"]) : 0;
            // but not for those two:
            float rawHeight = feature.Properties.ContainsKey("height") ? Convert.ToSingle(feature.Properties["height"]) : 10;  // default height 10m
            float roofHeight = feature.Properties.ContainsKey("roofHeight") ? Convert.ToSingle(feature.Properties["roofHeight"]) : (hasNonFlatRoofShape ? Mathf.Min(3, (rawHeight - minHeight) * 0.3f) : 0);  // default roofHeight 3m (or 30% of total height)

            // recalculate totalHeight and roofStartHeight (ground as reference)
            float totalHeight = -1;
            float roofStartHeight = -1;
            if (roofHeight + minHeight < rawHeight || Mathf.Approximately(roofHeight + minHeight, rawHeight))
            {
                totalHeight = rawHeight;
                roofStartHeight = rawHeight - roofHeight;
            }
            else
            {
                totalHeight = roofHeight;
                roofStartHeight = rawHeight;
            }
            bool isGroundLevel = minHeight == 0;

            if (feature.Geometry.Type == GeoJSON.Net.GeoJSONObjectType.Polygon)
            {
                try
                {
                    // use allVertices and combinedVertexOrder to store all mesh-information
                    LineString outer = ((Polygon)feature.Geometry).Coordinates[0];
                    List<Vector3> outerVertices = ToScenePositions(outer, false);
                    List<Vector3> allVertices = new List<Vector3>(outerVertices);
                    List<int> combinedVertexOrder = Enumerable.Range(0, allVertices.Count).ToList();

                    if (!feature.Properties.ContainsKey("building") && IsHullBuilding(outerVertices, hullLookup))
                    {
                        continue; // is hull-building
                    }

                    // calculate center point
                    Vector3 center = Vector3.zero;
                    for (int j = 0; j < allVertices.Count; j++)
                    {
                        center += allVertices[j];
                    }
                    center /= allVertices.Count;
                    center.y = 0.0f;
                    // check if center is on the current tile; skip the building if not (necessary since buildings on tile-edges occur on both tiles)
                    Vector3 inTileVec = center - new Vector3(tileOffset.Item1 * configInfo.tileSizeUnity, 0, -tileOffset.Item2 * configInfo.tileSizeUnity);
                    if (
                        !(inTileVec.x >= -configInfo.tileSizeUnity * 0.5 && inTileVec.x < configInfo.tileSizeUnity * 0.5 &&
                        inTileVec.z >= -configInfo.tileSizeUnity * 0.5 && inTileVec.z < configInfo.tileSizeUnity * 0.5)
                    )
                    {
                        continue;
                    }


                    // add holes...
                    SortedList<float, List<Vector3>> holes = new SortedList<float, List<Vector3>>();
                    for (int i = 1; i < ((Polygon)feature.Geometry).Coordinates.Count; i++)
                    {
                        List<Vector3> sceneVertices = ToScenePositions(((Polygon)feature.Geometry).Coordinates[i], true);
                        // for the unlikely case if two ore more hole have the same x-position
                        for (int j = 0; j < ((Polygon)feature.Geometry).Coordinates.Count; j++)
                        {
                            try
                            {
                                holes.Add(sceneVertices[PolygonUtilities.GetRightmostIndex(sceneVertices)].x - j * float.Epsilon, sceneVertices);
                                break;
                            }
                            catch (ArgumentException)
                            {
                                // continue
                            }

                        }
                    }
                    // ...from right to left -> reversed loop
                    for (int i = holes.Count - 1; i >= 0; i--)
                    {
                        List<Vector3> sceneVertices = holes.Values[i];
                        List<int> vertexOrder = Enumerable.Range(0, sceneVertices.Count).ToList();
                        try
                        {
                            (allVertices, combinedVertexOrder) = Triangulator.CombineVertices(allVertices, combinedVertexOrder, sceneVertices, vertexOrder);
                        }
                        catch (System.Exception)
                        {
                            // hole data is corrupted -> remove it
                            holes.RemoveAt(i);
                        }
                    }

                    // now we have all vertices in order in two data-structures (allVertices and combinedVertexOrder)
                    // start of triangulation (based on roofShape)
                    List<int> bottomTriangles = new List<int>(); // for bottom-side use a simple flat plane (if the building is not on the ground)
                    if (!isGroundLevel)
                    {
                        bottomTriangles = Triangulator.Triangulate(allVertices, combinedVertexOrder);
                    }
                    List<int> topTriangles = new List<int>();  // triangles who are byproducts of the roof-triangulation (but are no roof, e.g. the side triangles from a gabled roof)
                    List<List<int>> roofTriangles = new List<List<int>>();  // triangles that are actually the roof
                    List<Vector3> extraRoofVertices = new List<Vector3>();  // vertices, that build the roof
                    bool addConnectionRing = false;  // connects roofVertices with vertices below (like a extra roof ring)
                    bool addRoofBase = false;  // wether to add the upper ring of the building to the roof-vertices (e.g. flat roofs need that)
                    int extraRoofRings = 0;  // simple rings (like wall rings) but for the roof (e.g. dome)

                    string roofShape = feature.Properties.ContainsKey("roofShape") ? (string)feature.Properties["roofShape"] : "flat";
                    /* base idea:
                        1) find the corner-points (edge start/end points) of the roof (e.g. for gabled the two top-most points: >---<)
                        2) shoot rays along all edges to separate the areas (e.g. for gabled from the one corner to the other: >→→→<)
                            2.1) if a ray hits some (outer) edge, add the hit-point to the vertexOrder (+vertices)
                            2.2) make a jump-dictionary for the triangulation (e.g. "if you hit cornerA, the next vertex is cornerB")
                        3) find the sub -areas and triangulate them
                    */
                    switch (roofShape)
                    {
                        case "gabled":
                        case "gabeld":  // sometimes wrongly spelled
                        case "hipped":
                        case "half-hipped":
                            {
                                if (!feature.Properties.ContainsKey("roofDirection"))
                                {
                                    Debug.LogWarning("No roofDirection available! Used Flat roof as fallback");
                                    roofTriangles.Add(Triangulator.Triangulate(allVertices, combinedVertexOrder));
                                    addRoofBase = true;
                                    roofShape = "flat";
                                    roofStartHeight = totalHeight;
                                    break;
                                }
                                float clockwiseAngleFromN = (float)(Math.PI / 180.0) * Convert.ToSingle(feature.Properties["roofDirection"]);
                                Vector3 roofDir = new Vector3(MathF.Sin(clockwiseAngleFromN), 0, MathF.Cos(clockwiseAngleFromN));
                                /*
                                - find mid point (M) to start ray
                                - make ray in both directions to find start(S)/end(E) of roof-half (start/end = corner-points):
                                    1----->--2      1----->--2
                                    |        |      |        |
                                    |   M    |  =>  S---M----E
                                    |        |      |        |
                                    4--<-----3      4--<-----3
                                */
                                Vector3 midPos = CalculateMidPos(allVertices, roofDir);
                                Vector3 rayStart = midPos - 100f * roofDir; // go outside the polygon
                                                                            // get (all) hits/intersections with the building and the roofDirection-ray
                                List<(Vector3, int)> hits = PolygonUtilities.FindAllRayHitsInXZPlane(rayStart, roofDir, allVertices, combinedVertexOrder);
                                // sort them by distance (from a outside point on the ray), to get S and E
                                hits.Sort((a, b) => Vector3.Distance(rayStart, a.Item1).CompareTo(Vector3.Distance(rayStart, b.Item1)));
                                Vector3 S = hits[0].Item1;
                                Vector3 E = hits[hits.Count - 1].Item1;
                                // reposition S/E
                                //  -> for gabled: 0.1% (=not recognizable, but correct for the math)
                                //  -> for hipped: 10% inwards; or 90% till next hit, if next hit is closer than 10%
                                float distStoE = Vector3.Distance(S, E);
                                float pushInPercentage = roofShape == "gabled" || roofShape == "gabeld" ? 0.001f : 0.1f;
                                S += roofDir * Mathf.Min(distStoE * pushInPercentage, Vector3.Distance(S, hits[1].Item1) * 0.9f);
                                E -= roofDir * Mathf.Min(distStoE * pushInPercentage, Vector3.Distance(E, hits[hits.Count - 2].Item1) * 0.9f);

                                // find s_0 and s_n, as well as e_0  and e_n (to shoot rays from S/E)
                                /*
                                    STRUCTURE    GABLED/HALF       HIPPED
                                        x----          x----       s_next----
                                    /              /              / \
                                    x              s_next         x  \
                                    |              |  \           |   \
                                    |        =>    S'   S   =>    S'   S
                                    |              |  /           |   /
                                    x              s_prev         x  /
                                    \              \              \ /
                                        x----          x----       s_prev----
                                */
                                // special cases if the hit-positions (S/E) directly hit a vertex -> then prev/next must be shifted by 1
                                // normal case: prev = prevFromHit, next = (prevFromHit+1) % order.Count
                                int indexPrevFromS = hits[0].Item1 == allVertices[combinedVertexOrder[hits[0].Item2]] ? (hits[0].Item2 - 1 + combinedVertexOrder.Count) % combinedVertexOrder.Count : hits[0].Item2;
                                int indexNextFromS = hits[0].Item1 == allVertices[combinedVertexOrder[(hits[0].Item2 + 1) % combinedVertexOrder.Count]] ? (hits[0].Item2 + 2) % combinedVertexOrder.Count : (hits[0].Item2 + 1) % combinedVertexOrder.Count;
                                int indexPrevFromE = hits[hits.Count - 1].Item1 == allVertices[combinedVertexOrder[hits[hits.Count - 1].Item2]] ? (hits[hits.Count - 1].Item2 - 1 + combinedVertexOrder.Count) % combinedVertexOrder.Count : hits[hits.Count - 1].Item2;
                                int indexNextFromE = hits[hits.Count - 1].Item1 == allVertices[combinedVertexOrder[(hits[hits.Count - 1].Item2 + 1) % combinedVertexOrder.Count]] ? (hits[hits.Count - 1].Item2 + 2) % combinedVertexOrder.Count : (hits[hits.Count - 1].Item2 + 1) % combinedVertexOrder.Count;
                                // only special for hipped, otherwise just one triangle (s. above)
                                if (roofShape == "hipped")
                                {
                                    // prevS: E+1, S-1, S, roofDir, ++
                                    int newIndexPrevFromS = FindPreviousIndex(allVertices, combinedVertexOrder, outerVertices.Count, indexNextFromE, indexPrevFromS, S, roofDir);
                                    // prevE: S+1, E-1, E, -roofDir, ++
                                    int newIndexPrevFromE = FindPreviousIndex(allVertices, combinedVertexOrder, outerVertices.Count, indexNextFromS, indexPrevFromE, E, -roofDir);
                                    // nextS: E-1, S+1, S, roofDir, --
                                    int newIndexNextFromS = FindPreviousIndex(allVertices, combinedVertexOrder, outerVertices.Count, indexPrevFromE, indexNextFromS, S, roofDir, -1);
                                    // nextE: S-1, E+1, E, -roofDir, --
                                    int newIndexNextFromE = FindPreviousIndex(allVertices, combinedVertexOrder, outerVertices.Count, indexPrevFromS, indexNextFromE, E, -roofDir, -1);

                                    indexPrevFromS = newIndexPrevFromS;
                                    indexNextFromS = newIndexNextFromS;
                                    indexPrevFromE = newIndexPrevFromE;
                                    indexNextFromE = newIndexNextFromE;
                                }


                                // remember the areas that are not part of the roof (but byproducts of the roof-segmentation)
                                List<List<Vector3>> nonRoofAreas = new List<List<Vector3>>();
                                if (roofShape == "gabled" || roofShape == "gabeld")
                                {
                                    // add the sides the sides
                                    nonRoofAreas.Add(new List<Vector3>() { S });
                                    nonRoofAreas[0].AddRange(GetRangeOfVertices(allVertices, combinedVertexOrder, indexPrevFromS, indexNextFromS));
                                    nonRoofAreas.Add(new List<Vector3>() { E });
                                    nonRoofAreas[1].AddRange(GetRangeOfVertices(allVertices, combinedVertexOrder, indexPrevFromE, indexNextFromE));
                                }


                                // use the found vertices for rays/hits (+ S/E) and add them to the vertexOrder
                                (List<(Vector3, int)> allJumpPairs, Dictionary<int, int> newVerticesLookup) = Triangulator.AddRayHitsToPolygon(
                                    ref allVertices, ref combinedVertexOrder,
                                    new Dictionary<int, List<int>>(){
                                    {-1, new List<int>(){-2, indexPrevFromS, indexNextFromS}},
                                    {-2, new List<int>(){indexPrevFromE, indexNextFromE}}
                                    }, new Dictionary<int, Vector3>(){
                                    {-1, S}, {-2, E}
                                    }
                                );

                                try
                                {
                                    // now triangulate all sub-polygons
                                    IEnumerable<List<int>> allSubPolygons = Triangulator.GetSubPolygonVertexOrders(
                                        allVertices, combinedVertexOrder, newVerticesLookup, allJumpPairs
                                    );
                                    foreach (List<int> subPolygonVertexOrder in allSubPolygons)
                                    {
                                        List<int> triangles = Triangulator.Triangulate(allVertices, subPolygonVertexOrder);
                                        if (IsRoofArea(triangles, nonRoofAreas, allVertices))
                                        {
                                            roofTriangles.Add(triangles);
                                        }
                                        else
                                        {
                                            topTriangles.AddRange(triangles);
                                        }
                                    }
                                }
                                catch (ArgumentOutOfRangeException)
                                {
                                    Debug.LogError("Direct hit problem. Fall back to flat roof!");
                                    roofTriangles.Add(Triangulator.Triangulate(allVertices, combinedVertexOrder));
                                    addRoofBase = true;
                                    roofShape = "flat";
                                    roofStartHeight = totalHeight;
                                    break;
                                }


                                // calculate height of each vertex and add them to extraRoofVertices
                                Func<Vector3, float> distanceFunc;
                                if (roofShape == "gabled" || roofShape == "gabeld")
                                {
                                    distanceFunc = (Vector3 vertex) => VectorUtilities.DistancePointToInfiniteLine(midPos, roofDir, vertex);
                                }
                                else
                                {
                                    distanceFunc = (Vector3 vertex) => VectorUtilities.DistancePointToLineSegment(S, E, vertex);
                                }
                                List<float> distances = allVertices.ConvertAll(vertex => distanceFunc(vertex));
                                float maxDistance = distances.Max();
                                for (int i = 0; i < allVertices.Count; i++)
                                {
                                    float height = (distances[i] / maxDistance) * (totalHeight - roofStartHeight);
                                    extraRoofVertices.Add(allVertices[i] - Vector3.up * height * height_multiplier);
                                }


                                addConnectionRing = true;  // we need a connection ring since:
                                /*  ___   ___
                                    |  \ /  |
                                    |   x   |   => x in extraRoofVertices is higher than x at roofStatHeight
                                    S_______E
                                */
                            }
                            break;
                        case "round":
                        case "mansard":
                        case "quadruple_saltbox":
                        case "gambrel":
                        case "double_saltbox":
                        case "saltbox":
                            {
                                /*
                                roofDir like gabled:
                                    1----->--S---2        1----->--S---2        1----->--S---2
                                    |            |         \ \ \ \ \ \ \      s'|\_____A____/| e'
                                    | --dir----> |   =>      --dir---->   =>    | |___B____| |
                                    |            |         / / / / / / /      s'|/     C    \| e'
                                    4--<-----E---3        4--<-----E---3        4--<-----E---3
                                A, B, C are the 3 planes (separated by s'->e' rays), the 2 areas on the sides are minimal (s. gabled)

                                basic idea:
                                    1) find top- and bottom-most points (e.g. 1 & 4)
                                    2) based on this ray and the level of detail, shoot rays from this ray in the direction = roofDir (e.g. A/B-ray and B/C-ray)
                                    3) connect the side-triangles for s'/e' like in the gabled case
                                        3.1) special case: two side-triangles would overlap -> merge them
                                */
                                if (!feature.Properties.ContainsKey("roofDirection"))
                                {
                                    Debug.LogWarning("No roofDirection available! Used Flat roof as fallback");
                                    roofTriangles.Add(Triangulator.Triangulate(allVertices, combinedVertexOrder));
                                    addRoofBase = true;
                                    roofShape = "flat";
                                    roofStartHeight = totalHeight;
                                    break;
                                }
                                float clockwiseAngleFromN = (float)(Math.PI / 180.0) * Convert.ToSingle(feature.Properties["roofDirection"]);
                                Vector3 roofDir = new Vector3(MathF.Sin(clockwiseAngleFromN), 0, MathF.Cos(clockwiseAngleFromN));
                                // find top-most and down-most vertex (e.g. 1 & 4 in example above) as base-line to shoot rays (to the right = roofDir)
                                (int indexForS, int indexForE) = FindMinMaxVertices(allVertices, Vector3.Cross(roofDir, Vector3.down));
                                (Vector3 S, Vector3 E) = (allVertices[indexForS], allVertices[indexForE]);

                                Dictionary<int, List<int>> rays = new Dictionary<int, List<int>>();
                                List<List<Vector3>> nonRoofAreas = new List<List<Vector3>>();
                                Dictionary<int, Vector3> newVertices = new Dictionary<int, Vector3>();
                                List<(Vector3, float)> heights = new List<(Vector3, float)>() {
                                (S, 1), (E, 1)
                            };

                                int counter = -1;
                                Dictionary<(int, int), int> edgeToPrimePoint = new Dictionary<(int, int), int>();  // for side-triangles (if we need to merge)

                                // shoot rays to the right (=roofDir)
                                float levelOfDetail = roofShape == "round" ? 6 : 2;
                                for (float detailLevel = 1; detailLevel < levelOfDetail + 1; detailLevel++)
                                {
                                    Vector3 rayStart = S + (E - S) * (detailLevel / (levelOfDetail + 1)) - roofDir * 100;
                                    //TODO: Problem: cut-hit!
                                    List<(Vector3, int)> hits = PolygonUtilities.FindAllRayHitsInXZPlane(rayStart, roofDir, allVertices, combinedVertexOrder);
                                    // sort them by distance (from a outside point on the ray), to get s' and e'
                                    hits.Sort((a, b) => Vector3.Distance(rayStart, a.Item1).CompareTo(Vector3.Distance(rayStart, b.Item1)));
                                    float pushInPercentage = roofShape == "mansard" ? 0.1f : 0.001f;
                                    float primeDist = Vector3.Distance(hits[0].Item1, hits[hits.Count - 1].Item1) * pushInPercentage;  // distance s' to e'
                                                                                                                                       // go through all hits and connect them to the outer shell (=add rays to rays-dict)
                                    for (int i = 0; i < hits.Count; i++)
                                    {
                                        bool isSPrime = i % 2 == 0;  // else it is e'

                                        // reposition s#/e': s' -> shift downwards (with roofDir +1); e' -> shift upwards (-1)
                                        newVertices[counter] = hits[i].Item1 + roofDir * (isSPrime ? 1 : -1) * Mathf.Min(primeDist, Vector3.Distance(hits[i].Item1, hits[i % 2 == 0 ? i + 1 : i - 1].Item1) * 0.45f);

                                        rays[counter] = new List<int>();
                                        // normal prev and next
                                        int prev = hits[i].Item2;
                                        int next = (hits[i].Item2 + 1) % combinedVertexOrder.Count;

                                        // nonRoofAreas
                                        if (roofShape != "mansard" && roofShape != "quadruple_saltbox")
                                        {
                                            // (always) add the side-triangle
                                            nonRoofAreas.Add(new List<Vector3>() { newVertices[counter] });
                                            nonRoofAreas[nonRoofAreas.Count - 1].AddRange(GetRangeOfVertices(allVertices, combinedVertexOrder, prev, next));
                                        }

                                        if (isSPrime)
                                        {
                                            // swap for s' (-> easier to merge triangles later)
                                            (prev, next) = (next, prev);
                                        }

                                        // if any other s'/e' has the same edge=(prev, next)... -> merge
                                        int temp = prev;
                                        if (edgeToPrimePoint.ContainsKey((prev, next)))
                                        {
                                            temp = edgeToPrimePoint[(prev, next)];  //... connect to previous s'/e'
                                            rays[temp].Remove(next);  //... and delete the ray from previous s'/e' to next

                                            // nonRoofAreas: remove own triangle and add the new point to nonRoofAreas
                                            if (roofShape != "mansard" && roofShape != "quadruple_saltbox")
                                            {
                                                // (always) add the side-triangle
                                                nonRoofAreas.RemoveAt(nonRoofAreas.Count - 1);
                                                int indexOtherArea = nonRoofAreas.FindIndex(x => x.Contains(newVertices[temp]));
                                                nonRoofAreas[indexOtherArea].Insert(isSPrime ? nonRoofAreas[indexOtherArea].Count - 2 : 0, newVertices[counter]);
                                            }
                                        }
                                        edgeToPrimePoint[(prev, next)] = counter;  // add own information to dict/overwrite it
                                        prev = temp;

                                        // add rays to outer (or previous s'/e' if merge was detected)
                                        rays[counter].Add(prev);
                                        rays[counter].Add(next);

                                        if (isSPrime)
                                        {
                                            // ray to e' (only necessary if hit-pos is a s')
                                            rays[counter].Add(counter - 1);
                                        }

                                        counter--;
                                    }
                                    // add a reference height for each ray (in roofDir)
                                    heights.Add((newVertices[counter + 1], 1 - Mathf.Sin(Mathf.Acos((detailLevel / (levelOfDetail + 1)) * 2 - 1))));
                                    if (roofShape == "saltbox")
                                    {
                                        // skip the second segmentation, so we end up like this:
                                        /*
                                            ------?---
                                            |__|__?__|  => ? = there would be the second segmentation
                                        */
                                        detailLevel = levelOfDetail;
                                    }
                                }


                                // add all ray-hits to the vertexOrder
                                (List<(Vector3, int)> allJumpPairs, Dictionary<int, int> newVerticesLookup) = Triangulator.AddRayHitsToPolygon(
                                    ref allVertices, ref combinedVertexOrder, rays, newVertices
                                );

                                try
                                {
                                    // now triangulate all sub-polygons
                                    IEnumerable<List<int>> allSubPolygons = Triangulator.GetSubPolygonVertexOrders(
                                        allVertices, combinedVertexOrder, newVerticesLookup, allJumpPairs
                                    );

                                    foreach (List<int> subPolygonVertexOrder in allSubPolygons)
                                    {
                                        List<int> triangles = Triangulator.Triangulate(allVertices, subPolygonVertexOrder);
                                        if (IsRoofArea(triangles, nonRoofAreas, allVertices))
                                        {
                                            roofTriangles.Add(triangles);
                                        }
                                        else
                                        {
                                            topTriangles.AddRange(triangles);
                                        }

                                    }
                                }
                                catch (ArgumentOutOfRangeException)
                                {
                                    Debug.LogError("Direct hit problem. Fall back to flat roof!");
                                    roofTriangles.Add(Triangulator.Triangulate(allVertices, combinedVertexOrder));
                                    addRoofBase = true;
                                    roofShape = "flat";
                                    roofStartHeight = totalHeight;
                                    break;
                                }


                                // calculate heights and add the new vertices (to extraRoofVertices)
                                float maxDist = VectorUtilities.DistancePointToInfiniteLine(S, roofDir, E) / (levelOfDetail + 1);
                                for (int i = 0; i < allVertices.Count; i++)
                                {
                                    heights.Sort((a, b) =>
                                    {
                                        float dA = VectorUtilities.DistancePointToInfiniteLine(a.Item1, roofDir, allVertices[i]);
                                        float dB = VectorUtilities.DistancePointToInfiniteLine(b.Item1, roofDir, allVertices[i]);
                                        return dA.CompareTo(dB);
                                    });
                                    float height = Mathf.Lerp(heights[0].Item2, heights[1].Item2, VectorUtilities.DistancePointToInfiniteLine(heights[0].Item1, roofDir, allVertices[i]) / maxDist) * (totalHeight - roofStartHeight);
                                    extraRoofVertices.Add(allVertices[i] - Vector3.up * height * height_multiplier);
                                }


                                addConnectionRing = true;  // we need it (s. gabled)
                            }
                            break;
                        case "pyramid":
                        case "pyramidal":
                        case "dome":
                            {
                                /* idea:
                                    - make rings like for a cylinder, but for each layer/height shift the vertices a bit into the center
                                    - special case top-most layer (because all vertices would be the same -> use roofTriangles instead of extraRoofRings)
                                */

                                // nr rings until center (inclusive center, so 1 is lowest = pyramid)
                                float levelOfDetail = roofShape == "pyramid" || roofShape == "pyramidal" ? 1 : 6;
                                for (float detailLevel = 1; detailLevel < levelOfDetail; detailLevel++)
                                {
                                    float angleInRad = (detailLevel / levelOfDetail) * Mathf.PI * 0.5f;
                                    float height = (1.0f - Mathf.Sin(angleInRad)) * (totalHeight - roofStartHeight);
                                    float cos = Mathf.Cos(angleInRad);  // percentage to shift inwards
                                    for (int i = 0; i < allVertices.Count; i++)
                                    {
                                        Vector3 shiftedVertex = center + (allVertices[i] - center) * cos;
                                        extraRoofVertices.Add(shiftedVertex - Vector3.up * height * height_multiplier);
                                    }
                                    extraRoofRings++;
                                }

                                // last add is special since wee only want 1x center
                                // add center + triangles all around to center
                                extraRoofVertices.Add(center);  // extraRoofVertices are placed with totalHeight as default height
                                for (int i = 0; i < combinedVertexOrder.Count; i++)
                                {
                                    roofTriangles.Add(new List<int>(){
                                    ((int)levelOfDetail - 1) * allVertices.Count + combinedVertexOrder[i],
                                    ((int)levelOfDetail - 1) * allVertices.Count + combinedVertexOrder[(i + 1) % combinedVertexOrder.Count],
                                    ((int)levelOfDetail - 1) * allVertices.Count + allVertices.Count
                                });
                                }
                                addRoofBase = true;
                            }
                            break;
                        case "onion":
                            {
                                /* idea:
                                    - make rings like for a cylinder, but for each layer/height shift the vertices a bit into the center (with a onion-function)
                                    - special case top-most layer (because all vertices would be the same -> use roofTriangles instead of extraRoofRings)
                                */

                                float levelOfDetail = 12;
                                for (float detailLevel = 0; detailLevel < levelOfDetail; detailLevel++)
                                {
                                    float height = (1.0f - detailLevel / levelOfDetail) * (totalHeight - roofStartHeight);
                                    float x = 1.0f - (detailLevel / levelOfDetail);
                                    float percentage = Mathf.Lerp(  // onion function
                                        Mathf.Lerp(0, 1, x),
                                        Mathf.Sin(x * (2 * (Mathf.PI / 3))),
                                        x
                                    );
                                    for (int i = 0; i < allVertices.Count; i++)
                                    {
                                        Vector3 shiftedVertex = center + (allVertices[i] - center) * percentage;
                                        extraRoofVertices.Add(shiftedVertex - Vector3.up * height * height_multiplier);
                                    }
                                    extraRoofRings++;
                                }

                                // last add is special since wee only want 1x center
                                // add center + triangles all around to center
                                extraRoofVertices.Add(center);  // extraRoofVertices are placed with totalHeight as default height
                                for (int i = 0; i < combinedVertexOrder.Count; i++)
                                {
                                    roofTriangles.Add(new List<int>(){
                                    (int)levelOfDetail * allVertices.Count + combinedVertexOrder[i],
                                    (int)levelOfDetail * allVertices.Count + combinedVertexOrder[(i + 1) % combinedVertexOrder.Count],
                                    (int)levelOfDetail * allVertices.Count + allVertices.Count
                                });
                                }

                                addRoofBase = true;
                            }
                            break;
                        case "skillion":
                            {
                                // simple flat triangulation (but on copied and upwards shifted vertices -> extraRoofVertices + addConnectionRing)
                                if (!feature.Properties.ContainsKey("roofDirection"))
                                {
                                    Debug.LogWarning("No roofDirection available! Used Flat roof as fallback");
                                    roofTriangles.Add(Triangulator.Triangulate(allVertices, combinedVertexOrder));
                                    addRoofBase = true;
                                    roofShape = "flat";
                                    roofStartHeight = totalHeight;
                                    break;
                                }
                                // simple flat triangulation (without polygon-segmentation)
                                roofTriangles.Add(Triangulator.Triangulate(allVertices, combinedVertexOrder));

                                // calculate height (and add new vertices)
                                float clockwiseAngleFromN = (float)(Math.PI / 180.0) * Convert.ToSingle(feature.Properties["roofDirection"]);
                                Vector3 roofDir = new Vector3(MathF.Sin(clockwiseAngleFromN), 0, MathF.Cos(clockwiseAngleFromN));
                                // by setting the reference-point very far away, we don't need to project the vector onto the roofDir
                                Vector3 refPointForDistance = allVertices[0] - roofDir * 100f;
                                List<float> distances = allVertices.ConvertAll(vertex => Vector3.Distance(refPointForDistance, vertex));
                                float minDistance = distances.Min();
                                float maxDistance = distances.Max();
                                for (int i = 0; i < allVertices.Count; i++)
                                {
                                    float height = ((distances[i] - minDistance) / (maxDistance - minDistance)) * (totalHeight - roofStartHeight);
                                    extraRoofVertices.Add(allVertices[i] - Vector3.up * height * height_multiplier);
                                }


                                addConnectionRing = true;  // obvious
                            }
                            break;
                        case "flat":
                            {
                                // simple flat triangulation (without polygon-segmentation)
                                roofTriangles.Add(Triangulator.Triangulate(allVertices, combinedVertexOrder));
                                addRoofBase = true;
                            }
                            break;
                        default:
                            Debug.Log($"roof-shape \"{roofShape}\" not implemented yet. Defaults to flat!");
                            roofTriangles.Add(Triangulator.Triangulate(allVertices, combinedVertexOrder));
                            addRoofBase = true;
                            roofShape = "flat";
                            roofStartHeight = totalHeight;
                            break;
                    }

                    // get all needed uv-information to start build the mesh:
                    // facade + nrOfLevels
                    Facade facade;
                    int nrOfLevels = feature.Properties.ContainsKey("levels") ? Convert.ToInt32(feature.Properties["levels"]) : (int)((roofStartHeight - minHeight) / 3);
                    nrOfLevels -= feature.Properties.ContainsKey("minLevel") ? Convert.ToInt32(feature.Properties["minLevel"]) : 0;
                    if (feature.Properties.ContainsKey("building"))
                    {
                        string buildingId = (string)feature.Properties["building"];
                        if (!perBuildingData.ContainsKey(buildingId))
                        {
                            perBuildingData[buildingId] = new PerBuildingID(
                                Facade.GenerateRandomFacade(
                                    feature.Properties.ContainsKey("type") ? (string)feature.Properties["type"] : "",
                                    height_multiplier
                                ),
                                center
                            );
                        }
                        center = perBuildingData[buildingId].sharedCenter;
                        facade = perBuildingData[buildingId].facade;
                    }
                    else
                    {
                        facade = Facade.GenerateRandomFacade(
                            feature.Properties.ContainsKey("type") ? (string)feature.Properties["type"] : "",
                            height_multiplier
                        );
                    }
                    // set (and later get) window and door texture-array-index
                    // set uvDepth if not already set (for window)
                    int nrSingles = texArrayPrecursor.Count((kv) => kv.Key.Item2 == WindowLayout.SINGLE);
                    int nrPairs = texArrayPrecursor.Count - nrSingles;
                    if (!texArrayPrecursor.ContainsKey((facade.window, facade.layout)) && texArrayPrecursor.Count <= MeshCalculator.MAX_TEXT_ARR_DEPTH &&
                        (!facade.facadeRule.isChurch || (facade.facadeRule.isChurch && churchWindowTextureIndex == -1)))  // generate only one church window texture
                    {
                        // generate new texture
                        RenderTexture mainTex = facade.GetWindowTexture(textureResolution, shaderMapping);
                        texArrayPrecursor[(facade.window, facade.layout)] = (
                            // use offset of ±1 since we use the sign() to evaluate from which tex2dArray to sample from in the shader (sign(±0)=0 -> undefined)
                            facade.layout == WindowLayout.SINGLE ? nrSingles + 1 : -(nrPairs + 1),
                            mainTex
                        );

                        if (facade.facadeRule.isChurch && churchWindowTextureIndex == -1)
                        {
                            churchWindowTextureIndex = texArrayPrecursor[(facade.window, facade.layout)].Item1;
                        }
                    }
                    // set uvDepth if not already set (for doors)
                    if (!texArrayPrecursorDoors.ContainsKey(facade.door) && texArrayPrecursorDoors.Count <= MeshCalculator.MAX_TEXT_ARR_DEPTH)
                    {
                        RenderTexture mainTex = facade.GetDoorTexture(textureResolution, shaderMapping);
                        texArrayPrecursorDoors[facade.door] = (
                            texArrayPrecursorDoors.Count,
                            mainTex
                        );
                    }

                    // facade color (-> used in mesh-info so we can use 1x material for all buildings)
                    string buildingColor = null;
                    if (feature.Properties.ContainsKey("color"))
                    {
                        buildingColor = (string)feature.Properties["color"];
                    }


                    // holes = [outer, hole1, hole2, ...]
                    holes.Add(float.MaxValue, outerVertices);
                    List<int> holeSizes = holes.Reverse().ToList().ConvertAll(x => x.Value.Count);

                    // build the mesh with all information
                    Mesh buildingMesh = MeshCalculator.CalculateFacadeMesh(
                        ref allVertices,
                        ref topTriangles,
                        ref bottomTriangles,
                        ref extraRoofVertices,
                        holeSizes,
                        isGroundLevel,
                        addConnectionRing,
                        minHeight,
                        roofStartHeight,
                        totalHeight,
                        height_multiplier,
                        center,
                        facade,
                        facade.layout == WindowLayout.SINGLE ? nrSingles + 1 : -(nrPairs + 1),
                        texArrayPrecursorDoors.Count,
                        churchWindowTextureIndex,
                        facade.facadeRule.isChurch ? 1 : nrOfLevels,
                        buildingColor
                    );

                    // same goes for the roof-mesh
                    string roofColor = null;
                    if (feature.Properties.ContainsKey("roofColor"))
                    {
                        roofColor = (string)feature.Properties["roofColor"];
                    }
                    Mesh roofMesh = MeshCalculator.CalculateRoofMesh(
                        ref allVertices, ref roofTriangles, ref extraRoofVertices,
                        extraRoofRings, addRoofBase, roofShape == "flat",
                        roofStartHeight, totalHeight, height_multiplier, center, roofColor
                    );


                    if (roofShape == "flat" && roofStartHeight - minHeight >= 10)
                    {
                        instanceHandler.TryAddInstance(ref allVertices, ref combinedVertexOrder, roofStartHeight * height_multiplier, center);
                    }

                    GameObject buildingObj = Instantiate(buildingPrefab, center, Quaternion.identity);
                    GameObject roofObj = Instantiate(roofPrefab, center, Quaternion.identity);

                    MeshFilter buildingMeshFilter = buildingObj.GetComponent<MeshFilter>();
                    // float heightDiff = latLonInfo.GetHeightMultiplier(configInfo) * configInfo.GetMaxHeightDiff();
                    // reset mesh bounds to prevent culling
                    Color heightColorSample = heightMap.GetPixel(
                        (int)CachedRequestMaker.mapValue(center.x, configInfo.tileSizeUnity * (tileOffset.Item1 - 0.5f), configInfo.tileSizeUnity * (tileOffset.Item1 + 0.5f), 0, 256),
                        (int)CachedRequestMaker.mapValue(center.z, configInfo.tileSizeUnity * (-tileOffset.Item2 - 0.5f), configInfo.tileSizeUnity * (-tileOffset.Item2 + 0.5f), 0, 256)
                    );
                    buildingMesh.bounds = new Bounds(
                        new Vector3(
                            buildingMesh.bounds.center.x,
                            buildingMesh.bounds.center.y + CachedRequestMaker.HeightFromRGB(heightColorSample) * height_multiplier,
                            buildingMesh.bounds.center.z),
                        buildingMesh.bounds.size
                    );
                    buildingMeshFilter.mesh = buildingMesh;

                    MeshFilter roofMeshFilter = roofObj.GetComponent<MeshFilter>();
                    // reset mesh bounds to prevent culling
                    roofMesh.bounds = new Bounds(
                        new Vector3(
                            roofMesh.bounds.center.x,
                            roofMesh.bounds.center.y + CachedRequestMaker.HeightFromRGB(heightColorSample) * height_multiplier,
                            roofMesh.bounds.center.z),
                        roofMesh.bounds.size
                    );
                    roofMeshFilter.mesh = roofMesh;

                    MeshRenderer buildingMeshRenderer = buildingObj.GetComponent<MeshRenderer>();
                    buildingMeshRenderer.sharedMaterial = sharedBuildingMaterial;

                    MeshRenderer roofMeshRenderer = roofObj.GetComponent<MeshRenderer>();
                    roofMeshRenderer.sharedMaterial = sharedRoofMaterial;

                    // TODO: remove this information
                    // --->
                    // add debug information to building
                    GameObject go = new GameObject();
                    go.transform.SetParent(buildingObj.transform);
                    go.name = $"{outer.Coordinates[0].Latitude}; {outer.Coordinates[0].Longitude}".Replace(',', '.').Replace(';', ',');

                    Action<string, string> addDebugObj = (key, value) =>
                    {
                        GameObject go = new GameObject();
                        go.transform.SetParent(buildingObj.transform);
                        go.name = $"{key}: {value}";
                    };
                    foreach (var (k, val) in feature.Properties)
                    {
                        addDebugObj(k, val.ToString());
                    }
                    // <---
                }
                catch (System.Exception e)
                {
                    // catch corrupted data
                    Debug.LogError(e);
                }
            }
        }

        instanceHandler.SetupRenderer(latLonInfo.GetHeightMultiplier(configInfo), configInfo.tileSizeUnity, heightMap);

        if (texArrayPrecursor.Count == 0 || texArrayPrecursorDoors.Count == 0)
        {
            return;
        }

        // set upo the Texture2DArray's for the shared material
        // use offset of ±1 since we use the sign() to evaluate from which tex2dArray to sample from in the shader (sign(±0)=0 -> undefined)
        int nrOfSingles = texArrayPrecursor.Count((kv) => kv.Key.Item2 == WindowLayout.SINGLE);
        int nrOfPairs = texArrayPrecursor.Count - nrOfSingles;
        Texture2DArray mainTextures = new Texture2DArray(textureResolution, textureResolution, MeshCalculator.ClampUvDepth(nrOfSingles + 1), TextureFormat.ARGB32, true, false);
        Texture2DArray normalMaps = new Texture2DArray(textureResolution, textureResolution, MeshCalculator.ClampUvDepth(nrOfSingles + 1), TextureFormat.ARGB32, true, false);

        Texture2DArray mainTexturesPaired = new Texture2DArray((int)(textureResolution * 1.75f), textureResolution, MeshCalculator.ClampUvDepth(nrOfPairs + 1), TextureFormat.ARGB32, true, false);
        Texture2DArray normalMapsPaired = new Texture2DArray((int)(textureResolution * 1.75f), textureResolution, MeshCalculator.ClampUvDepth(nrOfPairs + 1), TextureFormat.ARGB32, true, false);
        foreach (var keyValuePair in texArrayPrecursor)
        {
            (Window _, WindowLayout layout) = keyValuePair.Key;
            (int i, RenderTexture mainTex) = keyValuePair.Value;

            if (Math.Abs(i) >= MeshCalculator.MAX_TEXT_ARR_DEPTH)
            {
                break;
            }

            if (layout == WindowLayout.SINGLE)
            {
                mainTextures.SetPixels(CachedRequestMaker.RenderTexToTex2D(mainTex).GetPixels(), i);
                normalMaps.SetPixels(CachedRequestMaker.CalculateNormalTexture(mainTex, textureResolution, shaderMapping).GetPixels(), i);
            }
            else
            {
                mainTexturesPaired.SetPixels(CachedRequestMaker.RenderTexToTex2D(mainTex).GetPixels(), Math.Abs(i));
                normalMapsPaired.SetPixels(CachedRequestMaker.CalculateNormalTexture(mainTex, textureResolution, shaderMapping).GetPixels(), Math.Abs(i));
            }
        }

        Texture2DArray mainTexturesDoor = new Texture2DArray(textureResolution, textureResolution, MeshCalculator.ClampUvDepth(texArrayPrecursorDoors.Count), TextureFormat.ARGB32, true, false);
        Texture2DArray normalMapsDoor = new Texture2DArray(textureResolution, textureResolution, MeshCalculator.ClampUvDepth(texArrayPrecursorDoors.Count), TextureFormat.ARGB32, true, false);
        foreach ((int i, RenderTexture mainTexDoor) in texArrayPrecursorDoors.Values)
        {
            if (i >= MeshCalculator.MAX_TEXT_ARR_DEPTH)
            {
                break;
            }

            mainTexturesDoor.SetPixels(CachedRequestMaker.RenderTexToTex2D(mainTexDoor).GetPixels(), i);
            normalMapsDoor.SetPixels(CachedRequestMaker.CalculateNormalTexture(mainTexDoor, textureResolution, shaderMapping).GetPixels(), i);
        }

        mainTextures.Apply();
        normalMaps.Apply();
        mainTexturesPaired.Apply();
        normalMapsPaired.Apply();
        mainTexturesDoor.Apply();
        normalMapsDoor.Apply();

        sharedBuildingMaterial.SetTexture("_MainTex", mainTextures);
        sharedBuildingMaterial.SetTexture("_NormalMap", normalMaps);
        sharedBuildingMaterial.SetTexture("_MainTexPaired", mainTexturesPaired);
        sharedBuildingMaterial.SetTexture("_NormalMapPaired", normalMapsPaired);
        sharedBuildingMaterial.SetTexture("_MainTexDoor", mainTexturesDoor);
        sharedBuildingMaterial.SetTexture("_NormalMapDoor", normalMapsDoor);
        sharedBuildingMaterial.SetTexture("_StairTex", stairTexture);


        // clean up
        foreach ((int _, RenderTexture mainTex) in texArrayPrecursor.Values)
        {
            mainTex.Release();
            mainTex.DiscardContents();
            mainTex.Destroy();
        }
        texArrayPrecursor = null;
        foreach ((int _, RenderTexture mainTex) in texArrayPrecursorDoors.Values)
        {
            mainTex.Release();
            mainTex.DiscardContents();
            mainTex.Destroy();
        }
        texArrayPrecursorDoors = null;
    }

    private List<Vector3> ToScenePositions(LineString lineString, bool isInner = false)
    {
        List<Vector3> positions = new List<Vector3>(lineString.Coordinates.Count - 1);

        // to scene coordinates
        for (int i = 0; i < lineString.Coordinates.Count - 1; i++)  // -1 since 1st == last
        {
            Position pos = (Position)lineString.Coordinates[i];
            positions.Add(latLonInfo.OtherToScenePos(pos.Latitude, pos.Longitude, configInfo));
        }
        List<int> vertexOrder = Enumerable.Range(0, positions.Count).ToList();
        // set winding order to be clockwise for outer line-string and counter-cw for inner
        if ((!PolygonUtilities.IsInClockwiseWindingOrder(positions, vertexOrder) && !isInner) || ((PolygonUtilities.IsInClockwiseWindingOrder(positions, vertexOrder) && isInner)))
        {
            positions.Reverse();
        }
        // remove vertices on one line (otherwise the triangulate algo won't work)
        HashSet<int> onOneLine = PolygonUtilities.AdjacentVerticesOnOneLine(positions, vertexOrder);
        if (onOneLine.Count > 0)
        {
            onOneLine.Reverse();
            foreach (int index in onOneLine)
            {
                positions.RemoveAt(index);
            }
        }

        return positions;
    }


    private Vector3 CalculateMidPos(List<Vector3> vertices, Vector3 roofDirection)
    {
        /* Idea: find min and max distance from the roofDirection-line ( going through the origin (0|0) )
                 and use the mid point between min and max as first part of the center (M). Make the same
                 with the orthogonal line to the roofDirection-line to geth the second part:

            Origin ---roofDirectionLine-->
               |
           orthogonal   0---min---1
              line      |         |
               |    minCross M maxCross
               |        |         |
               |        0---max---1
               V
        */

        Vector3 cross = Vector3.Cross(roofDirection, Vector3.up);

        float minDist = float.MaxValue;
        float maxDist = float.MinValue;
        Vector3 toMin = Vector3.zero;
        Vector3 toMax = Vector3.zero;

        float minDistCross = float.MaxValue;
        float maxDistCross = float.MinValue;
        Vector3 toMinCross = Vector3.zero;
        Vector3 toMaxCross = Vector3.zero;
        foreach (Vector3 vertex in vertices)
        {
            Vector3 distVec = VectorUtilities.VectorInfiniteLineToPoint(Vector3.zero, roofDirection, vertex);
            float dist = Vector3.Dot(distVec, cross) * distVec.magnitude;
            if (dist < minDist)
            {
                minDist = dist;
                toMin = distVec;
            }
            if (dist > maxDist)
            {
                maxDist = dist;
                toMax = distVec;
            }

            Vector3 distVecCross = VectorUtilities.VectorInfiniteLineToPoint(Vector3.zero, cross, vertex);
            float distCross = Vector3.Dot(distVecCross, roofDirection) * distVecCross.magnitude;
            if (distCross < minDistCross)
            {
                minDistCross = distCross;
                toMinCross = distVecCross;
            }
            if (distCross > maxDistCross)
            {
                maxDistCross = distCross;
                toMaxCross = distVecCross;
            }
        }

        return (toMin + (toMax - toMin) * 0.5f) + (toMinCross + (toMaxCross - toMinCross) * 0.5f);
    }

    private (int, int) FindMinMaxVertices(List<Vector3> vertices, Vector3 roofDirection)
    {
        /* Idea: find min and max distance from the orthogonal roofDirection-line ( going through the origin (0|0) )

            Origin ---roofDirectionLine-->
               |
           orthogonal   0---------1
              line      |         |
               |       min       max
               |        |         |
               |        0---------1
               V
        */

        Vector3 cross = Vector3.Cross(roofDirection, Vector3.up);

        float minDistCross = float.MaxValue;
        float maxDistCross = float.MinValue;
        int minIndex = -1;
        int maxIndex = -1;
        for (int i = 0; i < vertices.Count; i++)
        {
            Vector3 distVecCross = VectorUtilities.VectorInfiniteLineToPoint(Vector3.zero, cross, vertices[i]);
            float distCross = Vector3.Dot(distVecCross, roofDirection) * distVecCross.magnitude;
            if (distCross < minDistCross)
            {
                minDistCross = distCross;
                minIndex = i;
            }
            if (distCross > maxDistCross)
            {
                maxDistCross = distCross;
                maxIndex = i;
            }
        }

        return (minIndex, maxIndex);
    }


    private int FindPreviousIndex(List<Vector3> vertices, List<int> vertexOrder, int nrOuterVertices, int start, int end, Vector3 referencePoint, Vector3 direction, int increment = 1)
    {
        /* Example:
                  0------>----------1
                  |                 |
                  |      3----<-----2
            dir   |      |
            <--   S      E
                  |  ..  |

            -> start at S+1 = 0 and go along the order until a point is on the other side of the half-plane of E (normal=roofDir)
            -> use this point as previous of E
            -> after that, check for intersections:
                -> intersection with 2->3
                -> correct to 3 as previous of E
        */

        List<int> outerIndices = new List<int>();
        int startPrime = start + (increment < 0 && start < end ? vertexOrder.Count : 0);  // extended start
        int endPrime = end + (increment > 0 && end < start ? vertexOrder.Count : 0);  // extended end
        for (int i = startPrime; i != endPrime + increment; i += increment)
        {
            int index = i % vertexOrder.Count;
            // skip hole vertex (since S/E are always outer vertices, so for prev/next from S/E the same must hold true)
            if (vertexOrder[index] < nrOuterVertices)
            {
                outerIndices.Add(index);
            }
        }

        for (int i = 0; i < outerIndices.Count; i++)
        {
            Vector3 vertex = vertices[vertexOrder[outerIndices[i]]];
            // check if we reached other side of the half-plane
            if (Vector3.Dot(direction, (vertex - referencePoint).normalized) < 0)
            {
                Vector3 prevVertex = vertices[vertexOrder[outerIndices[(i - increment + outerIndices.Count) % outerIndices.Count]]];
                Vector3 nextVertex = vertices[vertexOrder[outerIndices[(i + increment + outerIndices.Count) % outerIndices.Count]]];

                // check  for this error
                /* a___
                        °°°---___b
                        c---°°°  /
                                /
                            E
                */
                Vector3 toPrevNorm = (prevVertex - vertex).normalized;
                Vector3 toNextNorm = (nextVertex - vertex).normalized;
                bool isConvex = VectorUtilities.IsConvex(toPrevNorm, toNextNorm);
                if (isConvex)
                {
                    float dot = Vector3.Dot(toPrevNorm, toNextNorm);
                    float otherDot = Vector3.Dot(toPrevNorm, (referencePoint - vertex).normalized);
                    if (dot > otherDot)
                    {
                        continue;
                    }
                }
                // if concave/reflex the error isn't possible (because of the setup)
                return outerIndices[i];
            }
        }
        // should never happen
        return outerIndices[outerIndices.Count - 1];
    }


    private bool IsRoofArea(List<int> triangles, List<List<Vector3>> nonRoofAreas, List<Vector3> vertices)
    {
        Vector3 centerOfTriangle = Vector3.zero;
        for (int i = 0; i < 3; i++)
        {
            centerOfTriangle += vertices[triangles[i]];
        }
        centerOfTriangle /= 3.0f;
        bool isRoofArea = true;
        foreach (List<Vector3> nonRoofArea in nonRoofAreas)
        {
            if (PolygonUtilities.PointInPolygon(centerOfTriangle, nonRoofArea, Enumerable.Range(0, nonRoofArea.Count).ToList()))
            {
                isRoofArea = false;
                break;
            }
        }
        return isRoofArea;
    }

    private List<Vector3> GetRangeOfVertices(List<Vector3> vertices, List<int> vertexOrder, int start, int end)
    {
        List<int> subVertexOrder = new List<int>();
        if (start <= end)
        {
            subVertexOrder = vertexOrder.GetRange(start, end - start + 1);
        }
        else
        {
            subVertexOrder = vertexOrder.GetRange(start, vertexOrder.Count - start);
            subVertexOrder.AddRange(vertexOrder.GetRange(0, end + 1));
        }

        return subVertexOrder.ConvertAll(x => vertices[x]);
    }

    private bool IsHullBuilding(List<Vector3> outerVertices, Dictionary<string, (HashSet<Vector3>, float)> hullLookup)
    {
        // idea: hullArea ~=~ area of all building parts;
        float hullArea = PolygonUtilities.CalculateAreaOfPolygon(outerVertices, Enumerable.Range(0, outerVertices.Count).ToList());
        foreach ((string buildingID, (HashSet<Vector3> partVertices, float area)) in hullLookup)
        {
            // check if hullArea is close to area of the sum of all building-part-areas
            if (MathF.Abs(area - hullArea) < 0.1)
            {
                // check if hull location matches building location
                int nrSameVertices = 0;
                foreach (Vector3 hullVertex in outerVertices)
                {
                    if (partVertices.Contains(hullVertex))
                    {
                        nrSameVertices++;
                    }
                }
                // >=60% of all vertices are covered by building parts
                if (nrSameVertices >= 0.6f * outerVertices.Count)
                {
                    return true;
                }
            }
        }

        return false;
    }

    // seems to doesn't work
    // private static void FindSmallestSupportedTextureFormats()
    // {
    //     GraphicsFormat[] possibleFormats = new GraphicsFormat[]{
    //         GraphicsFormat.B4G4R4A4_UNormPack16,
    //         GraphicsFormat.R8G8B8A8_UNorm,
    //         GraphicsFormat.R8G8B8A8_SNorm,
    //         GraphicsFormat.R8G8B8A8_UInt,
    //         GraphicsFormat.R8G8B8A8_SInt,
    //         GraphicsFormat.A2R10G10B10_UNormPack32,
    //     };

    //     foreach (GraphicsFormat format in possibleFormats)
    //     {
    //         GraphicsFormat supportedFormat = SystemInfo.GetCompatibleFormat(GraphicsFormat.B4G4R4A4_UNormPack16, FormatUsage.Render);
    //         if (supportedFormat != GraphicsFormat.None &&
    //             SystemInfo.IsFormatSupported(supportedFormat, FormatUsage.GetPixels) &&
    //             SystemInfo.IsFormatSupported(supportedFormat, FormatUsage.ReadPixels) &&
    //             SystemInfo.IsFormatSupported(supportedFormat, FormatUsage.Sample)
    //         )
    //         {
    //             BuildingGenerator.smallestSupportedTextureFormat = format;
    //             return;
    //         }
    //     }

    //     throw new Exception("All possible texture formats are not supported!");
    // }
}