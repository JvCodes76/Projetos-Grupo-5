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

    [Header("Referências")]
    [SerializeField] private PlayerData playerData;
    [SerializeField] private characterMovement characterMovement;
    

    private float currentTime;
    private bool timerActive = false;

    private void Awake()
    {
        if (timerText == null)
        {
            timerText = GetComponentInChildren<TextMeshProUGUI>();
        }

        if (playerData == null)
        {
            playerData = FindObjectOfType<PlayerData>();
        }

        if (characterMovement == null)
        {
            characterMovement = FindObjectOfType<characterMovement>();
        }
    }

    private void Start()
    {
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

                if (playerData != null)
                {
                    playerData.SaveData();
                }

                if (characterMovement != null)
                {
                    characterMovement.Die();
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