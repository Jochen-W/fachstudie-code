using System;
using UnityEngine;

[CreateAssetMenu(menuName = "ConfigInfo")]
public class ConfigInfo : ScriptableObject
{
    public int zoom = 0;
    public int zoomForTexture = 1;
    public int radius = 1;  // how many tiles should be around the mid tile
    public float tileSizeUnity = 50.0f; //use this to calculate building-positions and heights

    public float TilePerXYDegree()
    {
        return (float)(Math.Pow(2, zoom) / 360.0);
    }

    public float GetMaxHeightDiff()
    {
        // should be Mount Everest height + Mariana Trench depth: (8900.0f + 12.000f),
        // but mapzen tiles are only between 0 and 8900.0f
        return 8900.0f;
    }
}
