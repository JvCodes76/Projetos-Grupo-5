using System;
using UnityEngine.InputSystem;

/// <summary>
/// Rebinding interativo (SPEC §4.4, RF-64): captura o próximo controle do dispositivo do binding (grupo Keyboard ou
/// Gamepad) para um binding do PlayerControls, com o mapa Player desabilitado durante a captura. Esc cancela. Os
/// overrides vão para o PlayerPrefs (<see cref="GameInput.SaveOverrides"/>) e voltam no próximo boot (o GameInput os
/// carrega no primeiro uso).
/// </summary>
public static class RebindService
{
    public const string KeyboardGroup = "Keyboard";
    public const string GamepadGroup = "Gamepad";

    private static InputActionRebindingExtensions.RebindingOperation operation;
    private static Action<bool> pendingDone;
    private static bool restoreMap;

    /// <summary>Há uma captura em andamento.</summary>
    public static bool IsRebinding => operation != null;

    /// <summary>
    /// Começa a captura para <paramref name="action"/>.bindings[<paramref name="bindingIndex"/>]. <paramref name="done"/>
    /// recebe true se o binding mudou (e já foi salvo) ou false se a captura foi cancelada.
    /// </summary>
    public static bool StartRebind(InputAction action, int bindingIndex, Action<bool> done)
    {
        if (action == null || bindingIndex < 0 || bindingIndex >= action.bindings.Count || action.bindings[bindingIndex].isComposite)
        {
            done?.Invoke(false);
            return false;
        }

        Cancel();
        restoreMap = action.actionMap != null && action.actionMap.enabled;
        GameInput.Disable();

        string group = FirstGroup(action.bindings[bindingIndex].groups);
        InputActionRebindingExtensions.RebindingOperation op = action.PerformInteractiveRebinding(bindingIndex)
            .WithControlsExcluding("<Mouse>")
            .WithCancelingThrough("<Keyboard>/escape")
            .OnMatchWaitForAnother(0.1f);
        if (group == KeyboardGroup) op.WithControlsHavingToMatchPath("<Keyboard>");
        else if (group == GamepadGroup) op.WithControlsHavingToMatchPath("<Gamepad>");
        if (!string.IsNullOrEmpty(group)) op.WithBindingGroup(group);

        pendingDone = done;
        op.OnComplete(_ => Finish(true)).OnCancel(_ => Finish(false));
        operation = op;
        op.Start();
        return true;
    }

    /// <summary>Cancela a captura em andamento (sem mudar o binding).</summary>
    public static void Cancel()
    {
        operation?.Cancel();
    }

    /// <summary>Grava os overrides atuais no PlayerPrefs.</summary>
    public static void Save()
    {
        GameInput.SaveOverrides();
    }

    /// <summary>Volta todos os bindings ao padrão do asset e apaga os overrides salvos.</summary>
    public static void ResetAll()
    {
        Cancel();
        GameInput.ResetOverrides();
    }

    /// <summary>
    /// Índice do binding de <paramref name="action"/> no grupo <paramref name="group"/>: a parte de composite
    /// <paramref name="compositePart"/> (ex.: "negative") ou, se null, um binding simples; <paramref name="occurrence"/> 0 é
    /// o primário e 1 o alternativo. −1 se não houver.
    /// </summary>
    public static int FindBindingIndex(InputAction action, string group, string compositePart = null, int occurrence = 0)
    {
        if (action == null) return -1;
        int seen = 0;
        for (int i = 0; i < action.bindings.Count; i++)
        {
            InputBinding b = action.bindings[i];
            if (b.isComposite) continue;
            if (compositePart != null ? !(b.isPartOfComposite && b.name == compositePart) : b.isPartOfComposite) continue;
            if (!InGroup(b.groups, group)) continue;
            if (seen++ == occurrence) return i;
        }

        return -1;
    }

    /// <summary>Texto do binding para a UI (ex.: "Space", "A").</summary>
    public static string DisplayString(InputAction action, int bindingIndex)
    {
        if (action == null || bindingIndex < 0 || bindingIndex >= action.bindings.Count) return "—";
        string text = action.GetBindingDisplayString(bindingIndex);
        return string.IsNullOrEmpty(text) ? "—" : text;
    }

    private static void Finish(bool changed)
    {
        InputActionRebindingExtensions.RebindingOperation op = operation;
        Action<bool> done = pendingDone;
        operation = null;
        pendingDone = null;
        op?.Dispose();
        if (restoreMap) GameInput.Enable();
        if (changed) Save();
        done?.Invoke(changed);
    }

    private static bool InGroup(string groups, string group)
    {
        if (string.IsNullOrEmpty(group)) return true;
        if (string.IsNullOrEmpty(groups)) return false;
        foreach (string g in groups.Split(InputBinding.Separator))
        {
            if (g == group) return true;
        }

        return false;
    }

    private static string FirstGroup(string groups)
    {
        if (string.IsNullOrEmpty(groups)) return null;
        int sep = groups.IndexOf(InputBinding.Separator);
        return sep < 0 ? groups : groups.Substring(0, sep);
    }
}
