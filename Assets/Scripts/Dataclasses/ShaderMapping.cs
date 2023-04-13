using UnityEngine;
using System;
using System.Collections.Generic;

public enum ShaderType
{
    WindowTextureCreation,
    DoorTextureCreation,
    NormalMapCalculator,
    TextureCombiner,
    TextureQuadMerger,
}

[Serializable]
public struct ShaderTypeMapping
{
    public ShaderType shaderType;
    public ComputeShader shader;

    public ShaderTypeMapping(ShaderType shaderType, ComputeShader shader)
    {
        this.shaderType = shaderType;
        this.shader = shader;
    }
}

[CreateAssetMenu(menuName = "ShaderMapping")]
public class ShaderMapping : ScriptableObject
{
    public List<ShaderTypeMapping> allShaders;

    public ComputeShader GetShaderByType(ShaderType shaderType)
    {
        foreach (var item in allShaders)
        {
            if (item.shaderType == shaderType)
            {
                return item.shader;
            }
        }
        throw new Exception($"Shader with type {shaderType} not defined!");
    }
}
