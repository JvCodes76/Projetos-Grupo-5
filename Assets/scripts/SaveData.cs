using UnityEngine;

[System.Serializable]
public class SaveData
{
    public string sceneName; // Nome da fase onde o jogador parou
    public Vector3 playerPosition; // Posição do jogador
    public float playerHealth; // Vida do jogador (exemplo)

    // Construtor para criar um save novo facilmente
    public SaveData(string _scene, Vector3 _pos, float _health)
    {
        sceneName = _scene;
        playerPosition = _pos;
        playerHealth = _health;
    }
}