using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneChanger : MonoBehaviour
{
    public void OpenMainScene()
    {
        SceneManager.LoadScene("SampleScene", LoadSceneMode.Single);
    }

}
