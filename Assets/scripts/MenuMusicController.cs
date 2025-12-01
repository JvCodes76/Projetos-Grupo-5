using UnityEngine;
using UnityEngine.SceneManagement;

public class MenuMusicController : MonoBehaviour
{
    public static MenuMusicController Instance;

    void Start()
    {
        float savedVolume = PlayerPrefs.GetFloat("masterVolume", 1f);
        AudioListener.volume = savedVolume;
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        DontDestroyOnLoad(gameObject);
    }

    public void StopMusicAndDestroy()
    {
        Destroy(gameObject); 
    }
}