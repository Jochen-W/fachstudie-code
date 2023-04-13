using System;
using UnityEngine;



public class Mercator
{
    public static readonly double max_latitude = Mercator.RadiansToDegrees(Math.Atan(Math.Sinh(Math.PI)));
    public static readonly double equatorial_circumference = 40_075_016.686; // meter


    private static readonly double preCalc1DivBy360 = 1 / 360.0;

    /// <summary>
    /// Converts radians to degrees.
    /// </summary>
    public static double RadiansToDegrees(double radians)
    {
        return radians * (180 / Math.PI);
    }

    /// <summary>
    /// Converts degrees to radians.
    /// </summary>
    public static double DegreesToRadians(double degrees)
    {
        return degrees * (Math.PI / 180);
    }


    /// <summary>
    /// Calculates the 2D (x, y) coordinates from a given point on a sphere (latitude, longitude).
    /// </summary>
    /// <param name="latitude">ϕ ∈ [-90°, 90°]</param>
    /// <param name="longitude">λ ∈ [-180°, 180°]</param>
    /// <returns>A 2D point (x,y) on the plane as 2-tuple. x ∈ [-180°, 180°], y ∈ [-180°, 180°]</returns>
    /// <remark>
    /// Formula from https://de.wikipedia.org/wiki/Mercator-Projektion
    /// </remark>
    public static (double, double) LatLonToXY(double latitude, double longitude)
    {
        return (longitude, Mercator.RadiansToDegrees(Math.Asinh(Math.Tan(Mercator.DegreesToRadians(latitude)))));
    }

    /// <summary>
    /// Calculates the point on a plane (latitude, longitude) from a given 2D point on a plane (x, y).
    /// </summary>
    /// <param name="x">∈ [-180°, 180°]</param>
    /// <param name="y">∈ [-180°, 180°]</param>
    /// <returns>A point on a sphere (latitude, longitude) as 2-tuple. latitude ∈ [-90°, 90°], longitude ∈ [-180°, 180°] </returns>
    /// <remark>
    /// Formula from https://de.wikipedia.org/wiki/Mercator-Projektion
    /// </remark>
    public static (double, double) XYToLatLon(double x, double y)
    {
        return (Mercator.RadiansToDegrees(Math.Asin(Math.Tanh(Mercator.DegreesToRadians(y)))), x);
    }


    /// <summary>
    /// Calculates the 2D (x, y) coordinates from a given point on a sphere (latitude, longitude).
    /// </summary>
    /// <param name="x">∈ [-180°, 180°]</param>
    /// <param name="y">∈ [-180°, 180°]</param>
    /// <returns>The tile-</returns>
    /// <remark>
    /// Formula from https://wiki.openstreetmap.org/wiki/Slippy_map_tilenames#Derivation_of_tile_names
    /// Tiles are 256 × 256 pixel PNG files
    /// </remark>
    public static (int, int) XYToTileXY(double x, double y, int zoom_level)
    {
        double n = Math.Pow(2, zoom_level);
        double x_0_to_1 = (180.0 + x) * preCalc1DivBy360;
        double y_0_to_1 = (180.0 - y) * preCalc1DivBy360;
        return (Convert.ToInt32(Math.Floor(n * x_0_to_1)), Convert.ToInt32(Math.Floor(n * y_0_to_1)));
    }

    public static (double, double) TileXYToXY(int x, int y, int zoom_level)
    {
        double n = 360.0 / Math.Pow(2, zoom_level);
        return ((double)x * n - 180.0, 180.0 - (double)y * n);
    }


    public static double Frac(double value)
    {
        return value - Math.Truncate(value);
    }

    public static (double, double) XYToTileXYFracs(double x, double y, int zoom_level)
    {
        double n = Math.Pow(2, zoom_level);
        double x_0_to_1 = (180.0 + x) * preCalc1DivBy360;
        double y_0_to_1 = (180.0 - y) * preCalc1DivBy360;
        return (Mercator.Frac(n * x_0_to_1), Mercator.Frac(n * y_0_to_1));
    }


    public static Vector3 GetScaleVec(int zoom_level)
    {
        float s = (float)(Math.Pow(2, zoom_level) * preCalc1DivBy360);
        return new Vector3(s, 1, s);
    }

    public static Vector3 GetUnScaleVec(int zoom_level)
    {
        float s = (float)(360 / Math.Pow(2, zoom_level));
        return new Vector3(s, 1, s);
    }

    public static double CircumferenceAtLat(double latitude)
    {
        return equatorial_circumference * Math.Cos(Mercator.DegreesToRadians(latitude));
    }

}
