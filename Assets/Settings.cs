using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Linq;


public class Settings : MonoBehaviour
{

    [SerializeField] public TMP_Dropdown resolutionDropdown;

    private List<Resolution> resolutions;

    private void InitSettingMenu()
    {
        resolutions = new List<Resolution>();

        foreach (Resolution resolution in Screen.resolutions)
        {
            bool alreadyInList = resolutions.FindIndex(r => r.width == resolution.width && r.height == resolution.height) != -1;

            if (!alreadyInList)
            {
                resolutions.Add(resolution);
            }
        }

        resolutions.Sort((r1, r2) =>
        //Comparer for resolutions
            {
                int widthDifference = r2.width - r1.width;
                int heightDifference = r2.height - r1.height;

                if (widthDifference != 0)
                    return widthDifference;

                //In case the width of both resolutions is the same,
                //compare using the height difference.
                return heightDifference;
            }
        );

        // Create string list of resolutions for dropdown
        List<string> resolutionOptions = resolutions.ToList().ConvertAll(r => $"{r.width} x {r.height}");

        // Set dropdown index based on current resolution
        Resolution res = new Resolution()
        {
            width = Screen.width,
            height = Screen.height
        };

        int resolutionPosition = resolutions.FindIndex(r => r.width == res.width && r.height == res.height);

        // Assign values to dropdown menu
        resolutionDropdown.AddOptions(resolutionOptions);
        resolutionDropdown.value = resolutionPosition;
        resolutionDropdown.RefreshShownValue();

        // string[] qualityDropdown =
        // {
        //    GameMultiLang.GetTraduction("low"),
        //    GameMultiLang.GetTraduction("medium"),
        //    GameMultiLang.GetTraduction("high"),
        //    GameMultiLang.GetTraduction("veryHigh"),
        //    GameMultiLang.GetTraduction("ultra")
        // };

        // graphicDropdown.ClearOptions();
        // graphicDropdown.AddOptions(qualityDropdown.ToList());
        // graphicDropdown.value = PlayerPrefs.GetInt("qualityLevel", 0);
    }

    void OnEnable()
    {
        InitSettingMenu();
    }

    // Update is called once per frame
    void Update()
    {

    }
}
