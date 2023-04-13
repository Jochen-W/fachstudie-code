using System;
using System.Linq;
using System.Collections.Generic;

enum DoorSymbol
{
    DOOR,
    Nothing,
    S_1_G,
    S_1_P,
    S_2_G,
    S_2_P,
    S_3_G,
    S_3_P,
    Glass,
    Panel,
}

class DoorGrammar
{
    private static Random rnd = new Random();

    private static readonly List<DoorSymbol> terminals = new List<DoorSymbol>() { DoorSymbol.Nothing, DoorSymbol.Glass, DoorSymbol.Panel };
    private static readonly List<DoorSymbol> glassSyncSymbols = new List<DoorSymbol>() {
        DoorSymbol.S_1_G, DoorSymbol.S_2_G, DoorSymbol.S_3_G,
    };
    private static readonly Dictionary<DoorSymbol, List<(int, List<DoorSymbol>)>> rules = new Dictionary<DoorSymbol, List<(int, List<DoorSymbol>)>>{
        { DoorSymbol.DOOR, new List<(int, List<DoorSymbol>)>(){  // height division
            (1, new List<DoorSymbol>(){DoorSymbol.Nothing}),
            (5, new List<DoorSymbol>(){DoorSymbol.S_1_G}),
            (5, new List<DoorSymbol>(){DoorSymbol.S_1_P}),
            (5, new List<DoorSymbol>(){DoorSymbol.S_1_G, DoorSymbol.S_1_G}),
            (5, new List<DoorSymbol>(){DoorSymbol.S_1_G, DoorSymbol.S_1_P}),
            (5, new List<DoorSymbol>(){DoorSymbol.S_1_P, DoorSymbol.S_1_P}),
            (5, new List<DoorSymbol>(){DoorSymbol.S_1_G, DoorSymbol.S_1_G, DoorSymbol.S_1_G}),
            (5, new List<DoorSymbol>(){DoorSymbol.S_1_G, DoorSymbol.S_1_G, DoorSymbol.S_1_P}),
            (5, new List<DoorSymbol>(){DoorSymbol.S_1_G, DoorSymbol.S_1_P, DoorSymbol.S_1_P}),
            (5, new List<DoorSymbol>(){DoorSymbol.S_1_P, DoorSymbol.S_1_P, DoorSymbol.S_1_P}),

            (5, new List<DoorSymbol>(){DoorSymbol.Nothing}),
            (5, new List<DoorSymbol>(){DoorSymbol.S_2_G}),
            (5, new List<DoorSymbol>(){DoorSymbol.S_2_P}),
            (5, new List<DoorSymbol>(){DoorSymbol.S_2_G, DoorSymbol.S_2_G}),
            (5, new List<DoorSymbol>(){DoorSymbol.S_2_G, DoorSymbol.S_2_P}),
            (5, new List<DoorSymbol>(){DoorSymbol.S_2_P, DoorSymbol.S_2_P}),
            (5, new List<DoorSymbol>(){DoorSymbol.S_2_G, DoorSymbol.S_2_G, DoorSymbol.S_2_G}),
            (5, new List<DoorSymbol>(){DoorSymbol.S_2_G, DoorSymbol.S_2_G, DoorSymbol.S_2_P}),
            (5, new List<DoorSymbol>(){DoorSymbol.S_2_G, DoorSymbol.S_2_P, DoorSymbol.S_2_P}),
            (5, new List<DoorSymbol>(){DoorSymbol.S_2_P, DoorSymbol.S_2_P, DoorSymbol.S_2_P}),

            (5, new List<DoorSymbol>(){DoorSymbol.Nothing}),
            (5, new List<DoorSymbol>(){DoorSymbol.S_3_G}),
            (5, new List<DoorSymbol>(){DoorSymbol.S_3_P}),
            (5, new List<DoorSymbol>(){DoorSymbol.S_3_G, DoorSymbol.S_3_G}),
            (5, new List<DoorSymbol>(){DoorSymbol.S_3_G, DoorSymbol.S_3_P}),
            (5, new List<DoorSymbol>(){DoorSymbol.S_3_P, DoorSymbol.S_3_P}),
            (5, new List<DoorSymbol>(){DoorSymbol.S_3_G, DoorSymbol.S_3_G, DoorSymbol.S_3_G}),
            (5, new List<DoorSymbol>(){DoorSymbol.S_3_G, DoorSymbol.S_3_G, DoorSymbol.S_3_P}),
            (5, new List<DoorSymbol>(){DoorSymbol.S_3_G, DoorSymbol.S_3_P, DoorSymbol.S_3_P}),
            (5, new List<DoorSymbol>(){DoorSymbol.S_3_P, DoorSymbol.S_3_P, DoorSymbol.S_3_P}),
        }},
        // width division
        { DoorSymbol.S_1_G, new List<(int, List<DoorSymbol>)>(){
            (1, new List<DoorSymbol>(){DoorSymbol.Glass}),
        }},
        { DoorSymbol.S_1_P, new List<(int, List<DoorSymbol>)>(){
            (1, new List<DoorSymbol>(){DoorSymbol.Panel}),
        }},
        { DoorSymbol.S_2_G, new List<(int, List<DoorSymbol>)>(){
            (1, new List<DoorSymbol>(){DoorSymbol.Glass, DoorSymbol.Glass}),
        }},
        { DoorSymbol.S_2_P, new List<(int, List<DoorSymbol>)>(){
            (1, new List<DoorSymbol>(){DoorSymbol.Panel, DoorSymbol.Panel}),
        }},
        { DoorSymbol.S_3_G, new List<(int, List<DoorSymbol>)>(){
            (1, new List<DoorSymbol>(){DoorSymbol.Glass, DoorSymbol.Glass, DoorSymbol.Glass}),
        }},
        { DoorSymbol.S_3_P, new List<(int, List<DoorSymbol>)>(){
            (1, new List<DoorSymbol>(){DoorSymbol.Panel, DoorSymbol.Panel, DoorSymbol.Panel}),
        }},
    };

