using UnityEngine;
using UnityEngine.SceneManagement;

public class SettingsBack : MonoBehaviour
{
    [SerializeField] private string mainMenuScene = "MainMenu";
    public void Back()
    {
        SceneManager.LoadScene(mainMenuScene, LoadSceneMode.Single);
    }
}
