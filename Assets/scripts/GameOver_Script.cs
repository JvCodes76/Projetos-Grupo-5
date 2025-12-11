using UnityEngine;
using UnityEngine.SceneManagement;

public class GameOver_Script : MonoBehaviour
{
    [Header("Referências")]
    [SerializeField] private PlayerData playerData; 

    private void Start()
    {
        if (playerData == null)
        {
            playerData = FindObjectOfType<PlayerData>();
        }
    }

    public void RestartButton()
    {
        int currentSceneIndex = SceneManager.GetActiveScene().buildIndex;

        SceneManager.LoadScene(currentSceneIndex);
    }

    public void ExitButton()
    {
        if (playerData != null)
        {
            Destroy(playerData.gameObject);
        }
        else
        {
            PlayerData existingPlayerData = FindObjectOfType<PlayerData>();
            if (existingPlayerData != null)
            {
                Destroy(existingPlayerData.gameObject);
            }
        }

        SceneManager.LoadScene("MainMenu");
    }
}