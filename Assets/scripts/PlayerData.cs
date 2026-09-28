using UnityEngine;
using System.Collections.Generic;

// Singleton persistente (DontDestroyOnLoad) com os dados do jogador.
// Existe UMA única instância durante o jogo inteiro; acesse sempre por PlayerData.Instance.
public class PlayerData : MonoBehaviour
{
    // Chaves do PlayerPrefs usadas por este script (ResetData apaga só estas, sem mexer nas configurações)
    private const string KeySavedLevel = "SavedLevel";
    private const string KeyCoinCount = "CoinCount";
    private const string KeyAgility = "Agility";
    private const string KeyStrength = "Strength";
    private const string KeyMaxAirJumps = "MaxAirJumps";
    private const string KeyPlayerName = "PlayerName";
    private const string KeyCanWallJump = "CanWallJump";
    private const string KeyCanGrapplingHook = "CanGrapplingHook";
    private const string KeyTotalTimePlayed = "TotalTimePlayed";
    private const string KeyInventory = "Inventory";
    private const string KeyShopPurchasePrefix = "ShopPurchase_";

    // Quantidade de itens da loja (ver ShopManager)
    public const int ShopItemCount = 5;

    private static PlayerData _instance;
    private static bool _isQuitting;

    // Acesso global. Se ainda não existir (ex.: Play direto numa fase), cria um automaticamente.
    public static PlayerData Instance
    {
        get
        {
            if (_instance == null && !_isQuitting && Application.isPlaying)
            {
                _instance = FindFirstObjectByType<PlayerData>();
                if (_instance == null)
                {
                    new GameObject("PlayerData (Auto)").AddComponent<PlayerData>(); // Awake define _instance
                }
            }
            return _instance;
        }
    }

    // DADOS DO JOGADOR
    public float agility = 1f;
    public float strength = 1f;
    public int maxAirJumps = 1;
    public int currentLevel = 1;
    public int coinCount = 100;

    [Header("Estatísticas Globais")]
    public float totalTimePlayed = 0f; // Tempo total acumulado de todas as fases

    public bool canWallJump = true;
    public bool canGrapplingHook = true;
    public string playerName = "Cyborg";
    public List<string> inventory = new List<string>();

    [Header("Loja")]
    public int[] shopPurchases = new int[ShopItemCount]; // Quantidade comprada de cada item da loja

