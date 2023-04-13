using UnityEngine;


/*  IDEA:

    - use red channel for window and blue for wood, then pick random colors (facade, glass and wood)
    - use MaterialPropertyBlocks
    - calculate bump-map (normals + depth)

*/
public class Window
{
    // private static Dictionary<(Window, int), (RenderTexture, RenderTexture)> textureCache = new Dictionary<(Window, int), (RenderTexture, RenderTexture)>();

    // from left to right: sync2=00100, sync3=01010, smallG=01000, SGS=10001
    int beamMask1;
    int beamMask2;
    bool isFlipped;
    bool isRotatedClockwise;
    bool hasRoundTop;
    bool hasChurchTop;

    public Window()
    {
        this.beamMask1 = 0;
        this.beamMask2 = 0;
        this.isFlipped = false;
        this.isRotatedClockwise = false;
        this.hasRoundTop = false;
        this.hasChurchTop = false;
    }

    public Window(int mask1, int mask2, bool isFlipped, bool isRotated, bool hasRoundTop, bool hasChurchTop)
    {
        this.beamMask1 = mask1;
        this.beamMask2 = mask2;
        this.isFlipped = isFlipped;
        this.isRotatedClockwise = isRotated;
        this.hasRoundTop = hasRoundTop;
        this.hasChurchTop = hasChurchTop;
    }

    public void AddBeam(int linePositionFactor, bool isFirstDivision)
    {
        if (isFirstDivision)
        {
            this.beamMask1 |= (1 << (5 - linePositionFactor));
        }
        else
        {
            this.beamMask2 |= (1 << (5 - linePositionFactor));
        }
    }

    public void Flip()
    {
        this.isFlipped = !this.isFlipped;
    }

    public void RotateClockwise()
    {
        if (this.isRotatedClockwise)
        {
            this.Flip();
        }
        this.isRotatedClockwise = !this.isRotatedClockwise;
    }

    public void AddRoundTop()
    {
        this.hasRoundTop = true;
        this.hasChurchTop = false;
    }

    public void AddChurchTop()
    {
        this.hasChurchTop = true;
        this.hasRoundTop = false;
    }


    public Window GetFlippedCopy()
    {
        return new Window(this.beamMask1, this.beamMask2, !this.isFlipped, this.isRotatedClockwise, this.hasRoundTop, this.hasChurchTop);
    }

    public RenderTexture GetTexture(int resolution, ShaderMapping shaderMapping)
    {
        // if (Window.textureCache.ContainsKey((this, resolution)))
        // {
        //     return Window.textureCache[(this, resolution)];
        // }
        // calculate texture (mask texture)
        ComputeShader shader = shaderMapping.GetShaderByType(ShaderType.WindowTextureCreation);

        RenderTexture texture = new RenderTexture(resolution, resolution, 0, RenderTextureFormat.ARGB32);
        texture.enableRandomWrite = true;
        texture.wrapMode = TextureWrapMode.Repeat;
        texture.Create();

        shader.SetInt("resolution", resolution);
        shader.SetInt("beamMask1", this.beamMask1);
        shader.SetInt("beamMask2", this.beamMask2);
        shader.SetBool("isFlipped", this.isFlipped);
        shader.SetBool("isRotatedClockwise", this.isRotatedClockwise);
        shader.SetBool("hasRoundTop", this.hasRoundTop);
        shader.SetBool("hasChurchTop", this.hasChurchTop);

        shader.SetTexture(shader.FindKernel("CSMain"), "Result", texture);
        shader.Dispatch(shader.FindKernel("CSMain"), Mathf.CeilToInt(resolution / 8.0f), Mathf.CeilToInt(resolution / 8.0f), 1);

        // use gauss-filter to make the edges smooth -> doesn't look so good
        // ComputeShader gaussianShader = AssetDatabase.LoadAssetAtPath("Assets/Shaders/GaussianBlur.compute", typeof(ComputeShader)) as ComputeShader;
        // RenderTexture gaussianTexture = new RenderTexture(texture.width, texture.height, 0, RenderTextureFormat.ARGB4444);
        // gaussianTexture.enableRandomWrite = true;
        // gaussianTexture.wrapMode = TextureWrapMode.Repeat;
        // gaussianTexture.Create();
        // gaussianShader.SetInt("resolution", texture.width);
        // gaussianShader.SetTexture(gaussianShader.FindKernel("CSMain"), "Input", normalsTexture);
        // gaussianShader.SetTexture(gaussianShader.FindKernel("CSMain"), "Result", gaussianTexture);
        // gaussianShader.Dispatch(gaussianShader.FindKernel("CSMain"), Mathf.CeilToInt(resolution / 8.0f), Mathf.CeilToInt(resolution / 8.0f), 1);

        // Window.textureCache[(this, resolution)] = (texture, normalsTexture);

        return texture;
    }

    // public static void ClearTextureCache()
    // {
    //     foreach (var item in Window.textureCache.Values)
    //     {
    //         item.Item1.Release();
    //     }
    //     Window.textureCache = new Dictionary<(Window, int), (RenderTexture, RenderTexture)>();
    // }
}