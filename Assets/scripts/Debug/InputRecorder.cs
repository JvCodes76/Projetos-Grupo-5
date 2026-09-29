using System.IO;
using Roguelike.Movement;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Gravação e replay de input (SPEC §15, RNF-02): F5 começa/termina a gravação, F6 reproduz a última. Grava o
/// TickInput de cada tick a partir da posição atual, salva em Temp/Replays/*.json e, no replay, compara o hash do
/// estado no fim. Base futura de ghosts. Só no Editor e em Development Build.
/// </summary>
public sealed class InputRecorder : MonoBehaviour
{
    [SerializeField] private PlayerController controller;

    private InputRecording recording;
    private InputRecording replaying;
    private bool isRecording;

    private void Awake()
    {
        if (controller == null) controller = GetComponent<PlayerController>();
        if (!Debug.isDebugBuild) enabled = false; // só no Editor e em Development Build
    }

    private void OnEnable()
    {
        if (controller != null) controller.Ticked += HandleTicked;
    }

    private void OnDisable()
    {
        if (controller != null) controller.Ticked -= HandleTicked;
    }

    private void Update()
    {
        Keyboard kb = Keyboard.current;
        if (kb == null || controller == null) return;

        if (kb.f5Key.wasPressedThisFrame)
        {
            if (!isRecording) StartRecording();
            else StopRecording();
        }

        if (kb.f6Key.wasPressedThisFrame && !isRecording) StartReplay();
    }

    private void StartRecording()
    {
        recording = new InputRecording();
        controller.StartRecording(recording);
        isRecording = true;
        Debug.Log("[Movement] - Gravação de input iniciada (F5 para parar)");
    }

    private void StopRecording()
    {
        InputRecording done = controller.StopRecording();
        isRecording = false;
        if (done == null) return;

        string dir = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Temp", "Replays");
        Directory.CreateDirectory(dir);
        string path = Path.Combine(dir, $"replay_{System.DateTime.Now:yyyyMMdd_HHmmss}.json");
        File.WriteAllText(path, done.Serialize());
        Debug.Log($"[Movement] - Gravação salva: {done.Count} ticks, hash {done.FinalHash:X16} → {path}");
    }

    private void StartReplay()
    {
        if (recording == null || recording.Count == 0)
        {
            Debug.Log("[Movement] - Nada gravado ainda (F5)");
            return;
        }

        replaying = recording;
        replaying.Rewind();
        controller.Teleport(replaying.StartFeet);
        controller.SetInputOverride(replaying);
        Debug.Log($"[Movement] - Reproduzindo {replaying.Count} ticks");
    }

    private void HandleTicked(PlayerController pc)
    {
        if (replaying == null || !replaying.IsFinished) return;

        ulong hash = pc.Motor.ComputeHash();
        bool same = hash == replaying.FinalHash;
        Debug.Log($"[Movement] - Replay terminou: hash {hash:X16} {(same ? "igual" : "DIFERENTE de")} {replaying.FinalHash:X16}");
        pc.SetInputOverride(null);
        replaying = null;
    }
}