    // Reseta o estado estático ao entrar no Play Mode (funciona mesmo com "Enter Play Mode Options" sem domain reload)
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        _instance = null;
        _isQuitting = false;
    }

    // Garante que sempre exista um PlayerData, mesmo dando Play direto numa fase
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        _ = Instance;
    }

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            // Já existe um PlayerData persistente (ex.: voltando ao MainMenu) — descarta este.
            // Se o objeto tiver outros componentes (ex.: um jogador antigo), remove só o PlayerData.
            if (GetComponents<Component>().Length > 2) Destroy(this);
            else Destroy(gameObject);
            return;
        }

        _instance = this;

        if (transform.parent != null)
        {
            transform.SetParent(null);
        }
        DontDestroyOnLoad(gameObject);

        LoadData();
    }

    private void OnDestroy()
    {
        if (_instance == this)
        {
            _instance = null;
        }
    }

    public void SaveData()
    {
        PlayerPrefs.SetInt(KeySavedLevel, currentLevel);
        PlayerPrefs.SetInt(KeyCoinCount, coinCount);
        PlayerPrefs.SetFloat(KeyAgility, agility);
        PlayerPrefs.SetFloat(KeyStrength, strength);
        PlayerPrefs.SetInt(KeyMaxAirJumps, maxAirJumps);
        PlayerPrefs.SetString(KeyPlayerName, playerName);
        PlayerPrefs.SetInt(KeyCanWallJump, canWallJump ? 1 : 0);
        PlayerPrefs.SetInt(KeyCanGrapplingHook, canGrapplingHook ? 1 : 0);

        // SALVA O TEMPO TOTAL
        PlayerPrefs.SetFloat(KeyTotalTimePlayed, totalTimePlayed);

        // SALVA INVENTÁRIO E COMPRAS DA LOJA
        PlayerPrefs.SetString(KeyInventory, string.Join("|", inventory));
        EnsureShopArray();
        for (int i = 0; i < shopPurchases.Length; i++)
        {
            PlayerPrefs.SetInt(KeyShopPurchasePrefix + i, shopPurchases[i]);
        }

        PlayerPrefs.Save();
        Debug.Log("PlayerData: Jogo Salvo! Fase: " + currentLevel + ", Moedas: " + coinCount);
    }

    public void LoadData()
    {
        currentLevel = PlayerPrefs.GetInt(KeySavedLevel, 1);
        coinCount = PlayerPrefs.GetInt(KeyCoinCount, 100);
        agility = PlayerPrefs.GetFloat(KeyAgility, 1f);
        strength = PlayerPrefs.GetFloat(KeyStrength, 1f);
        maxAirJumps = PlayerPrefs.GetInt(KeyMaxAirJumps, 1);
        playerName = PlayerPrefs.GetString(KeyPlayerName, "Cyborg");
        canWallJump = PlayerPrefs.GetInt(KeyCanWallJump, 1) == 1;
        canGrapplingHook = PlayerPrefs.GetInt(KeyCanGrapplingHook, 1) == 1;

        // CARREGA O TEMPO TOTAL
        totalTimePlayed = PlayerPrefs.GetFloat(KeyTotalTimePlayed, 0f);

        // CARREGA INVENTÁRIO E COMPRAS DA LOJA
        inventory = new List<string>();
        string savedInventory = PlayerPrefs.GetString(KeyInventory, "");
        if (!string.IsNullOrEmpty(savedInventory))
        {
            inventory.AddRange(savedInventory.Split('|'));
        }

        shopPurchases = new int[ShopItemCount];
        for (int i = 0; i < shopPurchases.Length; i++)
        {
            shopPurchases[i] = PlayerPrefs.GetInt(KeyShopPurchasePrefix + i, 0);
        }

        Debug.Log($"PlayerData: Dados carregados. Nível {currentLevel}, {coinCount} moedas, Tempo Total: {totalTimePlayed}s.");
    }

    // FUNÇÃO CHAVE: Acumula o tempo da fase atual ao total
    public void AddPlayTime(float timeInLevel)
    {
        totalTimePlayed += timeInLevel;
        SaveData();
    }

    // Novo jogo: apaga SÓ as chaves do save do jogo (volume, VSync etc. são preservados)
    public void ResetData()
    {
        currentLevel = 1;
        coinCount = 100;
        agility = 1f;
        strength = 1f;
        maxAirJumps = 1;
        canWallJump = true;
        canGrapplingHook = true;
        playerName = "Cyborg";
        totalTimePlayed = 0f; // Reseta o tempo
        inventory.Clear();
        shopPurchases = new int[ShopItemCount];

        PlayerPrefs.DeleteKey(KeySavedLevel);
        PlayerPrefs.DeleteKey(KeyCoinCount);
        PlayerPrefs.DeleteKey(KeyAgility);
        PlayerPrefs.DeleteKey(KeyStrength);
        PlayerPrefs.DeleteKey(KeyMaxAirJumps);
        PlayerPrefs.DeleteKey(KeyPlayerName);
        PlayerPrefs.DeleteKey(KeyCanWallJump);
        PlayerPrefs.DeleteKey(KeyCanGrapplingHook);
        PlayerPrefs.DeleteKey(KeyTotalTimePlayed);
        PlayerPrefs.DeleteKey(KeyInventory);
        for (int i = 0; i < ShopItemCount; i++)
        {
            PlayerPrefs.DeleteKey(KeyShopPurchasePrefix + i);
        }
        PlayerPrefs.Save();

        Debug.Log("PlayerData: Dados resetados para novo jogo");
    }

    public int GetShopPurchases(int itemID)
    {
        EnsureShopArray();
        return (itemID >= 0 && itemID < shopPurchases.Length) ? shopPurchases[itemID] : 0;
    }

    public void AddShopPurchase(int itemID)
    {
        EnsureShopArray();
        if (itemID >= 0 && itemID < shopPurchases.Length)
        {
            shopPurchases[itemID]++;
        }
    }

    private void EnsureShopArray()
    {
        if (shopPurchases == null || shopPurchases.Length < ShopItemCount)
        {
            int[] resized = new int[ShopItemCount];
            if (shopPurchases != null)
            {
                System.Array.Copy(shopPurchases, resized, shopPurchases.Length);
            }
            shopPurchases = resized;
        }
    }

    public void AddCoins(int amount)
    {
        coinCount += amount;
        SaveData();
    }

    public void RemoveCoins(int amount)
    {
        coinCount = Mathf.Max(0, coinCount - amount);
        SaveData();
    }

    public void LevelUp()
    {
        currentLevel++;
        SaveData();
    }

    public void AddToInventory(string item)
    {
        if (!inventory.Contains(item))
        {
            inventory.Add(item);
            SaveData();
        }
    }

    public void RemoveFromInventory(string item)
    {
        if (inventory.Contains(item))
        {
            inventory.Remove(item);
            SaveData();
        }
    }

    private void OnApplicationQuit()
    {
        _isQuitting = true;
        if (_instance == this)
        {
            SaveData();
        }
    }
}
