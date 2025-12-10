using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;
using System;
using System.Linq;

public class GameEndScreen : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private TextMeshProUGUI finalTimeText;
    [SerializeField] private TextMeshProUGUI finalCoinsText;
    [SerializeField] private string mainMenuScene = "MainMenu";

    private void Start()
    {
        // 1. CHAVE: Limpa objetos persistentes indesejados (Resolve o problema da câmera e do audio listener)
        CleanUpDontDestroyObjects();

        // 2. Busca e exibe os dados (com debug no console)
        ShowStats();

        // 3. Libera o mouse para clicar no botão
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void CleanUpDontDestroyObjects()
    {
        // Tenta obter a cena especial "DontDestroyOnLoad"
        Scene ddolScene = SceneManager.GetSceneByName("DontDestroyOnLoad");
        
        if (ddolScene.isLoaded)
        {
            GameObject[] rootObjects = ddolScene.GetRootGameObjects();
            
            foreach (GameObject rootObj in rootObjects)
            {
                // Destrói câmeras e AudioListeners que não fazem parte do PlayerData/SceneController
                if ((rootObj.GetComponent<Camera>() != null || rootObj.GetComponent<AudioListener>() != null) 
                    && rootObj.GetComponent<PlayerData>() == null && rootObj.GetComponent<SceneController>() == null)
                {
                     Debug.Log("GameEndScreen: Destruindo objeto persistente desnecessário: " + rootObj.name);
                     Destroy(rootObj);
                }
            }
        }
        
        // Limpeza de AudioListener extras na cena atual
        AudioListener[] listeners = FindObjectsOfType<AudioListener>();
        if (listeners.Length > 1)
        {
            for (int i = 1; i < listeners.Length; i++)
            {
                Destroy(listeners[i].gameObject);
            }
        }
    }

    private void ShowStats()
    {
        PlayerData data = FindObjectOfType<PlayerData>();

        if (data != null)
        {
            // *** DEBUG SOLICITADO NO CONSOLE (Para você verificar os valores) ***
            Debug.Log($"--- DADOS FINAIS (GameEndScreen) ---");
            Debug.Log($"Moedas Finais: {data.coinCount}");
            Debug.Log($"Tempo Total (segundos): {data.totalTimePlayed}");
            Debug.Log($"------------------------------------");
            
            // CHAVE: Exibe as moedas no formato solicitado: "MOEDAS: 25"
            if(finalCoinsText != null)
                finalCoinsText.text = "MOEDAS: " + data.coinCount.ToString();

            // Formata e exibe o tempo total no formato solicitado: "TEMPO TOTAL: 01:25"
            if (finalTimeText != null)
            {
                TimeSpan t = TimeSpan.FromSeconds(data.totalTimePlayed);
                string formattedTime = (t.TotalHours >= 1)
                    ? string.Format("{0:00}:{1:00}:{2:00}", (int)t.TotalHours, t.Minutes, t.Seconds)
                    : string.Format("{0:00}:{1:00}", t.Minutes, t.Seconds);

                finalTimeText.text = "TEMPO TOTAL: " + formattedTime;
            }
        }
        else
        {
            Debug.LogError("GameEndScreen: PlayerData não encontrado! A tela final foi carregada sem dados do jogo.");
            if(finalCoinsText != null) finalCoinsText.text = "MOEDAS: 0";
            if(finalTimeText != null) finalTimeText.text = "TEMPO TOTAL: 00:00";
        }
    }

    public void BackToMenu()
    {
        // Limpa todos os objetos persistentes
        PlayerData data = FindObjectOfType<PlayerData>();
        if (data != null) Destroy(data.gameObject);

        if (SceneController.instance != null) Destroy(SceneController.instance.gameObject);

        SceneManager.LoadScene(mainMenuScene);
    }
}