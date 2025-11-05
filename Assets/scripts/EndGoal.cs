using UnityEngine;
using UnityEngine.SceneManagement;

public class EndGoal : MonoBehaviour
{
    [SerializeField] private string nextSceneName; // nome da próxima cena

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player")) // garante que só o jogador ativa
        {
            // Carrega a próxima cena
            SceneManager.LoadScene(nextSceneName);
        }
    }
}
