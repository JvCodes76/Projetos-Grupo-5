using UnityEngine;

public class ApplySavedVolume : MonoBehaviour
{
    private const string VolumePrefKey = "masterVolume";

    private void Awake()
    {
        // Lê o volume salvo (1 como padrão)
        float savedVolume = PlayerPrefs.GetFloat(VolumePrefKey, 1f);

        // Aplica esse valor ao volume global
        AudioListener.volume = savedVolume;
    }
}
