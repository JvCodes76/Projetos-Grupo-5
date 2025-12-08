using UnityEngine;
using UnityEngine.SceneManagement;

public class GameOver_Script : MonoBehaviour
{
    [Header("Referências")]
    [SerializeField] private PlayerData playerData; // Referência ao PlayerData

    private void Start()
    {
        // Tenta encontrar o PlayerData se não foi atribuído no Inspector
        if (playerData == null)
        {
            playerData = FindObjectOfType<PlayerData>();
        }
    }

    public void RestartButton()
    {
        // 1. Pega o índice da cena que está ativa para recarregar
        int currentSceneIndex = SceneManager.GetActiveScene().buildIndex;

        // 2. Destrói o objeto do PlayerData se existir
        if (playerData != null)
        {
            Destroy(playerData.gameObject);
        }
        else
        {
            // Se não encontrou pela referência, tenta encontrar na cena
            PlayerData existingPlayerData = FindObjectOfType<PlayerData>();
            if (existingPlayerData != null)
            {
                Destroy(existingPlayerData.gameObject);
            }
        }

        // 3. Recarrega a cena. O novo Player será instanciado na posição inicial.
        SceneManager.LoadScene(currentSceneIndex);
    }

    public void ExitButton()
    {
        // 1. Destrói o PlayerData antes de voltar ao menu
        if (playerData != null)
        {
            Destroy(playerData.gameObject);
        }
        else
        {
            // Se não encontrou pela referência, tenta encontrar na cena
            PlayerData existingPlayerData = FindObjectOfType<PlayerData>();
            if (existingPlayerData != null)
            {
                Destroy(existingPlayerData.gameObject);
            }
        }

        // 2. Carrega o menu principal
        SceneManager.LoadScene("MainMenu");
    }
}