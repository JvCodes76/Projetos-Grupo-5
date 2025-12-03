using UnityEngine;
using UnityEngine.SceneManagement;

public class GameOver_Script : MonoBehaviour
{
    // Não precisa de uma referência de campo, podemos acessar o PlayerData estaticamente.
    // private PlayerData playerData; // Não é mais necessário

    /*
    private void Start()
    {
        // Esta linha não é mais necessária: playerData = PlayerData.Instance;
    }
    */

    public void RestartButton()
    {
        // 1. Pega o índice da cena que está ativa para recarregar
        int currentSceneIndex = SceneManager.GetActiveScene().buildIndex;

        // 2. CRUCIAL: Destrói o objeto persistente do PlayerData (o seu Player)
        // Isso remove o jogador que está na posição de morte, forçando o Unity a usar 
        // a versão que está na cena na posição inicial.
        if (PlayerData.Instance != null)
        {
            Destroy(PlayerData.Instance.gameObject);
        }

        // 3. Recarrega a cena. O novo Player será instanciado na posição inicial.
        SceneManager.LoadScene(currentSceneIndex);
    }
    
    public void ExitButton()
    {
        // 1. Destrói o PlayerData antes de voltar ao menu (para não ter música de menu persistente ou bugs)
        if (PlayerData.Instance != null)
        {
            Destroy(PlayerData.Instance.gameObject);
        }

        // 2. Carrega o menu principal
        SceneManager.LoadScene("MainMenu");
    }
}