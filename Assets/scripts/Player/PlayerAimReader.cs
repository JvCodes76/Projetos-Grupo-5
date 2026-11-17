using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Adaptador da mira do gancho/teia: devolve, por frame, a direção para onde o jogador aponta.
/// - Mouse: do corpo do jogador até o cursor (no mundo). Vale desde que o mouse se mexa ou clique, até o gamepad
///   ser usado.
/// - Gamepad: o analógico direito enquanto estiver inclinado.
/// - Nenhum dos dois: vetor nulo. O PlayerMotor então mira com a direção do movimento (8 direções) ou, parado, na
///   diagonal para a frente.
/// A direção vai para o <see cref="Roguelike.Movement.FrameSample"/> e é quantizada no TickInput (determinismo).
/// </summary>
public static class PlayerAimReader
{
    private const float MouseMoveThresholdSqr = 0.25f; // px² por frame
    private const float MinAimDistanceSqr = 0.01f;

    private static bool mouseAiming;
    private static Camera cachedCamera;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        mouseAiming = false;
        cachedCamera = null;
    }

    /// <param name="worldOrigin">Centro do corpo do jogador como aparece na tela.</param>
    /// <param name="stickDeadzone">Zona morta do analógico (perfil: StickDeadzone).</param>
    public static Vector2 Read(Vector2 worldOrigin, float stickDeadzone)
    {
        Gamepad gamepad = Gamepad.current;
        if (gamepad != null)
        {
            Vector2 stick = gamepad.rightStick.ReadValue();
            if (stick.sqrMagnitude >= stickDeadzone * stickDeadzone)
            {
                mouseAiming = false;
                return stick;
            }

            if (UsedThisFrame(gamepad, stickDeadzone)) mouseAiming = false;
        }

        Mouse mouse = Mouse.current;
        if (mouse == null) return Vector2.zero;

        if (mouse.delta.ReadValue().sqrMagnitude > MouseMoveThresholdSqr || mouse.leftButton.wasPressedThisFrame)
        {
            mouseAiming = true;
        }

        if (!mouseAiming) return Vector2.zero;

        Camera cam = AimCamera();
        if (cam == null) return Vector2.zero;

        Vector2 screen = mouse.position.ReadValue();
        float depth = Mathf.Abs(cam.transform.position.z);
        Vector2 world = cam.ScreenToWorldPoint(new Vector3(screen.x, screen.y, depth));
        Vector2 aim = world - worldOrigin;
        return aim.sqrMagnitude >= MinAimDistanceSqr ? aim : Vector2.zero;
    }

    private static bool UsedThisFrame(Gamepad gamepad, float stickDeadzone)
    {
        return gamepad.leftStick.ReadValue().sqrMagnitude >= stickDeadzone * stickDeadzone
            || gamepad.dpad.ReadValue().sqrMagnitude > 0f
            || gamepad.buttonSouth.wasPressedThisFrame || gamepad.buttonEast.wasPressedThisFrame
            || gamepad.buttonWest.wasPressedThisFrame || gamepad.buttonNorth.wasPressedThisFrame
            || gamepad.rightTrigger.wasPressedThisFrame || gamepad.rightShoulder.wasPressedThisFrame
            || gamepad.leftTrigger.wasPressedThisFrame || gamepad.leftShoulder.wasPressedThisFrame;
    }

    private static Camera AimCamera()
    {
        if (cachedCamera == null || !cachedCamera.isActiveAndEnabled) cachedCamera = Camera.main;
        return cachedCamera;
    }
}
