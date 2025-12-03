using UnityEngine;
using System.IO; // Necessário para manipular arquivos

public static class SaveManager
{
    // Caminho do arquivo. Application.persistentDataPath garante que funcione em PC, Android, etc.
    private static string path = Application.persistentDataPath + "/savegame.json";

    // Variável temporária para passar dados do Menu para o Jogo
    public static SaveData loadedData; 

    public static void SaveGame(SaveData data)
    {
        // Transforma a classe em texto JSON
        string json = JsonUtility.ToJson(data);
        // Escreve no disco
        File.WriteAllText(path, json);
        Debug.Log("Jogo Salvo em: " + path);
    }

    public static SaveData LoadGame()
    {
        if (File.Exists(path))
        {
            string json = File.ReadAllText(path);
            SaveData data = JsonUtility.FromJson<SaveData>(json);
            return data;
        }
        else
        {
            Debug.LogWarning("Save file não encontrado!");
            return null;
        }
    }

    public static void DeleteSave()
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    public static bool HasSaveFile()
    {
        return File.Exists(path);
    }
}