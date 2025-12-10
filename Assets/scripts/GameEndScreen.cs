using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;
using System;
using System.Collections.Generic; // Necessário para usar Listas

public class GameEndScreen : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private TextMeshProUGUI finalTimeText;
    [SerializeField] private TextMeshProUGUI finalCoinsText;
    [SerializeField] private string mainMenuScene = "MainMenu";

    private void Start()
    {
        // 1. CORREÇÃO: Remove câmeras e áudios antigos que vieram de outras cenas
        CleanUpOldCameras();

        // 2. Busca e exibe os dados
        ShowStats();

        // 3. Libera o mouse para clicar no botão
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void CleanUpOldCameras()
    {
        // Encontra todas as câmeras na cena
        Camera[] allCameras = FindObjectsOfType<Camera>();
        
        // Pega a câmera atual desta cena (EndGame)
        Camera currentSceneCamera = Camera.main;
        if (currentSceneCamera == null) currentSceneCamera = GetComponent<Camera>();

        foreach (Camera cam in allCameras)
        {
            // Se a câmera não for a câmera da cena EndGame e estiver na área "DontDestroyOnLoad"
            if (cam != currentSceneCamera && cam.gameObject.scene.name == "DontDestroyOnLoad")
            {
                Destroy(cam.gameObject);
            }
            // Se a câmera estiver duplicada
            else if (cam != currentSceneCamera && cam.gameObject.scene.name != "EndGame") 
            {
                 Destroy(cam.gameObject);
            }
        }
        
        // Remove ouvintes de áudio extras (para sumir com o erro do console)
        AudioListener[] listeners = FindObjectsOfType<AudioListener>();
        if (listeners.Length > 1)
        {
            for (int i = 1; i < listeners.Length; i++)
            {
                Destroy(listeners[i]);
            }
        }
    }

    private void ShowStats()
    {
        PlayerData data = FindObjectOfType<PlayerData>();

        if (data != null)
        {
            if(finalCoinsText != null)
                finalCoinsText.text = "Moedas: " + data.coinCount.ToString();

            if (finalTimeText != null)
            {
                TimeSpan t = TimeSpan.FromSeconds(data.totalTimePlayed);
                string formattedTime = (t.TotalHours >= 1)
                    ? string.Format("{0:00}:{1:00}:{2:00}", (int)t.TotalHours, t.Minutes, t.Seconds)
                    : string.Format("{0:00}:{1:00}", t.Minutes, t.Seconds);

                finalTimeText.text = "Tempo Total: " + formattedTime;
            }
        }
        else
        {
            Debug.LogWarning("PlayerData não encontrado. Testando pelo editor?");
        }
    }

    public void BackToMenu()
    {
        // Opcional: Destruir o PlayerData para resetar tudo ao voltar ao menu
        PlayerData data = FindObjectOfType<PlayerData>();
        if (data != null) Destroy(data.gameObject);

        // Destroi o SceneController antigo também para evitar duplicatas
        if (SceneController.instance != null) Destroy(SceneController.instance.gameObject);

        SceneManager.LoadScene(mainMenuScene);
    }
}