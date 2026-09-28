using UnityEngine;
using Roguelike.Events;

public class EndGoal : MonoBehaviour
{
    [Header("Configurações de Áudio")]
    [SerializeField] private AudioClip victorySound;
    [SerializeField] private float soundVolume = 1f;

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

        // Avisa o resto do jogo que o objetivo da fase foi alcançado
        EventBus<LevelGoalReached>.Raise(new LevelGoalReached());
    }

    private void PlayVictorySound()
    {
        if (victorySound != null && audioSource != null)
        {
            audioSource.PlayOneShot(victorySound, soundVolume);
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
    }
}