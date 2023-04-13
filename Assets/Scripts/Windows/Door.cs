using UnityEngine;


public class Door
{
    // private static Dictionary<(Door, int), (RenderTexture, RenderTexture)> textureCache = new Dictionary<(Door, int), (RenderTexture, RenderTexture)>();

    // from left to right: sync2=00100, sync3=01010, smallG=01000, SGS=10001
    int nrOfVerticalSubdivisions;
    int nrOfHorizontalSubdivisions;
    int nrOfGlasses;
    bool isFlipped;
    bool hasRoundTop;

    public Door()
    {
        this.nrOfVerticalSubdivisions = 0;
        this.nrOfHorizontalSubdivisions = 0;
        this.nrOfGlasses = 0;
        this.isFlipped = false;
        this.hasRoundTop = false;
    }

    public Door(int nrOfVerticalSubdivisions, int nrOfHorizontalSubdivisions, int nrOfGlasses, bool isFlipped, bool hasRoundTop)
    {
        this.nrOfVerticalSubdivisions = nrOfVerticalSubdivisions;
        this.nrOfHorizontalSubdivisions = nrOfHorizontalSubdivisions;
        this.nrOfGlasses = nrOfGlasses;
        this.isFlipped = isFlipped;
        this.hasRoundTop = hasRoundTop;
    }

    public void SetNrOfVerticalSubdivisions(int nrOfDivisions, int nrOfGlasses)
    {
        this.nrOfVerticalSubdivisions = nrOfDivisions;
        this.nrOfGlasses = nrOfGlasses;
    }

    public void SetNrOfHorizontalSubdivisions(int nrOfDivisions)
    {
        this.nrOfHorizontalSubdivisions = nrOfDivisions;
    }


    public void Flip()
    {
        this.isFlipped = !this.isFlipped;
    }

    public void AddRoundTop()
    {
        this.hasRoundTop = true;
    }


    public Door GetFlippedCopy()
    {
        return new Door(this.nrOfVerticalSubdivisions, this.nrOfHorizontalSubdivisions, this.nrOfGlasses, !this.isFlipped, this.hasRoundTop);
    }

    public RenderTexture GetTexture(int resolution, ShaderMapping shaderMapping)
    {
        // if (Door.textureCache.ContainsKey((this, resolution)))
        // {
        //     return Door.textureCache[(this, resolution)];
        // }
        // calculate texture (mask texture)
        ComputeShader shader = shaderMapping.GetShaderByType(ShaderType.DoorTextureCreation);

        RenderTexture texture = new RenderTexture(resolution, resolution, 0, RenderTextureFormat.ARGB32);
        texture.enableRandomWrite = true;
        texture.wrapMode = TextureWrapMode.Repeat;
        texture.Create();

        shader.SetInt("resolution", resolution);
        shader.SetInt("nrOfVerticalSubdivisions", this.nrOfVerticalSubdivisions);
        shader.SetInt("nrOfHorizontalSubdivisions", this.nrOfHorizontalSubdivisions);
        shader.SetInt("nrOfGlasses", this.nrOfGlasses);
        shader.SetBool("isFlipped", this.isFlipped);
        shader.SetBool("hasRoundTop", this.hasRoundTop);

        shader.SetTexture(shader.FindKernel("CSMain"), "Result", texture);
        shader.Dispatch(shader.FindKernel("CSMain"), Mathf.CeilToInt(resolution / 8.0f), Mathf.CeilToInt(resolution / 8.0f), 1);

        return texture;
    }

    // public static void ClearTextureCache()
    // {
    //     foreach (var item in Door.textureCache.Values)
    //     {
    //         item.Item1.Release();
    //     }
    //     Door.textureCache = new Dictionary<(Door, int), (RenderTexture, RenderTexture)>();
    // }
}