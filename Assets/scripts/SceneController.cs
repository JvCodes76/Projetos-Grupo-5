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
    public int[] gameLevelIndexes = { 2, 3, 4};
    public string endGameSceneName = "EndGame";

    [Header("Referências")]
    [SerializeField] private PlayerData playerData;

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

        FindPlayerData();
    }

    private void Start()
    {
        ProcessCurrentScene();
    }

    private void FindPlayerData()
    {
        if (playerData == null)
        {
            playerData = FindObjectOfType<PlayerData>();
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        FindPlayerData();
        DestroyExistingPlayer();
        ProcessCurrentScene();
    }

    private void ProcessCurrentScene()
    {
        if (instance != this) return;

        Scene scene = SceneManager.GetActiveScene();
        
        // Se for a cena final ou menu, não faz spawn do player.
        if (scene.name == endGameSceneName || scene.buildIndex == 0)
        {
             DestroyExistingPlayer();
             return;
        }

        Debug.Log($"SceneController: Processando cena: {scene.name} (Índice: {scene.buildIndex})");

        if (ShouldSpawnPlayerInThisScene(scene))
        {
            SpawnPlayerIfNotExists();
        }
        else
        {
            DestroyExistingPlayer();
        }
    }

    private bool ShouldSpawnPlayerInThisScene(Scene scene)
    {
        return gameLevelIndexes.Contains(scene.buildIndex);
    }

    private void SpawnPlayerIfNotExists()
    {
        GameObject[] physicalPlayers = GameObject.FindGameObjectsWithTag("Player");
        bool hasPhysicalPlayer = physicalPlayers.Any(player => player.GetComponent<PlayerData>() == null);

        if (!hasPhysicalPlayer)
        {
            SpawnPlayer();
        }
        else
        {
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
        }

        if (playerPrefab != null)
        {
            GameObject newPlayer = Instantiate(playerPrefab, spawnPosition, spawnRotation);
            OnPlayerSpawned?.Invoke(newPlayer);
        }
    }

    private void MovePlayerToSpawnPoint(GameObject player)
    {
        GameObject spawnPoint = GameObject.FindWithTag("SpawnPoint");

        if (spawnPoint != null)
        {
            player.transform.position = spawnPoint.transform.position;
            player.transform.rotation = spawnPoint.transform.rotation;
        }
    }

    private void DestroyExistingPlayer()
    {
        GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
        foreach (GameObject player in players)
        {
            if (player.GetComponent<PlayerData>() == null)
            {
                Destroy(player);
            }
        }
    }

    public void NextLevel()
    {
        // CHAVE: Salva o tempo da fase atual ANTES de mudar de cena
        Timer currentTimer = FindObjectOfType<Timer>();
        if (currentTimer != null && playerData != null)
        {
            playerData.AddPlayTime(currentTimer.CurrentTime);
            Debug.Log($"SceneController: Tempo da fase ({currentTimer.CurrentTime}s) adicionado ao total. Total acumulado: {playerData.totalTimePlayed}s.");
        }

        int currentBuildIndex = SceneManager.GetActiveScene().buildIndex;
        int currentIndexInArray = Array.IndexOf(gameLevelIndexes, currentBuildIndex);

        if (currentIndexInArray != -1 && currentIndexInArray + 1 < gameLevelIndexes.Length)
        {
            int nextLevelIndex = gameLevelIndexes[currentIndexInArray + 1];
            SceneManager.LoadScene(nextLevelIndex);

            if (playerData != null)
            {
                playerData.currentLevel = nextLevelIndex;
                playerData.SaveData();
            }
        }
        else
        {
            // FIM DE JOGO: Carrega a tela de estatísticas
            Debug.Log("SceneController: Todas as fases concluídas! Carregando EndGame.");
            SceneManager.LoadScene(endGameSceneName); // <<<< Vai para EndGame
        }
    }

    public void LoadScene(string sceneName) => SceneManager.LoadScene(sceneName);
    public void LoadScene(int sceneIndex) => SceneManager.LoadScene(sceneIndex);

    private void OnDestroy()
    {
        if (instance == this)
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }
    }
}