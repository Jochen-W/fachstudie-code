using System;
using System.Linq;
using System.Collections.Generic;

enum WindowSymbol
{
    // START,
    // TOP,
    WINDOW,  // lv1
    SYNC_1_1,  // lv2
    SYNC_1_2,  // lv2
    SYNC_1_3,  // lv2
    SYNC_1_4,  // lv2
    GREAT_1,  // lv2
    Sync_2_Glass,
    GreatGlass,
    SmallGlass,
    Glass
}

class WindowGrammar
{
    private static Random rnd = new Random();

    private static readonly List<WindowSymbol> terminals = new List<WindowSymbol>() { WindowSymbol.SmallGlass, WindowSymbol.GreatGlass, WindowSymbol.Glass, WindowSymbol.Sync_2_Glass };
    private static readonly List<WindowSymbol> syncs = new List<WindowSymbol>() { WindowSymbol.SYNC_1_1, WindowSymbol.SYNC_1_2, WindowSymbol.SYNC_1_3, WindowSymbol.SYNC_1_4, WindowSymbol.Sync_2_Glass };
    private static readonly Dictionary<WindowSymbol, List<(int, List<WindowSymbol>)>> rules = new Dictionary<WindowSymbol, List<(int, List<WindowSymbol>)>>{
        { WindowSymbol.WINDOW, new List<(int, List<WindowSymbol>)>(){
            (1, new List<WindowSymbol>(){WindowSymbol.SYNC_1_1, WindowSymbol.SYNC_1_1}),
            (1, new List<WindowSymbol>(){WindowSymbol.SYNC_1_2, WindowSymbol.SYNC_1_2}),
            (1, new List<WindowSymbol>(){WindowSymbol.SYNC_1_3, WindowSymbol.SYNC_1_3}),
            (1, new List<WindowSymbol>(){WindowSymbol.SYNC_1_4, WindowSymbol.SYNC_1_4}),
            (1, new List<WindowSymbol>(){WindowSymbol.SYNC_1_1, WindowSymbol.SYNC_1_1, WindowSymbol.SYNC_1_1}),
            (1, new List<WindowSymbol>(){WindowSymbol.SYNC_1_2, WindowSymbol.SYNC_1_2, WindowSymbol.SYNC_1_2}),
            (1, new List<WindowSymbol>(){WindowSymbol.SYNC_1_3, WindowSymbol.SYNC_1_3, WindowSymbol.SYNC_1_3}),
            (1, new List<WindowSymbol>(){WindowSymbol.SYNC_1_4, WindowSymbol.SYNC_1_4, WindowSymbol.SYNC_1_4}),
            (3, new List<WindowSymbol>(){WindowSymbol.SmallGlass, WindowSymbol.GREAT_1}),
            (1, new List<WindowSymbol>(){WindowSymbol.SmallGlass, WindowSymbol.GreatGlass, WindowSymbol.SmallGlass}),
            (2, new List<WindowSymbol>(){WindowSymbol.Glass}),
        }},
        { WindowSymbol.SYNC_1_1, new List<(int, List<WindowSymbol>)>(){
            (1, new List<WindowSymbol>(){WindowSymbol.Sync_2_Glass, WindowSymbol.Sync_2_Glass}),
        }},
        { WindowSymbol.SYNC_1_2, new List<(int, List<WindowSymbol>)>(){
            (1, new List<WindowSymbol>(){WindowSymbol.Sync_2_Glass, WindowSymbol.Sync_2_Glass, WindowSymbol.Sync_2_Glass}),
        }},
        { WindowSymbol.SYNC_1_3, new List<(int, List<WindowSymbol>)>(){
            (1, new List<WindowSymbol>(){WindowSymbol.SmallGlass, WindowSymbol.GreatGlass}),
        }},
        { WindowSymbol.SYNC_1_4, new List<(int, List<WindowSymbol>)>(){
            (1, new List<WindowSymbol>(){WindowSymbol.Glass}),
        }},
        { WindowSymbol.GREAT_1, new List<(int, List<WindowSymbol>)>(){
            (2, new List<WindowSymbol>(){WindowSymbol.Sync_2_Glass, WindowSymbol.Sync_2_Glass}),
            (2, new List<WindowSymbol>(){WindowSymbol.Sync_2_Glass, WindowSymbol.Sync_2_Glass, WindowSymbol.Sync_2_Glass}),
            (2, new List<WindowSymbol>(){WindowSymbol.SmallGlass, WindowSymbol.GreatGlass}),
            (1, new List<WindowSymbol>(){WindowSymbol.SmallGlass, WindowSymbol.GreatGlass, WindowSymbol.SmallGlass}),
            (2, new List<WindowSymbol>(){WindowSymbol.Glass}),
        }},
    };

