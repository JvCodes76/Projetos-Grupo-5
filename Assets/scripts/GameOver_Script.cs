using UnityEngine;
using UnityEngine.SceneManagement;

public class GameOver_Script : MonoBehaviour
{
    public void RestartButton()
    {
        SceneManager.LoadScene("PrimeiraFase");
    }

    public void ExitButton()
    {
        SceneManager.LoadScene("MainMenu");
    }
}
