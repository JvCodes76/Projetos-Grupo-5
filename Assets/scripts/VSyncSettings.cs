using UnityEngine;
using UnityEngine.UI;

public class VSyncSettings : MonoBehaviour
{
    private Toggle vsyncToggle;

    private const string VSyncPrefKey = "vsyncEnabled";
    private const string FrameLimitPrefKey = "frameLimit";

    private void Awake()
    {
        // Pega o Toggle que está no mesmo GameObject
        vsyncToggle = GetComponent<Toggle>();
    }

    private void Start()
    {
        // 1) Ler se o VSync estava ligado ou desligado da última vez
        //    (por padrão, vamos considerar ligado = 1)
        bool vsyncOn = PlayerPrefs.GetInt(VSyncPrefKey, 1) == 1;

        // 2) Atualizar o estado visual da checkbox
        vsyncToggle.isOn = vsyncOn;

        // 3) Aplicar esse estado nas configurações reais (Unity)
        ApplyVSync(vsyncOn);

        // 4) Escutar quando o jogador clicar na checkbox
        vsyncToggle.onValueChanged.AddListener(HandleToggleChanged);
    }

    private void OnDestroy()
    {
        // Boa prática: parar de escutar quando o objeto for destruído
        if (vsyncToggle != null)
        {
            vsyncToggle.onValueChanged.RemoveListener(HandleToggleChanged);
        }
    }

    private void HandleToggleChanged(bool isOn)
    {
        // Toda vez que o jogador mexer na checkbox:
        // 1) aplica o VSync
        // 2) salva a escolha
        ApplyVSync(isOn);

        PlayerPrefs.SetInt(VSyncPrefKey, isOn ? 1 : 0);
        PlayerPrefs.Save();
    }

    private void ApplyVSync(bool enabled)
    {
        if (enabled)
        {
            // Ativa VSync: 1 = sincronizar com o refresh do monitor
            QualitySettings.vSyncCount = 1;

            // Descobre a taxa de atualização atual do monitor
            int refreshRate = Screen.currentResolution.refreshRate;

            // Define o limite de FPS (FrameLimit) com base nessa taxa
            Application.targetFrameRate = refreshRate;

            // Salva esse FrameLimit para quem quiser ler depois
            PlayerPrefs.SetInt(FrameLimitPrefKey, refreshRate);
        }
        else
        {
            // Desliga VSync
            QualitySettings.vSyncCount = 0;

            // Aqui você escolhe o comportamento quando o VSync está desligado:
            // -1 = sem limite (Unity decide)
            // ou você pode colocar um valor fixo, tipo 120, 144 etc.
            Application.targetFrameRate = -1;

            PlayerPrefs.SetInt(FrameLimitPrefKey, -1);
        }

        PlayerPrefs.Save();
    }
}
