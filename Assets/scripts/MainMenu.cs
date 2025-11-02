using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenu : MonoBehaviour
{
    [SerializeField] private string gameplayScene = "SampleScene";
    [SerializeField] private string settingsScene = "SettingsMenu";

    public void PlayGame()
    {
        SceneManager.LoadScene(gameplayScene, LoadSceneMode.Single);
    }

    public void OpenSettings()
    {
        SceneManager.LoadScene(settingsScene, LoadSceneMode.Single);
    }

    public void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;   // sai do Play no Editor
#else
        Application.Quit();                                // sai no build
#endif
    }
}
