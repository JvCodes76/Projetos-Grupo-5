using UnityEngine;
using UnityEngine.SceneManagement;
public class GameOver_Script : MonoBehaviour
{
    private PlayerData playerData;
    private void Start()
    {
        playerData = PlayerData.Instance;
    }
    public void RestartButton()
    {
        if (playerData != null)
        {
            SceneManager.LoadScene(playerData.currentLevel);
        }
        else
        {
            Debug.LogWarning("PlayerData não encontrado! Carregando cena padrão.");
            SceneManager.LoadScene(2);
        }
    }
    public void ExitButton()
    {
        SceneManager.LoadScene("MainMenu");
    }
}