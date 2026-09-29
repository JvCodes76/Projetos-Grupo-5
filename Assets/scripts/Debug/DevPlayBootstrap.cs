using Roguelike.Cameras;
using Roguelike.Events;
using Roguelike.Movement;
using UnityEngine;

/// <summary>
/// "Play direto" (SPEC §15, AUD bug 13): numa cena de fase ou no gym, sem passar pelo MainMenu, garante o
/// SimulationRunner, uma câmera com CameraFollow e o jogador no SpawnPoint, e dispara PlayerSpawned local.
/// Não faz nada quando a run está ativa (o RunManager já instancia o jogador).
/// </summary>
[DefaultExecutionOrder(-30)]
public sealed class DevPlayBootstrap : MonoBehaviour
{
    [SerializeField] private GameObject playerPrefab;
    [SerializeField] private string spawnPointTag = "SpawnPoint";
    [Tooltip("Kit de movimento do jogador de dev (vazio = kit do prefab).")]
    [SerializeField] private MovementKit kit;
    [SerializeField] private CameraProfile cameraProfile;
    [SerializeField] private FeedbackTuning feedbackTuning;

    private void Start()
    {
        if (RunManager.Instance != null) return;

        _ = SimulationRunner.Instance;
        EnsureCamera();

        PlayerController player = FindFirstObjectByType<PlayerController>();
        if (player == null && playerPrefab != null)
        {
            Vector3 position = Vector3.zero;
            GameObject spawn = GameObject.FindWithTag(spawnPointTag);
            if (spawn != null) position = spawn.transform.position;
            else Debug.LogWarning($"[DevPlayBootstrap] - Nenhum '{spawnPointTag}' na cena; jogador na origem");
            player = Instantiate(playerPrefab, position, Quaternion.identity).GetComponent<PlayerController>();
        }

        if (player == null)
        {
            Debug.LogWarning("[DevPlayBootstrap] - Sem prefab do jogador e sem PlayerController na cena");
            return;
        }

        if (kit != null) player.SetStatSource(kit.CreateInput(player.Profile));
        EventBus<PlayerSpawned>.Raise(new PlayerSpawned(player.gameObject));
        Debug.Log("[DevPlayBootstrap] - Play direto: jogador pronto (F1 overlay, F5/F6 gravação)");
    }

    private void EnsureCamera()
    {
        if (FindFirstObjectByType<CameraFollow>() != null) return;

        Camera cam = Camera.main;
        GameObject go = cam != null ? cam.gameObject : new GameObject("Main Camera", typeof(Camera));
        go.tag = "MainCamera";
        cam = go.GetComponent<Camera>();
        cam.orthographic = true;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.08f, 0.07f, 0.12f);
        if (go.GetComponent<AudioListener>() == null) go.AddComponent<AudioListener>();

        // O Awake do CameraFollow roda no AddComponent: os campos precisam existir antes (via profile padrão).
        var follow = go.AddComponent<CameraFollow>();
        if (cameraProfile != null || feedbackTuning != null) follow.Configure(cameraProfile, feedbackTuning);
    }
}
