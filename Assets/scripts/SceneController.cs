using UnityEngine;
using UnityEngine.SceneManagement;
using System.Linq;
using System;

public class SceneController : MonoBehaviour
{
    public static SceneController instance;
    public static event Action<GameObject> OnPlayerSpawned;

    [Header("Configurações")]
    public GameObject playerPrefab;
    public int[] gameLevelIndexes = {2, 3, 4, 5, 6};

    private void Awake()
    {
        SceneController[] controllers = FindObjectsOfType<SceneController>();

        if (controllers.Length > 1)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void Start()
    {
        ProcessCurrentScene();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Destroi qualquer player físico existente primeiro
        DestroyExistingPlayer();

        // Processa a cena atual
        ProcessCurrentScene();
    }

    private void ProcessCurrentScene()
    {
        if (instance != this) return;

        Scene scene = SceneManager.GetActiveScene();
        Debug.Log($"Processando cena: {scene.name} (Índice: {scene.buildIndex})");

        if (ShouldSpawnPlayerInThisScene(scene))
        {
            Debug.Log("Spawning player...");
            SpawnPlayerIfNotExists();
        }
        else
        {
            Debug.Log("Cena não é nível de jogo, destruindo player...");
            DestroyExistingPlayer();
        }
    }

    private bool ShouldSpawnPlayerInThisScene(Scene scene)
    {
        return gameLevelIndexes.Contains(scene.buildIndex);
    }

    private void SpawnPlayerIfNotExists()
    {
        // Verifica se existe algum player físico (não confundir com PlayerData)
        GameObject[] physicalPlayers = GameObject.FindGameObjectsWithTag("Player");
        bool hasPhysicalPlayer = physicalPlayers.Any(player => player.GetComponent<PlayerData>() == null);

        if (!hasPhysicalPlayer)
        {
            SpawnPlayer();
        }
        else
        {
            Debug.Log("Player físico já existe na cena, movendo para spawn point...");
            MovePlayerToSpawnPoint(physicalPlayers[0]);
        }
    }

    private void SpawnPlayer()
    {
        GameObject spawnPoint = GameObject.FindWithTag("SpawnPoint");

        Vector3 spawnPosition = Vector3.zero;
        Quaternion spawnRotation = Quaternion.identity;

        if (spawnPoint != null)
        {
            spawnPosition = spawnPoint.transform.position;
            spawnRotation = spawnPoint.transform.rotation;
            Debug.Log($"Spawning em: {spawnPoint.name} - Posição: {spawnPosition}");
        }
        else
        {
            Debug.LogWarning("SpawnPoint não encontrado. Spawnando na origem.");
        }

        if (playerPrefab != null)
        {
            GameObject newPlayer = Instantiate(playerPrefab, spawnPosition, spawnRotation);
            OnPlayerSpawned?.Invoke(newPlayer);
            Debug.Log("Player spawnado com sucesso.");
        }
        else
        {
            Debug.LogError("PlayerPrefab não atribuído no SceneController!");
        }
    }

    private void MovePlayerToSpawnPoint(GameObject player)
    {
        GameObject spawnPoint = GameObject.FindWithTag("SpawnPoint");

        if (spawnPoint != null)
        {
            player.transform.position = spawnPoint.transform.position;
            player.transform.rotation = spawnPoint.transform.rotation;
            Debug.Log("Player movido para o spawn point: " + spawnPoint.name);
        }
    }

    private void DestroyExistingPlayer()
    {
        GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
        foreach (GameObject player in players)
        {
            // Destroi apenas players físicos, não o PlayerData
            if (player.GetComponent<PlayerData>() == null)
            {
                Destroy(player);
                Debug.Log("Player físico destruído: " + player.name);
            }
        }
    }

    public void NextLevel()
    {
        int currentBuildIndex = SceneManager.GetActiveScene().buildIndex;
        int currentIndexInArray = Array.IndexOf(gameLevelIndexes, currentBuildIndex);

        if (currentIndexInArray != -1 && currentIndexInArray + 1 < gameLevelIndexes.Length)
        {
            int nextLevelIndex = gameLevelIndexes[currentIndexInArray + 1];
            SceneManager.LoadScene(nextLevelIndex);

            // Atualiza o nível no PlayerData
            if (PlayerData.Instance != null)
            {
                PlayerData.Instance.currentLevel = nextLevelIndex;
                PlayerData.Instance.SaveData();
            }
        }
        else
        {
            Debug.LogWarning("Não há mais níveis disponíveis! Voltando para o menu.");
            SceneManager.LoadScene(0);
        }
    }

    public void LoadScene(string sceneName)
    {
        SceneManager.LoadScene(sceneName);
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }
    }
}