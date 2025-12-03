using UnityEngine;
using UnityEngine.SceneManagement;

public class PlayerSaveController : MonoBehaviour
{
    void Start()
    {
        // Verifica se viemos de um "Continuar"
        if (SaveManager.loadedData != null)
        {
            // Aplica os dados carregados
            transform.position = SaveManager.loadedData.playerPosition;
            
            // Exemplo de vida (se você tiver um script de vida)
            // GetComponent<Health>().currentHealth = SaveManager.loadedData.playerHealth;

            Debug.Log("Jogo carregado com sucesso!");
        }
        else
        {
            Debug.Log("Novo Jogo iniciado (nenhum dado carregado).");
        }
    }

    // Chame esta função quando quiser salvar (ex: num Checkpoint ou botão de Pause)
    public void SaveMyGame()
    {
        // Cria os dados baseados no estado atual
        SaveData newData = new SaveData(
            SceneManager.GetActiveScene().name, // Nome da cena atual
            transform.position,                 // Posição atual
            100f                                // Vida atual (exemplo)
        );

        SaveManager.SaveGame(newData);
    }

    // Apenas para teste: Aperte F5 para salvar enquanto joga
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.F5))
        {
            SaveMyGame();
        }
    }
}