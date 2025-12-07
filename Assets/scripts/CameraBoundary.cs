using UnityEngine;
public class CameraBoundary : MonoBehaviour
{
    [Header("Limites da Câmera")]
    public float minX = 0;
    public float maxX = 50;
    public float minY = 0;
    public float maxY = 15;

    [Header("Visualização")]
    public Color gizmoColor = Color.yellow;

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = gizmoColor;
        Vector3 center = new Vector3((minX + maxX) / 2, (minY + maxY) / 2, 0);
        Vector3 size = new Vector3(maxX - minX, maxY - minY, 0.1f);
        Gizmos.DrawWireCube(center, size);
    }
}