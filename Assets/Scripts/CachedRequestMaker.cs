using UnityEngine;
using UnityEngine.Networking;
using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json;
using GeoJSON.Net.Feature;


public enum TileType
{
    MAP,
    NORMAL,
    ELEVATION,

    // BUILDING
}


public class CachedRequestMaker : MonoBehaviour
{

    private static readonly string baseUrlMap = "https://tile.openstreetmap.org";
    private static readonly string baseUrlNormals = "https://s3.amazonaws.com/elevation-tiles-prod/normal";
    private static readonly string baseUrlElevation = "https://s3.amazonaws.com/elevation-tiles-prod/terrarium";
    private static readonly string baseUrlBuildings = "https://data.osmbuildings.org/0.2/anonymous/tile";

    private static readonly Dictionary<TileType, string> tileTypeToUrl = new Dictionary<TileType, string>(){
        {TileType.MAP, baseUrlMap},
        {TileType.NORMAL, baseUrlNormals},
        {TileType.ELEVATION, baseUrlElevation}
    };

    private static Dictionary<(TileType, int, int, int), Texture2D> textureCache = new Dictionary<(TileType, int, int, int), Texture2D>();
    private static Dictionary<(int, int), Texture2D> texturePool = new Dictionary<(int, int), Texture2D>();

    public static async Task<FeatureCollection> GetBuildingTileData(ConfigInfo configInfo, int tile_x, int tile_y)
    {
        string uri = $"{baseUrlBuildings}/{configInfo.zoom}/{tile_x}/{tile_y}.json";
        // Debug.Log(uri);
        using (UnityWebRequest webRequest = UnityWebRequest.Get(uri))
        {
            UnityWebRequestAsyncOperation operation = webRequest.SendWebRequest();

            while (!operation.isDone)
                await Task.Yield();

            if (webRequest.result != UnityWebRequest.Result.Success)
            {
                Debug.Log(webRequest.error);
            }

            FeatureCollection collection = JsonConvert.DeserializeObject<FeatureCollection>(webRequest.downloadHandler.text);

            return collection;
        }
    }


    public static async Task<Texture2D> GetTextureTileData(ConfigInfo configInfo, int tile_x, int tile_y, TileType tile_type, int other_zoom = -1, bool cacheTexture = true)
    {
        int zoom = other_zoom;
        if (other_zoom == -1)
        {
            zoom = configInfo.zoom;
        }

        if (CachedRequestMaker.textureCache.ContainsKey((tile_type, zoom, tile_x, tile_y)))
        {
            return CachedRequestMaker.textureCache[(tile_type, zoom, tile_x, tile_y)];
        }

        string baseUrl = CachedRequestMaker.tileTypeToUrl[tile_type];
        string uri = $"{baseUrl}/{zoom}/{tile_x}/{tile_y}.png";
        // Debug.Log(uri);
        using (UnityWebRequest webRequest = UnityWebRequestTexture.GetTexture(uri))
        {
            UnityWebRequestAsyncOperation operation = webRequest.SendWebRequest();

            while (!operation.isDone)
                await Task.Yield();

            if (webRequest.result != UnityWebRequest.Result.Success)
            {
                Debug.Log(webRequest.error);
            }

            Texture2D texture = DownloadHandlerTexture.GetContent(webRequest);
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;

            if (cacheTexture)
            {
                CachedRequestMaker.textureCache.TryAdd((tile_type, zoom, tile_x, tile_y), texture);
            }
            return texture;
        }
    }

    public static async Task<Texture2D> GetHigherResTextureTileDataAsTex2D(ConfigInfo configInfo, int tile_x, int tile_y, TileType tile_type, int zoom, int targetZoom, ShaderMapping shaderMapping)
    {
        RenderTexture mergedTex = await CachedRequestMaker.GetHigherResTextureTileData(configInfo, tile_x, tile_y, tile_type, zoom, targetZoom, shaderMapping);

        Texture2D tex2d = new Texture2D(mergedTex.width, mergedTex.height, TextureFormat.ARGB32, true, false);
        RenderTexture.active = mergedTex;
        tex2d.ReadPixels(new Rect(0, 0, mergedTex.width, mergedTex.height), 0, 0);
        tex2d.Apply();

        RenderTexture.active = null;
        mergedTex.Release();
        mergedTex.DiscardContents();
        mergedTex.Destroy();
        mergedTex = null;

        return tex2d;
    }

