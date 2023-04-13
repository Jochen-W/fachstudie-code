using UnityEngine;
using System.Collections.Generic;


public class InstanceHandler : MonoBehaviour
{
    static System.Random rnd = new System.Random();
    new Renderer renderer;
    Mesh mesh;
    Material material;
    MaterialPropertyBlock mpb = null;
    List<Matrix4x4> trsList;
    List<Vector4> heightLookupPosList;

    public InstanceHandler()
    {
        this.trsList = new List<Matrix4x4>();
        this.heightLookupPosList = new List<Vector4>();
    }

    public void Awake()
    {
        this.renderer = GetComponentInChildren<MeshRenderer>();
        this.mesh = GetComponentInChildren<MeshFilter>().mesh;
        this.material = GetComponentInChildren<MeshRenderer>().material;
    }

    public void TryAddInstance(ref List<Vector3> vertices, ref List<int> vertexOrder, float height, Vector3 heightLookupPos)
    {
        if (InstanceHandler.rnd.Next(100) > 50)  // 50% chance
        {
            return;
        }

        // add only in corners (1x per building)
        for (int i = 0; i < vertexOrder.Count; i++)
        {
            Vector3 vPrev = vertices[vertexOrder[(i - 1 + vertexOrder.Count) % vertexOrder.Count]];
            Vector3 v = vertices[vertexOrder[i]];
            Vector3 vNext = vertices[vertexOrder[(i + 1) % vertexOrder.Count]];

            Vector3 to_prev = vPrev - v;
            Vector3 to_next = vNext - v;
            float angleDeg = Vector3.SignedAngle(to_next, to_prev, Vector3.up);

            if (
                !VectorUtilities.IsConvex(to_prev, to_next) ||          // reflex corner
                Mathf.Abs(angleDeg - 90) > 10 ||                        // no ca. 90° corner
                to_prev.sqrMagnitude < 1 || to_next.sqrMagnitude < 1    // triangle/corner is to small
            )
            {
                continue;
            }

            Vector3 midDir = (to_prev.normalized + to_next.normalized).normalized;

            if (!PolygonUtilities.PointInPolygon(v + midDir * 4, vertices, vertexOrder))
            {
                continue;
            }

            Vector3 pos = v + midDir * 2;
            pos.y = height;
            Quaternion rot = Quaternion.Euler(0, -Mathf.Atan2(to_prev.z, to_prev.x) * Mathf.Rad2Deg, 0);
            Vector3 scale = new Vector3((float)InstanceHandler.rnd.NextDouble() + 0.5f, 1, (float)InstanceHandler.rnd.NextDouble() + 0.5f);
            Matrix4x4 trs = Matrix4x4.TRS(pos, rot, scale);
            this.trsList.Add(trs);
            this.heightLookupPosList.Add(new Vector4(heightLookupPos.x, 0, heightLookupPos.z, 0));
            return;  // only 1x instance per roof/mesh
        }
    }

    public void SetupRenderer(float height_multiplier, float tileSizeUnity, Texture2D heightMap)
    {
        // deactivate own mesh-render (disabled renderer can still instantiate)
        this.renderer.enabled = false;

        if (this.heightLookupPosList.Count <= 0)
        {
            // if there is no instance to render, we can skip the setup
            return;
        }

        this.mpb = new MaterialPropertyBlock();
        this.mpb.SetVectorArray("_HeightLookupPoint", this.heightLookupPosList);
        this.renderer.SetPropertyBlock(this.mpb);

        this.material.SetFloat("_HeightMultiplier", height_multiplier);
        this.material.SetFloat("_TileSize", tileSizeUnity);
        this.material.SetFloat("_InverseTileSize", 1.0f / tileSizeUnity);
        this.material.SetTexture("_HeightMap", heightMap);
        // this.material.SetTexture("_MainTex", ...);

        this.renderer.sharedMaterial = this.material;
    }

    void Update()
    {
        if (this.heightLookupPosList.Count <= 0)
        {
            // no instance to render
            return;
        }

        Graphics.DrawMeshInstanced(this.mesh, 0, this.renderer.sharedMaterial, this.trsList, this.mpb);
    }
}