using UnityEngine;
using UnityEngine.UI;

public class VolumeSettings : MonoBehaviour
{
    private Slider volumeSlider;
    private const string VolumePrefKey = "masterVolume";

    private void Awake()
    {
        // Pega o componente Slider que est� no mesmo GameObject
        volumeSlider = GetComponent<Slider>();
    }

    private void Start()
    {
        // Carrega o volume salvo (se n�o tiver nada salvo, usa 1.0 como padr�o)
        float savedVolume = PlayerPrefs.GetFloat(VolumePrefKey, 1f);

        // Aplica o valor no slider e no volume global do jogo
        volumeSlider.value = savedVolume;
        AudioListener.volume = savedVolume;

        // Escuta mudan�as no slider
        volumeSlider.onValueChanged.AddListener(HandleSliderValueChanged);
    }

    private void OnDestroy()
    {
        // Boa pr�tica: parar de escutar quando o objeto for destru�do
        if (volumeSlider != null)
        {
            volumeSlider.onValueChanged.RemoveListener(HandleSliderValueChanged);
        }
    }

    private void HandleSliderValueChanged(float value)
    {
        // Atualiza o volume global
        AudioListener.volume = value;

        // Salva o valor para uso futuro
        PlayerPrefs.SetFloat(VolumePrefKey, value);
        PlayerPrefs.Save();
    }
}