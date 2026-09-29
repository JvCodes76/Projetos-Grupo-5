using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Dono único do input do jogador (SPEC §4.1, DS-03): uma instância da classe gerada <see cref="PlayerControls"/>
/// (Assets/PlayerControls.inputactions), com os overrides de rebinding carregados do PlayerPrefs e o mapa Player
/// habilitado. Nomes de action verificados em compilação; o rebinding vale numa instância só.
/// Estáticos zerados em SubsystemRegistration (Enter Play Mode sem domain reload).
/// </summary>
public static class GameInput
{
    public const string OverridesKey = "input.bindingOverrides";

    private static PlayerControls controls;

    public static PlayerControls Controls
    {
        get
        {
            EnsureCreated();
            return controls;
        }
    }

    public static PlayerControls.PlayerActions Player => Controls.Player;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        controls?.Dispose();
        controls = null;
    }

    public static void Enable()
    {
        EnsureCreated();
        if (!controls.Player.enabled) controls.Player.Enable();
    }

    public static void Disable()
    {
        if (controls != null && controls.Player.enabled) controls.Player.Disable();
    }

    /// <summary>Salva os overrides de binding atuais (rebinding, M.17).</summary>
    public static void SaveOverrides()
    {
        EnsureCreated();
        PlayerPrefs.SetString(OverridesKey, controls.asset.SaveBindingOverridesAsJson());
        PlayerPrefs.Save();
    }

    public static void ResetOverrides()
    {
        EnsureCreated();
        controls.asset.RemoveAllBindingOverrides();
        PlayerPrefs.DeleteKey(OverridesKey);
        PlayerPrefs.Save();
    }

    private static void EnsureCreated()
    {
        if (controls != null) return;

        controls = new PlayerControls();
        string json = PlayerPrefs.GetString(OverridesKey, string.Empty);
        if (!string.IsNullOrEmpty(json))
        {
            try
            {
                controls.asset.LoadBindingOverridesFromJson(json);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[GameInput] - Overrides de binding inválidos ignorados: {e.Message}");
            }
        }

        controls.Player.Enable();
    }
}
