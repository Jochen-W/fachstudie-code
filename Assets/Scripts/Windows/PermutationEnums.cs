using System;
using System.Collections.Generic;

public enum WindowShape : int
{
    NORMAL = 70,  //no subdivision
    ROUND = 20,  //subdivided, lower part is normal
    POINTED = 10,  //subdivided, lower part is normal
}

public enum WindowLayout : int
{
    SINGLE = 70,
    PAIRED = 10,
    PAIRED_FLIPPED = 20,
}

class RandomPicker
{
    public static Random rand = new Random();
    public static T GetRandom<T>() where T : Enum
    {
        Dictionary<T, int> RangeDict = new Dictionary<T, int>();
        int sum = 0;
        foreach (T enumVal in Enum.GetValues(typeof(WindowShape)))
        {
            sum += (int)(object)enumVal;
            RangeDict.Add(enumVal, sum);
        }

        int randomInt = RandomPicker.rand.Next(sum);

        foreach ((T key, int value) in RangeDict)
        {
            if (randomInt < value)
            {
                return key;
            }
        }

        // this should never happen
        return (T)Enum.GetValues(typeof(WindowShape)).GetValue(0);
    }
}