    private static async Task<RenderTexture> GetHigherResTextureTileData(ConfigInfo configInfo, int tile_x, int tile_y, TileType tile_type, int zoom, int targetZoom, ShaderMapping shaderMapping)
    {
        if (targetZoom == zoom)
        {
            Texture2D tex = await CachedRequestMaker.GetTextureTileData(configInfo, tile_x, tile_y, tile_type, targetZoom, false);
            RenderTexture rtex = new RenderTexture(tex.width, tex.height, 0, RenderTextureFormat.ARGB32);
            rtex.enableRandomWrite = true;
            Graphics.Blit(tex, rtex);

            Texture2D.Destroy(tex);
            tex = null;

            return rtex;
        }
        // else
        RenderTexture[] textures = await Task<RenderTexture[]>.WhenAll(new Task<RenderTexture>[]{
            CachedRequestMaker.GetHigherResTextureTileData(configInfo, tile_x * 2, tile_y * 2, tile_type, zoom + 1, targetZoom, shaderMapping),
            CachedRequestMaker.GetHigherResTextureTileData(configInfo, tile_x * 2 + 1, tile_y * 2, tile_type, zoom + 1, targetZoom, shaderMapping),
            CachedRequestMaker.GetHigherResTextureTileData(configInfo, tile_x * 2, tile_y * 2 + 1, tile_type, zoom + 1, targetZoom, shaderMapping),
            CachedRequestMaker.GetHigherResTextureTileData(configInfo, tile_x * 2 + 1, tile_y * 2 + 1, tile_type, zoom + 1, targetZoom, shaderMapping),
        });

        RenderTexture mergedTex = CachedRequestMaker.MergeTextures(textures[0], textures[1], textures[2], textures[3], shaderMapping);

        RenderTexture.active = null;
        foreach (var tex in textures)
        {
            tex.Release();
            tex.DiscardContents();
            tex.Destroy();
        }

        return mergedTex;
    }

    private static RenderTexture MergeTextures(RenderTexture tl, RenderTexture tr, RenderTexture bl, RenderTexture br, ShaderMapping shaderMapping)
    {
        ComputeShader shader = shaderMapping.GetShaderByType(ShaderType.TextureQuadMerger);

        RenderTexture mergedTex = new RenderTexture(tl.width * 2, tl.height * 2, 0, RenderTextureFormat.ARGB32);
        mergedTex.enableRandomWrite = true;

        shader.SetInt("base_resolution", tl.width);

        foreach ((string name, RenderTexture tex) in new (string, RenderTexture)[] { ("tl", tl), ("tr", tr), ("bl", bl), ("br", br) })
        {
            shader.SetTexture(shader.FindKernel("CSMain"), name, tex);
        }

        shader.SetTexture(shader.FindKernel("CSMain"), "Result", mergedTex);
        shader.Dispatch(shader.FindKernel("CSMain"), Mathf.CeilToInt(tl.width * 2 / 8.0f), Mathf.CeilToInt(tl.height * 2 / 8.0f), 1);

        return mergedTex;
    }
    public static void ClearCache()
    {
        foreach (var item in CachedRequestMaker.textureCache.Values)
        {
            Texture2D.Destroy(item);
        }
        CachedRequestMaker.textureCache = new Dictionary<(TileType, int, int, int), Texture2D>();
    }

    public static float HeightFromRGB(Color c)
    {
        // from https://www.mapzen.com/blog/terrain-tile-service
        return ((c.r * 255.0f) * 256.0f + (c.g * 255.0f) + (c.b * 255.0f) / 256.0f) - 32768.0f;
    }


    public static float mapValue(float n, float start1, float stop1, float start2, float stop2)
    {
        return ((n - start1) / (stop1 - start1)) * (stop2 - start2) + start2;
    }

    public static Texture2D RenderTexToTex2D(RenderTexture tex)
    {
        if (!CachedRequestMaker.texturePool.ContainsKey((tex.width, tex.height)))
        {
            CachedRequestMaker.texturePool[(tex.width, tex.height)] = new Texture2D(tex.width, tex.height, TextureFormat.ARGB32, false, false);
        }
        Texture2D tex2d = CachedRequestMaker.texturePool[(tex.width, tex.height)];
        // ReadPixels looks at the active RenderTexture.
        RenderTexture.active = tex;
        tex2d.ReadPixels(new Rect(0, 0, tex.width, tex.height), 0, 0);
        tex2d.Apply(true);
        // tex2d.Compress(true);

        RenderTexture.active = null;
        return tex2d;
    }


    public static Texture2D CalculateNormalTexture(RenderTexture texture, int resolution, ShaderMapping shaderMapping)
    {
        ComputeShader normalsShader = shaderMapping.GetShaderByType(ShaderType.NormalMapCalculator);
        RenderTexture normalsTexture = new RenderTexture(texture.width, texture.height, 0, RenderTextureFormat.ARGB32);
        normalsTexture.enableRandomWrite = true;
        normalsTexture.wrapMode = TextureWrapMode.Repeat;
        normalsTexture.Create();

        normalsShader.SetVector("resolution", new Vector2(texture.width, texture.height));
        normalsShader.SetTexture(normalsShader.FindKernel("CSMain"), "Input", texture);

        normalsShader.SetTexture(normalsShader.FindKernel("CSMain"), "Result", normalsTexture);
        normalsShader.Dispatch(normalsShader.FindKernel("CSMain"), Mathf.CeilToInt(texture.width / 8.0f), Mathf.CeilToInt(texture.height / 8.0f), 1);

        var texture2D = CachedRequestMaker.RenderTexToTex2D(normalsTexture);

        normalsTexture.Release();
        normalsTexture.DiscardContents();
        normalsTexture.Destroy();
        normalsTexture = null;

        return texture2D;
    }

    // public static Texture2D CopyTexture(Texture2D toCopy)
    // {
    //     Texture2D copy = new Texture2D(toCopy.width, toCopy.height, TextureFormat.ARGB32, true, false);
    //     copy.wrapMode = toCopy.wrapMode;
    //     copy.filterMode = toCopy.filterMode;
    //     copy.SetPixels(toCopy.GetPixels());
    //     copy.Apply();
    //     return copy;
    // }
}
