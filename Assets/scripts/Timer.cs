using UnityEngine;
using TMPro;
using System;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using Roguelike.Events;

public class Timer : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private TextMeshProUGUI timerText;
    [SerializeField] private GameObject gameOverScreen;
    [SerializeField] private GameObject shopScreenPrefab; // Agora é um prefab

    [Header("Config")]
    [SerializeField] private bool autoStart = true;
    [SerializeField] private bool isCountingUp = true;
    [SerializeField] private float startTime = 60f;

    private float currentTime;
    private bool timerActive = false;

    private void Awake()
    {
        if (timerText == null)
        {
            timerText = GetComponentInChildren<TextMeshProUGUI>();
        }
    }

    private void OnEnable()
    {
        EventBus<PlayerDied>.Subscribe(HandlePlayerDied);
        EventBus<LevelGoalReached>.Subscribe(HandleLevelGoalReached);
    }

    private void OnDisable()
    {
        EventBus<PlayerDied>.Unsubscribe(HandlePlayerDied);
        EventBus<LevelGoalReached>.Unsubscribe(HandleLevelGoalReached);
    }

    private void HandlePlayerDied(PlayerDied evt)
    {
        StopTimer();
        ShowGameOver();
        PlayerData.Instance?.SaveData();
    }

    private void HandleLevelGoalReached(LevelGoalReached evt)
    {
        StopTimer();

        if (PlayerData.Instance != null)
        {
            PlayerData.Instance.totalTimePlayed += CurrentTime;
            Debug.Log($"[Timer] - Tempo da fase ({CurrentTime}s) adicionado ao total. Total acumulado: {PlayerData.Instance.totalTimePlayed}s.");
        }
    }

    private void Start()
    {
        // Marca a fase atual como ponto de "Continuar"
        PlayerData playerData = PlayerData.Instance;
        if (playerData != null)
        {
            playerData.currentLevel = SceneManager.GetActiveScene().buildIndex;
            playerData.SaveData();
        }

        currentTime = isCountingUp ? 0f : Mathf.Max(0f, startTime);
        UpdateTimerDisplay();

        if (autoStart)
        {
            StartTimer();
        }
    }

    private void Update()
    {
        if (!timerActive) return;

        if (isCountingUp)
        {
            currentTime += Time.deltaTime;
        }
        else
        {
            currentTime -= Time.deltaTime;
            if (currentTime <= 0f)
            {
                currentTime = 0f;
                timerActive = false;

                if (timerText != null)
                {
                    timerText.color = Color.red;
                }

                ShowGameOver();

                if (PlayerData.Instance != null)
                {
                    PlayerData.Instance.SaveData();
                }

                // Tempo esgotado: quem trava o jogador é o characterMovement, ouvindo este evento
                EventBus<LevelTimeExpired>.Raise(new LevelTimeExpired());
                return;
            }
        }

        UpdateTimerDisplay();
    }

    // Ativa a tela de game over (idempotente: pode ser chamado mais de uma vez sem efeito colateral)
    private void ShowGameOver()
    {
        if (gameOverScreen != null)
        {
            gameOverScreen.SetActive(true);
        }
        else
        {
            Debug.LogWarning("[Timer] - GameOver Screen não atribuída no Timer!");
        }

        if (timerText != null)
        {
            timerText.gameObject.SetActive(false);
        }
    }

    private void UpdateTimerDisplay()
    {
        if (timerText == null) return;

        TimeSpan t = TimeSpan.FromSeconds(Mathf.Max(0f, currentTime));
        string text = (t.TotalHours >= 1.0)
            ? string.Format("{0:00}:{1:00}:{2:00}", (int)t.TotalHours, t.Minutes, t.Seconds)
            : string.Format("{0:00}:{1:00}", t.Minutes, t.Seconds);

        timerText.text = text;
    }

    public void StartTimer() => timerActive = true;
    public void StopTimer() => timerActive = false;

    public void ResetTimer(bool restart = false)
    {
        currentTime = isCountingUp ? 0f : Mathf.Max(0f, startTime);
        UpdateTimerDisplay();
        timerActive = false;

        if (restart)
        {
            StartTimer();
        }
    }

    public void AddTime(float secondsToAdd)
    {
        currentTime += secondsToAdd;
        UpdateTimerDisplay();
    }

    public void SubtractTime(float secondsToSubtract)
    {
        currentTime = Mathf.Max(0f, currentTime - secondsToSubtract);
        UpdateTimerDisplay();
    }

    public float CurrentTime => currentTime;
    public bool IsActive => timerActive;
    public bool IsCountingUp => isCountingUp;
}
