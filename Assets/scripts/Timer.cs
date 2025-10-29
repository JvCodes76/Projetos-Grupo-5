using UnityEngine;
using TMPro;
using System;
using UnityEngine.UI;

public class Timer : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private TextMeshProUGUI timerText;   // arraste o Text (TMP) aqui
    [SerializeField] private GameObject GameOverScreen;

    [Header("Config")]
    [SerializeField] private bool autoStart = true;       // começa sozinho ao dar Play
    [SerializeField] private bool isCountingUp = true;    // false = contagem regressiva
    [SerializeField] private float startTime = 60f;       // em segundos (ex.: 60 = 1:00)

    private float currentTime;
    private bool timerActive = false;

    private void Awake()
    {
        // fallback opcional: tenta achar um TMP no mesmo objeto/filho
        if (timerText == null) timerText = GetComponentInChildren<TextMeshProUGUI>();
    }

    private void Start()
    {
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
                currentTime = 0f;
                timerActive = false;
                timerText.color = Color.red;
                GameOverScreen.SetActive(true);
                Debug.Log("Timer finished!");
            }
        }

        UpdateTimerDisplay();
    }

    private void UpdateTimerDisplay()
    {
        // mostra HH:MM:SS automaticamente quando passar de 1h
        TimeSpan t = TimeSpan.FromSeconds(Mathf.Max(0f, currentTime));
        string text = (t.TotalHours >= 1.0)
            ? string.Format("{0:00}:{1:00}:{2:00}", (int)t.TotalHours, t.Minutes, t.Seconds)
            : string.Format("{0:00}:{1:00}", t.Minutes, t.Seconds);

        if (timerText != null) timerText.text = text;
    }

    // Métodos públicos para botões/eventos
    public void StartTimer()  => timerActive = true;
    public void StopTimer()   => timerActive = false;
    public void ResetTimer()
    {
        currentTime = isCountingUp ? 0f : Mathf.Max(0f, startTime);
        UpdateTimerDisplay();
        timerActive = false;
    }
}
