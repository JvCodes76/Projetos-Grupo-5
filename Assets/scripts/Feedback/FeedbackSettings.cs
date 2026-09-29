using System;
using UnityEngine;

/// <summary>
/// Opções de acessibilidade do feedback (SPEC §10.4, RF-65, RF-66), persistidas no PlayerPrefs.
/// Shake ∈ {0; 0,5; 1} (padrão 0,5) · Rumble ∈ {0; 0,5; 1} (padrão 1) · Freeze (padrão ligado; desligar não muda o
/// tempo da fase, §3.5) · Reduzir flashes (padrão desligado). UI: FeedbackOptionsPanel na SettingsMenu (M.17).
/// </summary>
public static class FeedbackSettings
{
    private const string ShakeKey = "feedback.shakeScale";
    private const string RumbleKey = "feedback.rumbleScale";
    private const string FreezeKey = "feedback.freeze";
    private const string FlashesKey = "feedback.reduceFlashes";

    public static readonly float[] ScaleSteps = { 0f, 0.5f, 1f };

    public static event Action Changed;

    public static float ShakeScale
    {
        get => PlayerPrefs.GetFloat(ShakeKey, 0.5f);
        set => SetFloat(ShakeKey, SnapToStep(value));
    }

    public static float RumbleScale
    {
        get => PlayerPrefs.GetFloat(RumbleKey, 1f);
        set => SetFloat(RumbleKey, SnapToStep(value));
    }

    public static bool FreezeEnabled
    {
        get => PlayerPrefs.GetInt(FreezeKey, 1) != 0;
        set => SetInt(FreezeKey, value ? 1 : 0);
    }

    public static bool ReduceFlashes
    {
        get => PlayerPrefs.GetInt(FlashesKey, 0) != 0;
        set => SetInt(FlashesKey, value ? 1 : 0);
    }

    public static void ResetToDefaults()
    {
        PlayerPrefs.DeleteKey(ShakeKey);
        PlayerPrefs.DeleteKey(RumbleKey);
        PlayerPrefs.DeleteKey(FreezeKey);
        PlayerPrefs.DeleteKey(FlashesKey);
        PlayerPrefs.Save();
        Changed?.Invoke();
    }

    private static float SnapToStep(float value)
    {
        float best = ScaleSteps[0];
        for (int i = 1; i < ScaleSteps.Length; i++)
        {
            if (Mathf.Abs(ScaleSteps[i] - value) < Mathf.Abs(best - value)) best = ScaleSteps[i];
        }

        return best;
    }

    private static void SetFloat(string key, float value)
    {
        PlayerPrefs.SetFloat(key, value);
        PlayerPrefs.Save();
        Changed?.Invoke();
    }

    private static void SetInt(string key, int value)
    {
        PlayerPrefs.SetInt(key, value);
        PlayerPrefs.Save();
        Changed?.Invoke();
    }
}
