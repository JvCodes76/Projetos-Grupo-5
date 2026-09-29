using Roguelike.Events;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenu : MonoBehaviour
{
    [Header("Configuração de Cenas")]
    [SerializeField] private string settingsScene = "SettingsMenu";

    // --- BOTÃO: NOVA RUN ---
    public void PlayGame()
    {
        // Quem carrega a fase é o RunManager (ouvinte deste evento): o menu só pede uma run nova.
        Debug.Log("[MainMenu] - Nova run pedida");
        EventBus<NewRunRequested>.Raise(new NewRunRequested());
    }

    public void OpenSettings()
    {
        SceneManager.LoadScene(settingsScene, LoadSceneMode.Single);
    }

    public void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}