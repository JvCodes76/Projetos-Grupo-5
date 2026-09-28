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
    // Índices de Build das fases jogáveis, na ordem (MainMenu=0, SettingsMenu=1, EndGame vem depois da última)
    public int[] gameLevelIndexes = { 2, 3, 4 };
    public string endGameSceneName = "EndGame";

    private void Awake()
    {
        if (instance != null && instance != this)
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
        // O jogador é identificado só pela tag "Player" (o PlayerData agora é um singleton separado)
        GameObject existingPlayer = GameObject.FindGameObjectWithTag("Player");

        if (existingPlayer == null)
        {
            SpawnPlayer();
        }
        else
        {
            MovePlayerToSpawnPoint(existingPlayer);
            OnPlayerSpawned?.Invoke(existingPlayer);
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
            Destroy(player);
        }
    }

    public void NextLevel()
    {
        PlayerData playerData = PlayerData.Instance;

        // CHAVE: Soma o tempo da fase atual ao total ANTES de mudar de cena
        Timer currentTimer = FindFirstObjectByType<Timer>();
        if (currentTimer != null && playerData != null)
        {
            currentTimer.StopTimer();
            playerData.totalTimePlayed += currentTimer.CurrentTime;
            Debug.Log($"SceneController: Tempo da fase ({currentTimer.CurrentTime}s) adicionado ao total. Total acumulado: {playerData.totalTimePlayed}s.");
        }

        int currentBuildIndex = SceneManager.GetActiveScene().buildIndex;
        int currentIndexInArray = Array.IndexOf(gameLevelIndexes, currentBuildIndex);

        if (currentIndexInArray != -1 && currentIndexInArray + 1 < gameLevelIndexes.Length)
        {
            int nextLevelIndex = gameLevelIndexes[currentIndexInArray + 1];

            // Salva a PRÓXIMA fase como ponto de "Continuar" (junto com o tempo acumulado)
            if (playerData != null)
            {
                playerData.currentLevel = nextLevelIndex;
                playerData.SaveData();
            }

            SceneManager.LoadScene(nextLevelIndex);
        }
        else
        {
            // FIM DE JOGO: salva o tempo total e carrega a tela de estatísticas
            if (playerData != null)
            {
                playerData.SaveData();
            }

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
