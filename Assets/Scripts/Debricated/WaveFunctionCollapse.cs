using System;
using System.Linq;
using System.Collections.Generic;

enum Tile : uint
{
    //clockwise: trbl
    CORNER_BL = 0b1100,  // └─
    CORNER_TL = 0b0110,  // ┌─
    CORNER_TR = 0b0011,  // ─┐
    CORNER_BR = 0b1001,  // ─┘

    T_TOP = 0b1101,     // ┴
    T_RIGHT = 0b1110,   // ├─
    T_BOTTOM = 0b0111,  // ┬
    T_LEFT = 0b1011,    // ─┤

    VERTICAL = 0b1010,    // │
    HORIZONTAL = 0b0101,  // ──
    CROSS = 0b1111,     // ─┼─
    EMPTY = 0b0000,      //  ◻
}

enum Direction : uint
{
    TOP = 3,
    RIGHT = 2,
    BOTTOM = 1,
    LEFT = 0,
}

enum Option : uint
{
    UNDEFINED = 0b00,  // utility value
    EMPTY = 0b01,
    LINE = 0b010,
    BOTH = 0b11,  // utility value
}

class RandomHelper
{
    static Random rnd = new Random();
    public static T GetRandomFromList<T>(List<T> list, Dictionary<T, int> weights) where T : Enum
    {
        (int, T)[] rangeArray = new (int, T)[list.Count];
        int sum = 0;
        for (int i = 0; i < list.Count; i++)
        {
            sum += weights[list[i]];
            rangeArray[i] = (sum, list[i]);
        }

        int randomInt = RandomHelper.rnd.Next(sum);

        for (int i = 0; i < list.Count; i++)
        {
            if (rangeArray[i].Item1 > randomInt)
            {
                return rangeArray[i].Item2;
            }
        }

        // this should never happen
        return list[0];
    }
}

class Cell
{
    Option[] neighboringOptions;  // it they change -> inform the neighbor (propagate)
    List<Tile> possibleTiles;  // all possible shapes
    Tile? tile;

    public Cell()
    {
        this.neighboringOptions = new Option[4] { Option.BOTH, Option.BOTH, Option.BOTH, Option.BOTH };
        this.possibleTiles = new List<Tile>((Tile[])Enum.GetValues(typeof(Tile)));
        this.tile = null;
    }

    /// <summary>
    /// Calculates the possible connections in each direction based on the given shapes.
    /// </summary>
    /// <example>
    /// shapes = {──, ─┬─} -> [EMPTY, LINE, BOTH, LINE] (in clockwise order, first is TOP)
    /// </example>
    private static Option[] CalculateOptions(List<Tile> shapes)
    {
        Option[] options = new Option[4] { Option.UNDEFINED, Option.UNDEFINED, Option.UNDEFINED, Option.UNDEFINED };
        foreach (Tile shape in shapes)
        {
            foreach (Direction dir in Enum.GetValues(typeof(Direction)))
            {
                options[3 - (int)dir] = (Option)((uint)options[3 - (int)dir] | (((uint)shape >> (int)dir) & 1) + 1);
            }
        }
        return options;
    }

    /// <summary>
    /// Returns wether the constraint (option + direction) matches with the tile.
    /// </summary>
    /// <example>
    /// (CROSS, LINE, TOP) returns true, since CROSS has a line-connection at the top
    /// (EMPTY, LINE, TOP) returns false, since EMPTY has a no line-connection at the top
    /// </example>
    private static bool MatchesTileToConstrain(Tile tile, Option option, Direction direction)
    {
        return option == Option.BOTH || (((uint)tile >> (int)direction) & 1) == (uint)option - 1;
    }

    /// <summary>
    /// Used to determine if a propagation is needed (changes in constrains) and in which directions.
    /// </summary>
    private List<Direction> OptionChanges(List<Tile> shapes)
    {
        List<Direction> changes = new List<Direction>();
        Option[] temp = CalculateOptions(shapes);
        foreach (Direction dir in Enum.GetValues(typeof(Direction)))
        {
            if (temp[3 - (int)dir] != this.neighboringOptions[3 - (int)dir])
            {
                changes.Add(dir);
            }
        }
        this.neighboringOptions = temp;
        return changes;
    }