    private static void ApplyRule(DoorSymbol nonTerminal, List<DoorSymbol> ruleToApply, ref Door door)
    {
        bool isVerticalSegmentation = nonTerminal == DoorSymbol.DOOR;  // vertical segmentation = divide x into n segments

        List<int> linePositionFactors = new List<int>();

        if (isVerticalSegmentation && ruleToApply.Contains(DoorSymbol.Nothing))
        {
            return;
        }

        if (isVerticalSegmentation)
        {
            door.SetNrOfVerticalSubdivisions(ruleToApply.Count, ruleToApply.Intersect(glassSyncSymbols).Count());
        }
        else
        {
            // since the segmentation is everywhere the same, defining it multiple times doesn't change anything
            // therefore we can do it multiple times
            door.SetNrOfHorizontalSubdivisions(ruleToApply.Count);
        }
    }

    private static bool IsTerminalOnly(List<DoorSymbol> ruleStr)
    {
        foreach (var ruleChar in ruleStr)
        {
            if (!DoorGrammar.terminals.Contains(ruleChar))
            {
                return false;
            }
        }
        return true;
    }

    private static int GetFirstNonTerminal(List<DoorSymbol> ruleStr)
    {
        for (int i = 0; i < ruleStr.Count; i++)
        {
            if (!DoorGrammar.terminals.Contains(ruleStr[i]))
            {
                return i;
            }
        }
        // should never happen
        return -1;
    }

    private static List<DoorSymbol> GetRandomRule(List<(int, List<DoorSymbol>)> possibleRules)
    {
        int[] rangeArray = new int[possibleRules.Count];
        int sum = 0;
        for (int i = 0; i < possibleRules.Count; i++)
        {
            sum += possibleRules[i].Item1;
            rangeArray[i] = sum;
        }

        int randomInt = DoorGrammar.rnd.Next(sum);

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

    public static Door GenerateDoor()
    {
        Door door = new Door();

        List<DoorSymbol> ruleStr = new List<DoorSymbol>() { DoorSymbol.DOOR };

        while (!DoorGrammar.IsTerminalOnly(ruleStr))
        {
            int index = DoorGrammar.GetFirstNonTerminal(ruleStr);

            List<(int, List<DoorSymbol>)> possibleRules = DoorGrammar.rules[ruleStr[index]];
            List<DoorSymbol> ruleToApply = DoorGrammar.GetRandomRule(possibleRules);
            DoorGrammar.ApplyRule(ruleStr[index], ruleToApply, ref door);
            ruleStr.RemoveAt(index);
            ruleStr.InsertRange(index, ruleToApply);
        }

        if (DoorGrammar.rnd.Next(100) < 50)  // 50%
        {
            door.Flip();
        }


        return door;
    }
}