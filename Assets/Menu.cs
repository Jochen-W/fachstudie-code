using UnityEngine;
using UnityEngine.SceneManagement;

public class Menu : MonoBehaviour
{

    [Tooltip("Show menu in UI")]
    public GameObject menuPanel;

    public GameObject settingsMenu;

    public void OpenMapScene()
    {
        CachedRequestMaker.ClearCache();
        SceneManager.LoadScene("ZoomableMap", LoadSceneMode.Single);
    }

    public void ExitApplication()
    {
        Application.Quit();
    }

    public void handleSettings()
    {
        settingsMenu.SetActive(!settingsMenu.activeSelf);
        menuPanel.SetActive(!menuPanel.activeSelf);
    }

    public void CloseMenu()
    {
        menuPanel.SetActive(!menuPanel.activeSelf);
    }

    public void ToggleMenu()
    {
        if (settingsMenu.activeSelf)
        {
            settingsMenu.SetActive(!settingsMenu.activeSelf);
        }
        else
        {
            menuPanel.SetActive(!menuPanel.activeSelf);
        }
    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            ToggleMenu();
        }
    }
}
