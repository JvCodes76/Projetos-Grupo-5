using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MainMenu : MonoBehaviour
{
    // Chave usada pelo PlayerData para salvar o índice da fase
    private const string SavedLevelKey = "SavedLevel";
    
    // O índice da PRIMEIRA fase na Build Settings. O usuário confirmou que é 2.
    private const int FirstLevelIndex = 2; 

    [Header("Configuração de Cenas")]
    [SerializeField] private string settingsScene = "SettingsMenu";

    [Header("UI")]
    [SerializeField] private Button continueButton;

    private void Start()
    {
        if (continueButton != null)
        {
            int savedLevel = PlayerPrefs.GetInt(SavedLevelKey, 0);
            
            // O botão 'Continuar' só é ativo se o nível salvo for >= ao primeiro nível (índice 2)
            bool hasValidSave = savedLevel >= FirstLevelIndex;
            continueButton.interactable = hasValidSave;
            
            Debug.Log($"MainMenu: Nível salvo para CONTINUAR: {savedLevel}. Botão ativo: {hasValidSave}");
        }
    }

    // --- BOTÃO: NOVO JOGO ---
    public void PlayGame()
    {
        // 1. Reseta o save do jogo (fase, tempo, moedas, loja...). Apaga só as chaves do jogo:
        //    volume, VSync e outras configurações são preservados.
        PlayerData data = PlayerData.Instance;
        if (data != null)
        {
            data.ResetData();
        }
        else
        {
            PlayerPrefs.DeleteKey(SavedLevelKey);
            PlayerPrefs.Save();
        }

        // 2. Para a música do menu
        // if (MenuMusicController.Instance != null) { MenuMusicController.Instance.StopMusicAndDestroy(); }

        // 3. Carrega a PRIMEIRA fase usando o índice (2)
        SceneManager.LoadScene(FirstLevelIndex, LoadSceneMode.Single);
    }

    // --- BOTÃO: CONTINUAR ---
    public void ContinueGame()
    {
        if (PlayerPrefs.HasKey(SavedLevelKey))
        {
            int levelToLoad = PlayerPrefs.GetInt(SavedLevelKey);
            
            // DEBUG CRÍTICO: Mostra o valor que será carregado
            Debug.Log($"CONTINUAR: Tentando carregar o índice de Build: {levelToLoad}."); 

            // 2. Para a música do menu
            // if (MenuMusicController.Instance != null) { MenuMusicController.Instance.StopMusicAndDestroy(); }

            // 3. Carrega a cena salva
            if (levelToLoad >= FirstLevelIndex)
            {
                // Carrega a fase salva
                SceneManager.LoadScene(levelToLoad);
            }
            else
            {
                // Fallback de segurança 
                Debug.LogWarning($"CONTINUAR: Nível salvo inválido ({levelToLoad}). Carregando primeira fase ({FirstLevelIndex}).");
                SceneManager.LoadScene(FirstLevelIndex);
            }
        }
        else
        {
            Debug.LogError("CONTINUAR: Botão clicado, mas a chave 'SavedLevel' não existe no PlayerPrefs.");
        }
    }
    
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    // Pressione F12 para apagar o save (função de debug — só no Editor/Development Build)
    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.F12))
        {
            if (PlayerData.Instance != null) PlayerData.Instance.ResetData(); // Zera também os dados em memória
            PlayerPrefs.DeleteAll();
            Debug.Log("TODOS OS DADOS DE SAVE FORAM APAGADOS! Recarregando Menu.");
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }
    }
#endif

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