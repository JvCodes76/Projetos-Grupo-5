using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

public class EnemyBullet : MonoBehaviour
{
    [SerializeField] private float speed = 6f;
    private Vector2 direction;

    [SerializeField] private LayerMask groundLayer;

    // Referências necessárias
    private PlayerData playerData;
    private characterMovement characterMovement;
    private GameObject gameOverScreen;
    private TextMeshProUGUI timerText;

    private void Awake()
    {
        // Encontrar as referências necessárias
        playerData = FindObjectOfType<PlayerData>();
        characterMovement = FindObjectOfType<characterMovement>();

        // Procurar a tela de game over
        FindGameOverScreen();

        // Procurar o texto do timer
        Timer timer = FindObjectOfType<Timer>();
        if (timer != null)
        {
            timerText = timer.GetComponentInChildren<TextMeshProUGUI>();
        }
    }

    // Método para encontrar a tela de game over
    private void FindGameOverScreen()
    {
        // Procura por tag (funciona mesmo se estiver dentro de Canvas e desativado)
        gameOverScreen = GameObject.FindGameObjectWithTag("GameOver");

        // Se não encontrou, tenta procurar em todos os objetos (incluindo os desativados)
        if (gameOverScreen == null)
        {
            // Encontra todos os objetos do tipo Canvas (incluindo os desativados)
            Canvas[] allCanvases = Resources.FindObjectsOfTypeAll<Canvas>();
            foreach (Canvas canvas in allCanvases)
            {
                // Procura por um objeto com a tag "GameOverScreen" dentro do Canvas
                Transform[] children = canvas.GetComponentsInChildren<Transform>(true);
                foreach (Transform child in children)
                {
                    if (child.CompareTag("GameOver"))
                    {
                        gameOverScreen = child.gameObject;
                        break;
                    }
                }
                if (gameOverScreen != null) break;
            }
        }

        if (gameOverScreen == null)
        {
            Debug.LogError("GameOver Screen não encontrada! Certifique-se de que existe um objeto com a tag 'GameOverScreen' na cena.");
        }
        else
        {
            Debug.Log("GameOver Screen encontrada: " + gameOverScreen.name);
        }
    }

    public void SetDirection(Vector2 dir)
    {
        direction = dir.normalized;
    }

    private void Update()
    {
        transform.Translate(direction * speed * Time.deltaTime);
        Destroy(gameObject, 5f);
    }

    private void OnTriggerEnter2D(Collider2D col)
    {
        if (col.CompareTag("Player") || col.gameObject.layer == LayerMask.NameToLayer("Player"))
        {
            TriggerDeathSequence();
            Destroy(gameObject);
            return;
        }

        // chão
        if (((1 << col.gameObject.layer) & groundLayer) != 0)
        {
            Destroy(gameObject);
        }
    }

    private void OnCollisionEnter2D(Collision2D col)
    {
        if (((1 << col.gameObject.layer) & groundLayer) != 0)
        {
            Destroy(gameObject);
        }
    }

    private void TriggerDeathSequence()
    {
        Debug.Log("Sequência de morte iniciada");

        // Chamar o método Die do characterMovement
        if (characterMovement != null)
        {
            characterMovement.Die();
        }
        else
        {
            Debug.LogWarning("characterMovement não encontrado!");
        }

        // Mostrar tela de game over
        if (gameOverScreen != null)
        {
            gameOverScreen.SetActive(true);
            Debug.Log("GameOver Screen ativada");
        }
        else
        {
            Debug.LogError("GameOver Screen não encontrada! Não é possível mostrar a tela de game over.");
        }

        // Desativar o texto do timer se existir
        if (timerText != null)
        {
            timerText.gameObject.SetActive(false);
        }

        // Salvar dados do jogador
        if (playerData != null)
        {
            playerData.SaveData();
        }
    }
}