using UnityEngine;
using TMPro;
using System;
using UnityEngine.UI;
using UnityEngine.SceneManagement; // NECESSÁRIO ADICIONAR ISSO

public class Timer : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private TextMeshProUGUI timerText;
    [SerializeField] private GameObject GameOverScreen;

    [Header("Config")]
    [SerializeField] private bool autoStart = true;
    [SerializeField] private bool isCountingUp = true;
    [SerializeField] private float startTime = 60f;

    private float currentTime;
    private bool timerActive = false;

    private void Awake()
    {
        if (timerText == null) timerText = GetComponentInChildren<TextMeshProUGUI>();
    }

    private void Start()
    {
        // NOVO: Atualiza o PlayerData com a fase atual assim que a fase começa
        if (PlayerData.Instance != null)
        {
            PlayerData.Instance.currentLevel = SceneManager.GetActiveScene().buildIndex;
        }

        currentTime = isCountingUp ? 0f : Mathf.Max(0f, startTime);
        UpdateTimerDisplay();
        if (autoStart) StartTimer();
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
                // LÓGICA DE MORTE
                currentTime = 0f;
                timerActive = false;
                timerText.color = Color.red;
                
                // ⚠️ REMOVIDA A CHAMADA ao PlayerPositionSaver.PrepareSave()

                // Salva o jogo (apenas Nível e Moedas)
                if (PlayerData.Instance != null)
                {
                    // O nível atual já foi setado no Start.
                    PlayerData.Instance.SaveData();
                }

                GameOverScreen.SetActive(true);
                Debug.Log("Tempo esgotado! Jogo salvo (apenas nível).");
            }
        }

        UpdateTimerDisplay();
    }

    private void UpdateTimerDisplay()
    {
        TimeSpan t = TimeSpan.FromSeconds(Mathf.Max(0f, currentTime));
        string text = (t.TotalHours >= 1.0)
            ? string.Format("{0:00}:{1:00}:{2:00}", (int)t.TotalHours, t.Minutes, t.Seconds)
            : string.Format("{0:00}:{1:00}", t.Minutes, t.Seconds);

        if (timerText != null) timerText.text = text;
    }

    public void StartTimer()  => timerActive = true;
    public void StopTimer()   => timerActive = false;
    public void ResetTimer()
    {
        currentTime = isCountingUp ? 0f : Mathf.Max(0f, startTime);
        UpdateTimerDisplay();
        timerActive = false;
    }
}