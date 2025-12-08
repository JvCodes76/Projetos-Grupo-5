using UnityEngine;
using System.Collections.Generic;

public class PlayerData : MonoBehaviour
{
    // DADOS DO JOGADOR
    public float agility = 1f;
    public float strength = 1f;
    public int maxAirJumps = 1;
    public int currentLevel = 1;
    public int coinCount = 100;
    public bool canWallJump = true;
    public bool canGrapplingHook = true;
    public string playerName = "Cyborg";
    public List<string> inventory = new List<string>();

    private void Awake()
    {
        LoadData();
    }

    public void SaveData()
    {
        PlayerPrefs.SetInt("SavedLevel", currentLevel);
        PlayerPrefs.SetInt("CoinCount", coinCount);
        PlayerPrefs.SetFloat("Agility", agility);
        PlayerPrefs.SetFloat("Strength", strength);
        PlayerPrefs.SetInt("MaxAirJumps", maxAirJumps);
        PlayerPrefs.SetString("PlayerName", playerName);
        PlayerPrefs.SetInt("CanWallJump", canWallJump ? 1 : 0);
        PlayerPrefs.SetInt("CanGrapplingHook", canGrapplingHook ? 1 : 0);

        PlayerPrefs.Save();
        Debug.Log("Jogo Salvo! Fase: " + currentLevel + ", Moedas: " + coinCount);
    }

    public void LoadData()
    {
        currentLevel = PlayerPrefs.GetInt("SavedLevel", 1);
        coinCount = PlayerPrefs.GetInt("CoinCount", 100);
        agility = PlayerPrefs.GetFloat("Agility", 1f);
        strength = PlayerPrefs.GetFloat("Strength", 1f);
        maxAirJumps = PlayerPrefs.GetInt("MaxAirJumps", 1);
        playerName = PlayerPrefs.GetString("PlayerName", "Cyborg");
        canWallJump = PlayerPrefs.GetInt("CanWallJump", 1) == 1;
        canGrapplingHook = PlayerPrefs.GetInt("CanGrapplingHook", 1) == 1;

        Debug.Log("Dados carregados: Nível " + currentLevel + ", " + coinCount + " moedas");
    }

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
        inventory.Clear();

        PlayerPrefs.DeleteAll();
        Debug.Log("Dados resetados para novo jogo");
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
        SaveData();
    }
}
