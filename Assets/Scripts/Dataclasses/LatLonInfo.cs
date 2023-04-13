using UnityEngine;
using System;

[CreateAssetMenu(menuName = "LatLonInfo")]
public class LatLonInfo : ScriptableObject
{
    public double latitude = 0.0;
    public double longitude = 0.0;



    public (double, double) AsXY()
    {
        return Mercator.LatLonToXY(latitude, longitude);
    }

    public (int, int) AsTileXY(ConfigInfo configInfo)
    {
        (double x, double y) = AsXY();
        return Mercator.XYToTileXY(x, y, configInfo.zoom);
    }

    public Vector3 AsScenePos(ConfigInfo configInfo)
    {
        (double x, double y) = AsXY();
        (double x_frac, double y_frac) = Mercator.XYToTileXYFracs(x, y, configInfo.zoom);
        double x_origin = (x_frac - 0.5) * configInfo.tileSizeUnity;
        double y_origin = (0.5 - y_frac) * configInfo.tileSizeUnity;
        return new Vector3((float)x_origin, 0, (float)y_origin);
    }


    public Vector3 OtherToScenePos(double lat_other, double lon_other, ConfigInfo configInfo)
    {
        (double x, double y) = AsXY();
        (double x_other, double y_other) = Mercator.LatLonToXY(lat_other, lon_other);
        Vector3 diff = new Vector3((float)(x_other - x), 0, (float)(y_other - y));
        return AsScenePos(configInfo) + diff * configInfo.TilePerXYDegree() * configInfo.tileSizeUnity;
    }


    public (double, double) XYFromScenePos(Vector3 scene_pos, ConfigInfo configInfo)
    {
        Vector3 diff = (scene_pos - AsScenePos(configInfo)) / (configInfo.tileSizeUnity * configInfo.TilePerXYDegree());
        (double x, double y) = AsXY();
        return (x + diff.x, y + diff.z);
    }

    public float GetHeightMultiplier(ConfigInfo configInfo)
    {
        return (float)((configInfo.tileSizeUnity * Math.Pow(2, configInfo.zoom)) / Mercator.CircumferenceAtLat(latitude));
    }
}
