using Roguelike.Events;
using Roguelike.Movement;
using UnityEngine;

/// <summary>
/// Objetivo da fase. Detectado pela varredura de triggers do tick (DS-12: <see cref="IPlayerTickTrigger"/>), e não
/// por OnTriggerEnter2D: o LevelGoalReached sai no tick exato em que o corpo entra, antes da checagem do LevelTimer
/// no mesmo tick (empate favorece o jogador). Emite LevelGoalReached uma vez.
/// </summary>
public class EndGoal : MonoBehaviour, IPlayerTickTrigger
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

    public void OnPlayerTickEnter(Component player, long tick)
    {
        if (alreadyTriggered) return;
        if (player == null || !player.CompareTag("Player")) return;

        alreadyTriggered = true;
        TriggerVictory(tick);
    }

    private void TriggerVictory(long tick)
    {
        Debug.Log($"[EndGoal] - Jogador alcançou o objetivo no tick {tick}");

        PlayVictorySound();

        // Desativa o colisor para evitar múltiplas chamadas
        Collider2D goalCollider = GetComponent<Collider2D>();
        if (goalCollider != null)
        {
            goalCollider.enabled = false;
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

    // Reativa o goal (trigger, collider e sprite; útil para testes). Não cancela o fim de fase que o
    // RunManager já tenha iniciado ao ouvir LevelGoalReached: ele carrega a próxima cena só depois do
    // resultado e da escolha do upgrade.
    public void ResetGoal()
    {
        alreadyTriggered = false;

        Collider2D goalCollider = GetComponent<Collider2D>();
        if (goalCollider != null)
        {
            goalCollider.enabled = true;
        }

        SpriteRenderer spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer != null)
        {
            spriteRenderer.enabled = true;
        }
    }
}
