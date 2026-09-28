using UnityEngine;
using TMPro;
using System;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

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

    // Referência ao jogador REAL da cena (spawnado pelo SceneController) — resolvida em runtime.
    // Não use referência do Inspector aqui: nas fases o jogador só existe depois do sceneLoaded.
    private characterMovement characterMovement;

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
        SceneController.OnPlayerSpawned += HandlePlayerSpawned;
    }

    private void OnDisable()
    {
        SceneController.OnPlayerSpawned -= HandlePlayerSpawned;
    }

    private void HandlePlayerSpawned(GameObject player)
    {
        if (player != null)
        {
            characterMovement = player.GetComponent<characterMovement>();
        }
    }

    // Busca o jogador só quando precisa (ele é spawnado depois do Awake deste script)
    private characterMovement GetPlayerMovement()
    {
        if (characterMovement == null)
        {
            characterMovement = FindFirstObjectByType<characterMovement>();
        }
        return characterMovement;
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

                // ATIVA A TELA DE GAME OVER (que é filha do timer)
                if (gameOverScreen != null)
                {
                    gameOverScreen.SetActive(true);
                }
                else
                {
                    Debug.LogWarning("GameOver Screen não atribuída no Timer!");
                }

                // Opcional: Desativa o texto do timer para não ficar visível
                if (timerText != null)
                {
                    timerText.gameObject.SetActive(false);
                }

                if (PlayerData.Instance != null)
                {
                    PlayerData.Instance.SaveData();
                }

                // Mata o jogador REAL da cena (não o prefab)
                characterMovement player = GetPlayerMovement();
                if (player != null)
                {
                    player.Die();
                }
                else
                {
                    Debug.LogWarning("Timer: tempo esgotado, mas o jogador não foi encontrado na cena!");
                }
                return;
            }
        }

        UpdateTimerDisplay();
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
