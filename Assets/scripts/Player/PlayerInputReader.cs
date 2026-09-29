using Roguelike.Movement;
using UnityEngine.InputSystem;

/// <summary>
/// Adaptador de input (SPEC §4.1): no início de cada frame monta um <see cref="FrameSample"/> com as bordas
/// (WasPressedThisFrame/WasReleasedThisFrame) e o estado (IsPressed) de cada botão e os eixos brutos. O
/// InputSampler do núcleo converte isso em TickInput por tick sem perder apertos. Não aloca.
/// </summary>
public static class PlayerInputReader
{
    public static FrameSample Read()
    {
        PlayerControls.PlayerActions player = GameInput.Player;
        var sample = new FrameSample
        {
            RawX = player.Movement.ReadValue<float>(),
            RawY = player.Vertical.ReadValue<float>(),
        };

        Accumulate(ref sample, player.Jump, ButtonBits.Jump);
        Accumulate(ref sample, player.Dash, ButtonBits.Dash);
        Accumulate(ref sample, player.Grapple, ButtonBits.Grapple);
        Accumulate(ref sample, player.Restart, ButtonBits.Restart);
        return sample;
    }

    /// <summary>Botões fisicamente segurados agora (Rearm: ignorar até soltar, RF-63).</summary>
    public static ButtonBits HeldButtons()
    {
        PlayerControls.PlayerActions player = GameInput.Player;
        ButtonBits held = ButtonBits.None;
        if (player.Jump.IsPressed()) held |= ButtonBits.Jump;
        if (player.Dash.IsPressed()) held |= ButtonBits.Dash;
        if (player.Grapple.IsPressed()) held |= ButtonBits.Grapple;
        if (player.Restart.IsPressed()) held |= ButtonBits.Restart;
        return held;
    }

    private static void Accumulate(ref FrameSample sample, InputAction action, ButtonBits bit)
    {
        if (action.WasPressedThisFrame()) sample.PressedThisFrame |= bit;
        if (action.WasReleasedThisFrame()) sample.ReleasedThisFrame |= bit;
        if (action.IsPressed()) sample.HeldNow |= bit;
    }
}
