using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Transform target;

    [Header("Offset & Suavização")]
    [SerializeField] private Vector3 offset = new Vector3(0f, 1.5f, -10f);
    [SerializeField] private float smoothTime = 0.2f;

    [Header("Limites da Câmera")]
    [SerializeField] private float minX = 0;
    [SerializeField] private float maxX = 1000;
    [SerializeField] private float minY = 0;
    [SerializeField] private float maxY = 1000;

    [Header("Configuração Inicial")]
    [SerializeField] private bool snapToTargetOnStart = true;
    [SerializeField] private float maxTeleportDistance = 20f;

    private Vector3 _velocity = Vector3.zero;

    private void Start()
    {
        FindAndSetTarget();

        if (snapToTargetOnStart && target != null)
        {
            SnapToTarget();
        }
    }

    private void LateUpdate()
    {
        if (target == null)
        {
            FindAndSetTarget();
            if (target == null) return;
        }

        // Teleporta se estiver muito longe
        float distance = Vector3.Distance(transform.position, target.position + offset);
        if (distance > maxTeleportDistance)
        {
            SnapToTarget();
            return;
        }

        Vector3 desiredPosition = target.position + offset;
        desiredPosition.x = Mathf.Clamp(desiredPosition.x, minX, maxX);
        desiredPosition.y = Mathf.Clamp(desiredPosition.y, minY, maxY);

        transform.position = Vector3.SmoothDamp(
            transform.position,
            desiredPosition,
            ref _velocity,
            smoothTime
        );
    }

    private void FindAndSetTarget()
    {
        GameObject cyborg = GameObject.Find("Cyborg");
        if (cyborg != null)
        {
            target = cyborg.transform;
            Debug.Log("Target encontrado: " + target.name);
        }
        else
        {
            Debug.LogWarning("GameObject 'Cyborg' não encontrado na cena.");
        }
    }

    private void SnapToTarget()
    {
        Vector3 desiredPosition = target.position + offset;
        desiredPosition.x = Mathf.Clamp(desiredPosition.x, minX, maxX);
        desiredPosition.y = Mathf.Clamp(desiredPosition.y, minY, maxY);
        transform.position = desiredPosition;
        _velocity = Vector3.zero;
    }

    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
        SnapToTarget();
    }

    public void FindTargetAgain()
    {
        FindAndSetTarget();
        if (target != null) SnapToTarget();
    }
}