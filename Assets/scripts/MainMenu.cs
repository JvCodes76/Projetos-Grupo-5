using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI; 

public class MainMenu : MonoBehaviour
{
    // Chave usada pelo PlayerData para salvar o nível
    private const string SavedLevelKey = "SavedLevel"; 

    [Header("Configuração de Cenas")]
    [SerializeField] private string gameplayScene = "SampleScene"; 
    [SerializeField] private string settingsScene = "SettingsMenu";

    [Header("UI")]
    [SerializeField] private Button continueButton; 

    private void Start()
    {
        // Garante que o botão 'Continuar' só esteja ativo se houver um save válido.
        if (continueButton != null)
        {
            // Verifica diretamente se a chave de nível existe no disco
            if (PlayerPrefs.HasKey(SavedLevelKey))
            {
                continueButton.interactable = true;
            }
            else
            {
                continueButton.interactable = false;
            }
        }
    }

    // --- BOTÃO: NOVO JOGO ---
    public void PlayGame() 
    {
        // 1. Reseta os dados (e apaga a chave "SavedLevel" do disco)
        if (PlayerData.Instance != null)
        {
            PlayerData.Instance.ResetData();
            // 💡 Se o PlayerData persistente existe no Menu, ele é destruído aqui
            Destroy(PlayerData.Instance.gameObject); 
        }
        else
        {
            // Se já foi destruído pelo Exit Button, garante o reset manual das chaves
            PlayerPrefs.DeleteKey(SavedLevelKey);
            PlayerPrefs.Save();
        }

        // 2. Para a música do menu
        if (MenuMusicController.Instance != null)
        {
            MenuMusicController.Instance.StopMusicAndDestroy();
        }
        
        // 3. Carrega a primeira fase
        SceneManager.LoadScene(gameplayScene, LoadSceneMode.Single);
    }

    private void Update()
    {
        // Pressione F12 (ou qualquer tecla que você queira)
        if (Input.GetKeyDown(KeyCode.F12)) 
        {
            PlayerPrefs.DeleteAll();
            Debug.Log("TODOS OS DADOS DE SAVE FORAM APAGADOS!");
            // Recarrega o menu para aplicar a mudança
            SceneManager.LoadScene("MainMenu"); 
        }
    }

    // --- BOTÃO: CONTINUAR ---
    public void ContinueGame()
    {
        // 1. 💡 NOVO: Verifica se o save existe e lê o índice da fase DIRETAMENTE do disco
        if (PlayerPrefs.HasKey(SavedLevelKey))
        {
            int levelToLoad = PlayerPrefs.GetInt(SavedLevelKey);
            
            // 2. Para a música do menu
            if (MenuMusicController.Instance != null)
            {
                MenuMusicController.Instance.StopMusicAndDestroy();
            }

            // 3. Destrói a instância do PlayerData se ela existir no menu (necessário se o player não foi destruído no Exit)
            if (PlayerData.Instance != null)
            {
                 Destroy(PlayerData.Instance.gameObject);
            }

            // 4. Carrega a cena salva. O novo PlayerData.Awake() carregará os dados automaticamente.
            if (levelToLoad > 0)
            {
                SceneManager.LoadScene(levelToLoad);
            }
            else
            {
                // Fallback de segurança (deve ser a primeira fase)
                SceneManager.LoadScene(gameplayScene);
            }
        }
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