using UnityEngine;

public class PlayerData : MonoBehaviour
{
    public static PlayerData Instance { get; private set; }

    // DADOS DO JOGADOR
    // Removidas as variáveis savedPosX/Y/Z
    public float agility = 1f;
    public float strength = 1f;
    public int maxAirJumps = 1;
    public int currentLevel = 1; 
    public int coinCount = 100;
    public bool canWallJump = true;
    public bool canGrapplingHook = true;
    public string playerName = "Cyborg";
    public System.Collections.Generic.List<string> inventory = new System.Collections.Generic.List<string>();


    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            LoadData(); // Carrega os dados salvos ao iniciar o jogo
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void SaveData()
    {
        // Salva apenas o nível e as moedas (posição removida)
        PlayerPrefs.SetInt("SavedLevel", currentLevel);
        PlayerPrefs.SetInt("CoinCount", coinCount);
        
        PlayerPrefs.SetFloat("Agility", agility);
        PlayerPrefs.SetFloat("Strength", strength);
        PlayerPrefs.SetString("PlayerName", playerName);
        
        PlayerPrefs.Save();
        Debug.Log("Jogo Salvo! Fase: " + currentLevel);
    }

    public void LoadData()
    {
        // Carrega apenas o nível e as moedas (posição removida)
        // Padrão é 1, a primeira fase
        currentLevel = PlayerPrefs.GetInt("SavedLevel", 1); 
        coinCount = PlayerPrefs.GetInt("CoinCount", 100);

        agility = PlayerPrefs.GetFloat("Agility", 1f);
        strength = PlayerPrefs.GetFloat("Strength", 1f);
        playerName = PlayerPrefs.GetString("PlayerName", "Cyborg");
    }
    
    // Método para ser chamado no "Novo Jogo"
    public void ResetData() 
    {
        currentLevel = 1; // Volta para a primeira fase
        coinCount = 0;
        
        // Remove a chave de nível para que o "Continuar" seja desativado
        PlayerPrefs.DeleteKey("SavedLevel"); 
        
        SaveData();
        LoadData(); 
    }
}