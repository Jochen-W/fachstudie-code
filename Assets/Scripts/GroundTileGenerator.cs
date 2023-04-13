using UnityEngine;
using System.Threading.Tasks;

public class GroundTileGenerator : MonoBehaviour
{
    public GameObject tilePrefab;
    public LatLonInfo latLonInfo;
    public ConfigInfo configInfo;
    public ShaderMapping shaderMapping;

    async void Awake()
    {
        (double mapX, double mapY) = Mercator.LatLonToXY(latLonInfo.latitude, latLonInfo.longitude);
        (int tile_x, int tile_y) = Mercator.XYToTileXY(mapX, mapY, configInfo.zoom);

        for (int y = -configInfo.radius; y < configInfo.radius + 1; y++)
        {
            for (int x = -configInfo.radius; x < configInfo.radius + 1; x++)
            {
                Vector3 pos = new Vector3(configInfo.tileSizeUnity * x, 0, -configInfo.tileSizeUnity * y);
                GameObject tile = Instantiate(tilePrefab, pos, Quaternion.identity);
                tile.transform.localScale = new Vector3(configInfo.tileSizeUnity / 10, 1, configInfo.tileSizeUnity / 10);
                // reset mesh bounds to prevent culling
                MeshFilter meshFilter = tile.GetComponent<MeshFilter>();
                float heightDiff = latLonInfo.GetHeightMultiplier(configInfo) * configInfo.GetMaxHeightDiff();
                meshFilter.mesh.bounds = new Bounds(new Vector3(0.0f, heightDiff / 2.0f, 0.0f), new Vector3(meshFilter.mesh.bounds.size.x, heightDiff, meshFilter.mesh.bounds.size.z));

                Texture2D[] textures = await Task.WhenAll(
                    CachedRequestMaker.GetHigherResTextureTileDataAsTex2D(configInfo, tile_x + x, tile_y + y, TileType.MAP, configInfo.zoom, configInfo.zoomForTexture, shaderMapping),
                    CachedRequestMaker.GetTextureTileData(configInfo, tile_x + x, tile_y + y, TileType.NORMAL),
                    CachedRequestMaker.GetTextureTileData(configInfo, tile_x + x, tile_y + y, TileType.ELEVATION),
                    CachedRequestMaker.GetTextureTileData(configInfo, tile_x + x + 1, tile_y + y, TileType.ELEVATION),
                    CachedRequestMaker.GetTextureTileData(configInfo, tile_x + x, tile_y + y + 1, TileType.ELEVATION)
                );

                MeshRenderer meshRenderer = tile.GetComponent<MeshRenderer>();
                meshRenderer.material.SetTexture("_MainTex", textures[0]);
                meshRenderer.material.SetTexture("_NormalMap", textures[1]);
                meshRenderer.material.SetTexture("_HeightMap", textures[2]);
                // for seamless edges
                meshRenderer.material.SetTexture("_HeightMapRight", textures[3]);
                meshRenderer.material.SetTexture("_HeightMapBelow", textures[4]);
                meshRenderer.material.SetFloat("_UseRight", 1.0f);  //true
                meshRenderer.material.SetFloat("_UseBelow", 1.0f);  //true

                meshRenderer.material.SetFloat("_HeightMultiplier", latLonInfo.GetHeightMultiplier(configInfo));
            }
        }
    }
}
