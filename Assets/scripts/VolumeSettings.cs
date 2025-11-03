using UnityEngine;
using UnityEngine.UI;

public class VolumeSettings : MonoBehaviour
{
    private Slider volumeSlider;
    private const string VolumePrefKey = "masterVolume";

    private void Awake()
    {
        // Pega o componente Slider que está no mesmo GameObject
        volumeSlider = GetComponent<Slider>();
    }

    private void Start()
    {
        // Carrega o volume salvo (se não tiver nada salvo, usa 1.0 como padrão)
        float savedVolume = PlayerPrefs.GetFloat(VolumePrefKey, 1f);

        // Aplica o valor no slider e no volume global do jogo
        volumeSlider.value = savedVolume;
        AudioListener.volume = savedVolume;

        // Escuta mudanças no slider
        volumeSlider.onValueChanged.AddListener(HandleSliderValueChanged);
    }

    private void OnDestroy()
    {
        // Boa prática: parar de escutar quando o objeto for destruído
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