    /// <summary>
    /// Collapse chooses one of the possible tiles at random and sets it as its own tile.
    /// </summary>
    public List<Direction> Collapse(Dictionary<Tile, int> weights)
    {
        this.tile = RandomHelper.GetRandomFromList(this.possibleTiles, weights);
        return OptionChanges(new List<Tile>() { (Tile)this.tile });
    }

    /// <summary>
    /// Constraints the number of options in a direction.
    /// </summary>
    /// <example>
    /// (LINE, TOP) constraints the possible tiles to tiles, that have can connect to a LINE at the top.
    /// </example>
    public List<Direction> Constrain(Option option, Direction direction)
    {
        this.possibleTiles = this.possibleTiles.Where(tile => Cell.MatchesTileToConstrain(tile, option, direction)).ToList();
        return OptionChanges(this.possibleTiles);
    }

    /// <summary>
    /// Constraints the possible tiles to exactly the given one.
    /// The phantom tiles are a option to trick the OptionChanges method.
    /// </summary>
    public void SetPossibleTile(Tile tile, List<Tile> phantomTiles)
    {
        this.possibleTiles = new List<Tile>() { tile };
        OptionChanges(phantomTiles);
    }

    public Option GetConstraints(Direction direction)
    {
        return this.neighboringOptions[3 - (int)direction];
    }

    public bool IsCollapsed()
    {
        return this.tile is not null;
    }

    public int GetEntropy()
    {
        return this.IsCollapsed() ? int.MaxValue : this.possibleTiles.Count;
    }

    public void RemoveEdges()
    {
        this.possibleTiles.Remove(Tile.CORNER_BL);
        this.possibleTiles.Remove(Tile.CORNER_BR);
        this.possibleTiles.Remove(Tile.CORNER_TL);
        this.possibleTiles.Remove(Tile.CORNER_TR);
    }

    public Tile? GetTile()
    {
        return this.tile;
    }

}

class WFC  // Wave Function Collapse
{
    public static readonly Dictionary<Tile, int> weights = new Dictionary<Tile, int>()
    {
        // weights as ratios
        {Tile.CORNER_BL,   0},
        {Tile.CORNER_TL,   0},
        {Tile.CORNER_TR,   0},
        {Tile.CORNER_BR,   0},
        {Tile.T_TOP,       5},
        {Tile.T_RIGHT,     5},
        {Tile.T_BOTTOM,    5},
        {Tile.T_LEFT,      5},
        {Tile.VERTICAL,   20},
        {Tile.HORIZONTAL, 20},
        {Tile.CROSS,      40},
        {Tile.EMPTY,      40},
    };

    public static readonly Dictionary<Direction, Direction> opposites = new Dictionary<Direction, Direction>(){
        {Direction.TOP, Direction.BOTTOM},
        {Direction.RIGHT, Direction.LEFT},
        {Direction.BOTTOM, Direction.TOP},
        {Direction.LEFT, Direction.RIGHT},
    };
    public static readonly Dictionary<Direction, (int, int)> indexOffsetLookup = new Dictionary<Direction, (int, int)>(){
        {Direction.TOP, (-1, 0)},
        {Direction.BOTTOM, (1, 0)},
        {Direction.LEFT, (0, -1)},
        {Direction.RIGHT, (0, 1)},
    };

    /// <summary>
    /// Calculates wether the two shapes matches. DirB is the direction from A to B.
    /// So if B is above A, dirB should be TOP.
    /// </summary>
    public static bool IsMatch(Tile a, Tile b, Direction dirB)
    {
        // use mask to isolate the right bit
        uint aValue = ((uint)a >> (int)dirB) & 1;
        uint bValue = ((uint)b >> (int)opposites[dirB]) & 1;
        return aValue == bValue;
    }

