using UnityEngine;
using UnityEngine.SceneManagement;

public class GameOver_Script : MonoBehaviour
{
    public void RestartButton()
    {
        int currentSceneIndex = SceneManager.GetActiveScene().buildIndex;

        SceneManager.LoadScene(currentSceneIndex);
    }

    public void ExitButton()
    {
        // O PlayerData é um singleton persistente: NÃO destruir, só salvar antes de voltar ao menu
        if (PlayerData.Instance != null)
        {
            PlayerData.Instance.SaveData();
        }

        SceneManager.LoadScene("MainMenu");
    }
}
