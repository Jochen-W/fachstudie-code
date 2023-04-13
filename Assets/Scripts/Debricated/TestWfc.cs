using UnityEngine;
using System.Collections.Generic;
public class TestWfc : MonoBehaviour
{

    Dictionary<Tile, string> tileStrLookup = new Dictionary<Tile, string>(){
        {Tile.CORNER_BL,  "└"},
        {Tile.CORNER_TL,  "┌"},
        {Tile.CORNER_TR,  "┐"},
        {Tile.CORNER_BR,  "┘"},
        {Tile.T_TOP,      "┴"},
        {Tile.T_RIGHT,    "├"},
        {Tile.T_BOTTOM,   "┬"},
        {Tile.T_LEFT,     "┤"},
        {Tile.VERTICAL,   "│"},
        {Tile.HORIZONTAL, "─"},
        {Tile.CROSS,      "┼"},
        {Tile.EMPTY,      "▒"},
    };

    public int numberOfGenerations = 1;
    public int height = 5;
    public int width = 5;

    void Start()
    {
        for (int _ = 0; _ < numberOfGenerations; _++)
        {
            Tile[,] result = WFC.GenerateResult(height, width);
            string s = "";
            for (int row = 0; row < height; row++)
            {
                for (int col = 0; col < width; col++)
                {
                    s += tileStrLookup[result[row, col]];
                }
                s += "\n";
            }
            Debug.Log(s);
        }
    }
}
