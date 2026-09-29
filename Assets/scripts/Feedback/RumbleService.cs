using Roguelike.Events;
using Roguelike.Movement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>
/// Vibração do gamepad (SPEC §10.3, RF-59). DDOL, criado sob demanda. As regras (preset por cue, duração em tempo não
/// escalado, parada) são do <see cref="RumblePolicy"/> (puro); aqui só se aplica no hardware. Parada garantida
/// (InputSystem.ResetHaptics) em: pausa, PlayerDied, troca de cena, perda de foco e OnDisable/OnDestroy.
/// </summary>
public sealed class RumbleService : MonoBehaviour
{
    private static RumbleService instance;

    private readonly RumblePolicy policy = new RumblePolicy();
    private FeedbackTuning tuning;
    private bool hardwareActive;

    public static RumbleService Instance
    {
        get
        {
            if (instance == null && Application.isPlaying)
            {
                var go = new GameObject(nameof(RumbleService));
                instance = go.AddComponent<RumbleService>();
            }

            return instance;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        instance = null;
    }

    /// <summary>Vibra conforme o cue (pulo não vibra). <paramref name="tuning"/> define os presets.</summary>
    public static void Play(FeedbackCue cue, FeedbackTuning presets)
    {
        RumbleLevel level = RumblePolicy.LevelFor(cue);
        if (level == RumbleLevel.None || presets == null) return;
        RumbleService service = Instance;
        if (service == null) return;
        service.tuning = presets;
        service.policy.Play(RumblePolicy.PresetFor(level, presets), FeedbackSettings.RumbleScale);
        service.Apply();
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnEnable()
    {
        EventBus<PlayerDied>.Subscribe(HandlePlayerDied);
        SceneManager.activeSceneChanged += HandleSceneChanged;
    }

    private void OnDisable()
    {
        EventBus<PlayerDied>.Unsubscribe(HandlePlayerDied);
        SceneManager.activeSceneChanged -= HandleSceneChanged;
        policy.OnDisabled();
        StopHardware();
    }

    private void OnDestroy()
    {
        policy.OnDisabled();
        StopHardware();
        if (instance == this) instance = null;
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (hasFocus) return;
        policy.OnFocusLost();
        StopHardware();
    }

    private void Update()
    {
        if (Time.timeScale <= 0f || SimulationRunner.Paused)
        {
            if (policy.IsActive || hardwareActive)
            {
                policy.OnPaused();
                StopHardware();
            }

            return;
        }

        if (!policy.IsActive)
        {
            if (hardwareActive) StopHardware();
            return;
        }

        if (!policy.Tick(Time.unscaledDeltaTime)) StopHardware();
    }

    private void HandlePlayerDied(PlayerDied evt)
    {
        policy.OnPlayerDied();
        StopHardware();
    }

    private void HandleSceneChanged(Scene from, Scene to)
    {
        policy.OnSceneChanged();
        StopHardware();
    }

    private void Apply()
    {
        Gamepad pad = Gamepad.current;
        if (pad == null || !policy.IsActive) return;
        pad.SetMotorSpeeds(policy.LowFrequency, policy.HighFrequency);
        hardwareActive = true;
    }

    private void StopHardware()
    {
        if (!hardwareActive) return;
        InputSystem.ResetHaptics();
        hardwareActive = false;
    }
}
