using Roguelike.Events;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MainMenu : MonoBehaviour
{
    [Header("Configuração de Cenas")]
    [SerializeField] private string settingsScene = "SettingsMenu";

    // Ligado quando a run começa: a fase carrega de forma assíncrona e, até lá, o menu continuaria clicável
    // (Settings carregaria por cima com a run ativa fora da fase).
    private bool runStarting;

    private void OnEnable()
    {
        EventBus<RunStarted>.Subscribe(HandleRunStarted);
    }

    private void OnDisable()
    {
        EventBus<RunStarted>.Unsubscribe(HandleRunStarted);
    }

    // --- BOTÃO: NOVA RUN ---
    public void PlayGame()
    {
        if (runStarting) return;

        // Quem carrega a fase é o RunManager (ouvinte deste evento): o menu só pede uma run nova.
        // Se a run começar, o RunStarted chega durante este Raise e trava o menu.
        Debug.Log("[MainMenu] - Nova run pedida");
        EventBus<NewRunRequested>.Raise(new NewRunRequested());
    }

    public void OpenSettings()
    {
        if (runStarting) return;

        SceneManager.LoadScene(settingsScene, LoadSceneMode.Single);
    }

    private void HandleRunStarted(RunStarted evt)
    {
        runStarting = true;

        // Só os botões desta cena: a RunUI (DDOL) também tem Selectables e não pode ser desligada.
        Selectable[] selectables = FindObjectsByType<Selectable>(FindObjectsSortMode.None);
        for (int i = 0; i < selectables.Length; i++)
        {
            if (selectables[i].gameObject.scene == gameObject.scene)
            {
                selectables[i].interactable = false;
            }
        }
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