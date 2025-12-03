using UnityEngine;

public class PlayerData : MonoBehaviour
{
    public static PlayerData Instance { get; private set; }

    public float agility = 1f;
    public float strength = 1f;
    public int maxAirJumps = 1;
    public int currentLevel;
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
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void SaveData()
    {
        PlayerPrefs.SetFloat("Agility", agility);
        PlayerPrefs.SetFloat("Strength", strength);
        PlayerPrefs.SetString("PlayerName", playerName);
        PlayerPrefs.Save();
    }

    public void LoadData()
    {
        agility = PlayerPrefs.GetFloat("Agility", 1f);
        strength = PlayerPrefs.GetFloat("Strength", 1f);
        playerName = PlayerPrefs.GetString("PlayerName", "Cyborg");
    }
}