using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

public class SceneController : MonoBehaviour
{
    public static SceneController instance;
    private PlayerData playerData;
    private bool isInitialized = false;

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
            StartCoroutine(Initialize());
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private IEnumerator Initialize()
    {
        yield return new WaitUntil(() => PlayerData.Instance != null);

        playerData = PlayerData.Instance;
        playerData.currentLevel = SceneManager.GetActiveScene().buildIndex;

        SceneManager.sceneLoaded += OnSceneLoaded;
        isInitialized = true;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (isInitialized && playerData != null)
        {
            playerData.currentLevel = scene.buildIndex;
        }
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }
    }

    public void NextLevel()
    {
        if (!isInitialized || playerData == null) return;

        int nextLevel = SceneManager.GetActiveScene().buildIndex + 1;

        if (nextLevel < SceneManager.sceneCountInBuildSettings)
        {
            SceneManager.LoadSceneAsync(nextLevel);
        }
        else
        {
            Debug.LogWarning("No more levels available!");
        }
    }

    public void LoadScene(string sceneName)
    {
        SceneManager.LoadSceneAsync(sceneName);
    }
}