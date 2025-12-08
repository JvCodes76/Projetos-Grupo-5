using UnityEngine;

public class EndGoal : MonoBehaviour
{
    [Header("Configurações de Áudio")]
    [SerializeField] private AudioClip victorySound;
    [SerializeField] private float soundVolume = 1f;

    [Header("Configurações de Transição")]
    [SerializeField] private float delayToLoadNextLevel = 0.5f;

    private AudioSource audioSource;
    private bool alreadyTriggered = false;

    private void Start()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (alreadyTriggered) return;

        Debug.Log("Trigger entrou: " + other.name);

        if (other.CompareTag("Player"))
        {
            alreadyTriggered = true;
            TriggerVictory();
        }
    }

    private void TriggerVictory()
    {
        Debug.Log("Vitória! Player atingiu o goal.");

        // Reproduz som de vitória
        PlayVictorySound();

        // Desativa o colisor para evitar múltiplas chamadas
        Collider2D collider = GetComponent<Collider2D>();
        if (collider != null)
        {
            collider.enabled = false;
        }

        // Desativa outros componentes visuais opcionais
        SpriteRenderer spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer != null)
        {
            spriteRenderer.enabled = false;
        }

        // Chama o próximo nível com delay
        Invoke(nameof(LoadNextLevel), delayToLoadNextLevel);
    }

    private void PlayVictorySound()
    {
        if (victorySound != null && audioSource != null)
        {
            audioSource.PlayOneShot(victorySound, soundVolume);
        }
    }

    private void LoadNextLevel()
    {
        if (SceneController.instance != null)
        {
            SceneController.instance.NextLevel();
        }
        else
        {
            Debug.LogError("SceneController instance is null!");

            // Fallback: tenta encontrar a instância (MÉTODO ATUALIZADO)
            SceneController controller = FindFirstObjectByType<SceneController>();
            if (controller != null)
            {
                controller.NextLevel();
            }
            else
            {
                Debug.LogError("Nenhum SceneController encontrado na cena!");
            }
        }
    }

    // Método para resetar o goal (útil para testes)
    public void ResetGoal()
    {
        alreadyTriggered = false;

        Collider2D collider = GetComponent<Collider2D>();
        if (collider != null)
        {
            collider.enabled = true;
        }

        SpriteRenderer spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer != null)
        {
            spriteRenderer.enabled = true;
        }

        CancelInvoke(nameof(LoadNextLevel));
    }
}