    public static bool IsCollapsed(ref Cell[,] grid)
    {
        foreach (Cell cell in grid)
        {
            if (!cell.IsCollapsed())
            {
                return false;
            }
        }
        return true;
    }

    public static (int, int) GetMinEntropyPosition(ref Cell[,] grid)
    {
        int minEntropy = int.MaxValue;
        (int rowPos, int colPos) = (-1, -1);
        for (int row = 0; row < grid.GetLength(0); row++)
        {
            for (int col = 0; col < grid.GetLength(1); col++)
            {
                if (grid[row, col].GetEntropy() < minEntropy)
                {
                    minEntropy = grid[row, col].GetEntropy();
                    (rowPos, colPos) = (row, col);
                }
            }
        }
        return (rowPos, colPos);
    }

    // TODO: back-propagation (on "error")
    public static Tile[,] GenerateResult(int height, int width)
    {
        Cell[,] grid = new Cell[height, width];
        // initialize all
        for (int row = 0; row < height; row++)
        {
            for (int col = 0; col < width; col++)
            {
                grid[row, col] = new Cell();
                grid[row, col].RemoveEdges();
            }
        }
        // constrain edges
        for (int i = 0; i < height; i++)
        {
            grid[i, 0].Constrain(Option.EMPTY, Direction.LEFT);
            grid[i, width - 1].Constrain(Option.EMPTY, Direction.RIGHT);
        }
        for (int i = 0; i < width; i++)
        {
            grid[0, i].Constrain(Option.EMPTY, Direction.TOP);
            grid[height - 1, i].Constrain(Option.EMPTY, Direction.BOTTOM);
        }
        // set edges (constrain them to corner; use the empty tile, so a neighbor option change is detected when collapsed)
        grid[0, 0].SetPossibleTile(Tile.CORNER_TL, new List<Tile>() { Tile.CORNER_TL, Tile.EMPTY });
        grid[0, width - 1].SetPossibleTile(Tile.CORNER_TR, new List<Tile>() { Tile.CORNER_TR, Tile.EMPTY });
        grid[height - 1, 0].SetPossibleTile(Tile.CORNER_BL, new List<Tile>() { Tile.CORNER_BL, Tile.EMPTY });
        grid[height - 1, width - 1].SetPossibleTile(Tile.CORNER_BR, new List<Tile>() { Tile.CORNER_BR, Tile.EMPTY });

        // (posX, posY, dirChangeCameFrom)
        Stack<(int, int, Direction)> stack = new Stack<(int, int, Direction)>();

        while (!WFC.IsCollapsed(ref grid))
        {
            (int r, int c) = WFC.GetMinEntropyPosition(ref grid);
            List<Direction> changesInDir = grid[r, c].Collapse(weights);

            // propagate
            foreach (Direction dir in changesInDir)
            {
                (int rOff, int cOff) = WFC.indexOffsetLookup[dir];
                stack.Push((r + rOff, c + cOff, WFC.opposites[dir]));
            }
            while (stack.Count > 0)
            {
                (int row, int col, Direction dir) = stack.Pop();
                (int rOff, int cOff) = WFC.indexOffsetLookup[dir];
                List<Direction> newChangesInDir = grid[row, col].Constrain(grid[row + rOff, col + cOff].GetConstraints(WFC.opposites[dir]), dir);
                newChangesInDir.Remove(dir);  // don't jump backwards
                foreach (Direction newDir in newChangesInDir)
                {
                    (rOff, cOff) = WFC.indexOffsetLookup[newDir];
                    stack.Push((row + rOff, col + cOff, WFC.opposites[newDir]));
                }
            }
        }

        Tile[,] result = new Tile[height, width];
        for (int row = 0; row < height; row++)
        {
            for (int col = 0; col < width; col++)
            {
                result[row, col] = (Tile)grid[row, col].GetTile();
            }
        }
        return result;
    }
}