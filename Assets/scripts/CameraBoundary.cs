using Roguelike.Cameras;
using UnityEngine;

/// <summary>
/// Limites do CENTRO da câmera na fase + plano de queda (SPEC §11, RF-50). Registra-se em <see cref="Current"/> no
/// OnEnable (fim do FindFirstObjectByType e dos limites da fase anterior "vazando"). O PlayerController usa o
/// <see cref="KillPlaneY"/> como saída por baixo da fase (respawn no último chão seguro, Q3).
/// </summary>
public class CameraBoundary : MonoBehaviour
{
    [Header("Limites da Câmera (centro)")]
    public float minX = 0;
    public float maxX = 50;
    public float minY = 0;
    public float maxY = 15;

    [Header("Zona de queda")]
    [Tooltip("Usa killPlaneY como saída por baixo da fase. Sem isto: minY − meia altura da câmera − 1.")]
    public bool useKillPlane = false;
    [Tooltip("Abaixo desta altura (pés) o jogador volta ao último chão seguro; a câmera nunca mostra abaixo dela.")]
    public float killPlaneY = -10f;

    [Header("Visualização")]
    public Color gizmoColor = Color.yellow;

    /// <summary>Meia altura visível padrão (ortho 5,625 = 20 × 11,25 u em 16:9, M32).</summary>
    public const float DefaultHalfHeight = 5.625f;

    /// <summary>Boundary da fase atual (a última habilitada).</summary>
    public static CameraBoundary Current { get; private set; }

    /// <summary>Plano de queda efetivo.</summary>
    public float KillPlaneY => useKillPlane ? killPlaneY : minY - DefaultHalfHeight - 1f;

    public CameraBounds ToBounds() => new CameraBounds(minX, maxX, minY, maxY, KillPlaneY);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Current = null;
    }

    private void OnEnable()
    {
        Current = this;
    }

    private void OnDisable()
    {
        if (Current == this) Current = null;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = gizmoColor;
        Vector3 center = new Vector3((minX + maxX) / 2, (minY + maxY) / 2, 0);
        Vector3 size = new Vector3(maxX - minX, maxY - minY, 0.1f);
        Gizmos.DrawWireCube(center, size);

        Gizmos.color = Color.red;
        float y = KillPlaneY;
        Gizmos.DrawLine(new Vector3(minX - 20f, y, 0f), new Vector3(maxX + 20f, y, 0f));
    }
}
