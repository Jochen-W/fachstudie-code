using System.Collections.Generic;
using UnityEngine;


public class Facade
{
    public struct FacadeRule
    {
        public bool hasWindows;
        public bool usePruneRules;  // = windows tend to have less beams
        public bool canHaveRoundTop;
        public bool isChurch;     // = mosaic, ...

        public FacadeRule(bool _hasWindows, bool _usePruneRules, bool _canHaveRoundTop, bool _isChurch)
        {
            this.hasWindows = _hasWindows;
            this.usePruneRules = _usePruneRules;
            this.canHaveRoundTop = _canHaveRoundTop;
            this.isChurch = _isChurch;
        }
    }

    public Window window;
    public Window flippedWindow;
    public Door door;
    public Door flippedDoor;
    public float windowWidth;
    public float windowHeight;
    public WindowShape windowShape;
    public WindowLayout layout;
    public FacadeRule facadeRule;

    /*
        residential 		                                        -> normal
        religious, synagogue                                        -> mosaic
        commercial 			                                        -> wenig balken
        retail 				                                        -> groß (wenig balken)
        roof, ruins, parking, container, shelter, viaduct, bunker   -> nix (z.B. Tanke)
    */

    private static Dictionary<HashSet<string>, FacadeRule> facadeRules = new Dictionary<HashSet<string>, FacadeRule>(){
        {new HashSet<string>(){"religious", "synagogue"},   new FacadeRule(true, false, false, true)},
        {new HashSet<string>(){"commercial"},               new FacadeRule(true, true, false, false)},
        {new HashSet<string>(){"retail"},                   new FacadeRule(true, true, false, false)},
        // no/empty facades
        {new HashSet<string>(){"roof", "ruins", "parking", "container", "shelter", "viaduct", "bunker", "anchorage", "tower"}, new FacadeRule(false, false, false, false)},
    };

    public Facade(Window window, Door door, float windowWidth, float windowHeight, WindowShape windowShape, WindowLayout layout, FacadeRule facadeRule)
    {
        this.window = window;
        this.door = door;
        if (facadeRule.isChurch) this.window.AddChurchTop();
        if (layout == WindowLayout.PAIRED_FLIPPED)
        {
            this.flippedWindow = window.GetFlippedCopy();
            this.flippedDoor = door.GetFlippedCopy();
        }
        this.windowShape = windowShape;
        this.layout = layout;
        this.windowWidth = windowWidth * (facadeRule.isChurch ? 2 : 1);
        this.windowHeight = windowHeight;  // unused
        this.facadeRule = facadeRule;
    }

    public static Facade GenerateRandomFacade(string buildingType, float heightMultiplier)
    {
        FacadeRule facadeRule = new FacadeRule(true, false, true, false);  // default
        foreach ((HashSet<string> set, FacadeRule rule) in Facade.facadeRules)
        {
            if (set.Contains(buildingType))
            {
                facadeRule = rule;
                break;
            }
        }

        float width = 1.4f + 0.2f * (float)RandomPicker.rand.Next(6);  //1,4m ... 2,6m
        float height = 1.0f + 0.2f * (float)RandomPicker.rand.Next(4);  //1,0m ... 1,6m

        return new Facade(WindowGrammar.GenerateWindow(facadeRule.usePruneRules, facadeRule.canHaveRoundTop),
                          DoorGrammar.GenerateDoor(),
                          width * heightMultiplier,
                          height * heightMultiplier,
                          RandomPicker.GetRandom<WindowShape>(),
                          facadeRule.isChurch ? WindowLayout.SINGLE : RandomPicker.GetRandom<WindowLayout>(),
                          facadeRule
                        );
    }

    public RenderTexture GetDoorTexture(int resolution, ShaderMapping shaderMapping)
    {
        return this.door.GetTexture(resolution, shaderMapping);
    }

    public RenderTexture GetWindowTexture(int resolution, ShaderMapping shaderMapping)
    {
        RenderTexture texture;
        switch (this.layout)
        {
            case WindowLayout.PAIRED:
                texture = Facade.CombineTexture(shaderMapping, this.window.GetTexture(resolution, shaderMapping));
                break;
            case WindowLayout.PAIRED_FLIPPED:
                texture = Facade.CombineTexture(shaderMapping, this.window.GetTexture(resolution, shaderMapping), this.flippedWindow.GetTexture(resolution, shaderMapping));
                break;
            case WindowLayout.SINGLE:
            default:
                texture = this.window.GetTexture(resolution, shaderMapping);
                break;
        }

        return texture;
    }

    private static RenderTexture CombineTexture(ShaderMapping shaderMapping, RenderTexture left, RenderTexture right = null)
    {
        RenderTexture texture = new RenderTexture((int)(left.width * 1.75f), left.height, 0, RenderTextureFormat.ARGB32);
        texture.enableRandomWrite = true;
        texture.wrapMode = TextureWrapMode.Repeat;
        texture.Create();

        ComputeShader shader = shaderMapping.GetShaderByType(ShaderType.TextureCombiner);
        shader.SetInt("base_resolution", left.width);
        shader.SetFloat("cut_out_ratio", 7.0f / 8.0f);
        shader.SetBool("same_texture", right == null);
        shader.SetTexture(shader.FindKernel("CSMain"), "Left", left);
        if (right == null)
        {
            right = new RenderTexture(1, 1, 0, RenderTextureFormat.ARGB32);
            right.enableRandomWrite = true;
            right.wrapMode = TextureWrapMode.Repeat;
            right.Create();
        }
        shader.SetTexture(shader.FindKernel("CSMain"), "Right", right);
        shader.SetTexture(shader.FindKernel("CSMain"), "Result", texture);
        shader.Dispatch(shader.FindKernel("CSMain"), Mathf.CeilToInt(texture.width / 8.0f), Mathf.CeilToInt(texture.height / 8.0f), 1);

        return texture;
    }

    // private static RenderTexture CutOutCenterOfTexture(RenderTexture toBigTexture, float ratio = 10 / 16.0f)
    // {
    //     RenderTexture texture = new RenderTexture(toBigTexture.width, (int)(toBigTexture.height * ratio), 0, RenderTextureFormat.ARGB4444);
    //     texture.enableRandomWrite = true;
    //     texture.wrapMode = TextureWrapMode.Repeat;
    //     texture.Create();

    //     ComputeShader shader = AssetDatabase.LoadAssetAtPath("Assets/Shaders/TextureCutOut.compute", typeof(ComputeShader)) as ComputeShader;

    //     shader.SetInt("offset", (int)(toBigTexture.height * ((1 - ratio) * 0.5f)));
    //     shader.SetTexture(shader.FindKernel("CSMain"), "Input", toBigTexture);

    //     shader.SetTexture(shader.FindKernel("CSMain"), "Result", texture);
    //     shader.Dispatch(shader.FindKernel("CSMain"), Mathf.CeilToInt(texture.width / 8.0f), Mathf.CeilToInt(texture.height / 8.0f), 1);

    //     return texture;
    // }
}