    // pruned rules is used for windows with less beams
    private static readonly Dictionary<WindowSymbol, List<(int, List<WindowSymbol>)>> prunedRules = new Dictionary<WindowSymbol, List<(int, List<WindowSymbol>)>>{
        { WindowSymbol.WINDOW, new List<(int, List<WindowSymbol>)>(){
            (1, new List<WindowSymbol>(){WindowSymbol.SYNC_1_1, WindowSymbol.SYNC_1_1}),
            (1, new List<WindowSymbol>(){WindowSymbol.SYNC_1_3, WindowSymbol.SYNC_1_3}),
            (1, new List<WindowSymbol>(){WindowSymbol.SYNC_1_4, WindowSymbol.SYNC_1_4}),
            (4, new List<WindowSymbol>(){WindowSymbol.SmallGlass, WindowSymbol.GREAT_1}),
            (8, new List<WindowSymbol>(){WindowSymbol.Glass}),
        }},
        { WindowSymbol.SYNC_1_1, new List<(int, List<WindowSymbol>)>(){
            (1, new List<WindowSymbol>(){WindowSymbol.Sync_2_Glass, WindowSymbol.Sync_2_Glass}),
        }},
        { WindowSymbol.SYNC_1_3, new List<(int, List<WindowSymbol>)>(){
            (1, new List<WindowSymbol>(){WindowSymbol.SmallGlass, WindowSymbol.GreatGlass}),
        }},
        { WindowSymbol.SYNC_1_4, new List<(int, List<WindowSymbol>)>(){
            (1, new List<WindowSymbol>(){WindowSymbol.Glass}),
        }},
        { WindowSymbol.GREAT_1, new List<(int, List<WindowSymbol>)>(){
            (1, new List<WindowSymbol>(){WindowSymbol.SmallGlass, WindowSymbol.GreatGlass}),
            (4, new List<WindowSymbol>(){WindowSymbol.Glass}),
        }},
    };

    private static void ApplyRule(WindowSymbol nonTerminal, List<WindowSymbol> ruleToApply, ref Window window)
    {
        bool isSegmentationHorizontal = nonTerminal == WindowSymbol.WINDOW;  // horizontal segmentation = divide x into n segments

        List<int> linePositionFactors = new List<int>();

        switch (ruleToApply)
        {
            case var _ when ruleToApply.Intersect(WindowGrammar.syncs).Count() > 0:
                int nrSubDivisions = ruleToApply.Count(s => WindowGrammar.syncs.Contains(s));
                for (int i = 1; i < nrSubDivisions; i++)
                {
                    linePositionFactors.Add((int)((i / (float)nrSubDivisions) * 6));
                }
                break;
            case var _ when ruleToApply.Contains(WindowSymbol.GREAT_1):
                linePositionFactors.Add(2);  // 1/3
                break;
            case var _ when ruleToApply.Contains(WindowSymbol.GreatGlass):
                int nrOfSmallGlass = ruleToApply.Count(s => s == WindowSymbol.SmallGlass);
                if (nrOfSmallGlass == 1)
                {
                    linePositionFactors.Add(2);  // 1/3
                }
                else if (nrOfSmallGlass == 2)
                {
                    linePositionFactors.Add(1);  // 1/6
                    linePositionFactors.Add(5);  // 5/6
                }
                break;
                // ignore Glass (since it makes no lines)
        }

        foreach (int linePosFactor in linePositionFactors)
        {
            window.AddBeam(linePosFactor, nonTerminal == WindowSymbol.WINDOW);
        }
    }

    private static bool IsTerminalOnly(List<WindowSymbol> ruleStr)
    {
        foreach (var ruleChar in ruleStr)
        {
            if (!WindowGrammar.terminals.Contains(ruleChar))
            {
                return false;
            }
        }
        return true;
    }

    private static int GetFirstNonTerminal(List<WindowSymbol> ruleStr)
    {
        for (int i = 0; i < ruleStr.Count; i++)
        {
            if (!WindowGrammar.terminals.Contains(ruleStr[i]))
            {
                return i;
            }
        }
        // should never happen
        return -1;
    }

    private static List<WindowSymbol> GetRandomRule(List<(int, List<WindowSymbol>)> possibleRules)
    {
        int[] rangeArray = new int[possibleRules.Count];
        int sum = 0;
        for (int i = 0; i < possibleRules.Count; i++)
        {
            sum += possibleRules[i].Item1;
            rangeArray[i] = sum;
        }

        int randomInt = WindowGrammar.rnd.Next(sum);

        for (int i = 0; i < possibleRules.Count; i++)
        {
            if (rangeArray[i] > randomInt)
            {
                return possibleRules[i].Item2;
            }
        }

        // this should never happen
        return possibleRules[0].Item2;
    }

    public static Window GenerateWindow(bool usePrunedRules = false, bool canHaveRoundTop = true)
    {
        Window window = new Window();

        //WindowSymbol, (top, left), (bottom, right)
        List<WindowSymbol> ruleStr = new List<WindowSymbol>() { WindowSymbol.WINDOW };

        while (!WindowGrammar.IsTerminalOnly(ruleStr))
        {
            int index = WindowGrammar.GetFirstNonTerminal(ruleStr);

            List<(int, List<WindowSymbol>)> possibleRules = (usePrunedRules ? WindowGrammar.prunedRules : WindowGrammar.rules)[ruleStr[index]];
            List<WindowSymbol> ruleToApply = WindowGrammar.GetRandomRule(possibleRules);
            WindowGrammar.ApplyRule(ruleStr[index], ruleToApply, ref window);
            ruleStr.RemoveAt(index);
            ruleStr.InsertRange(index, ruleToApply);
        }

        if (WindowGrammar.rnd.Next(100) < 50)  // 50%
        {
            window.Flip();
        }
        if (WindowGrammar.rnd.Next(100) < 50)  // 50%
        {
            window.RotateClockwise();
        }

        if (canHaveRoundTop && WindowGrammar.rnd.Next(100) < 8)  // 8%
        {
            window.AddRoundTop();
        }

        return window;
    }
}