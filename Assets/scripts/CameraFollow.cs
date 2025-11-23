using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    [Header("Target")]
    [Tooltip("O Transform do GameObject que a câmera deve seguir")]
    [SerializeField] private Transform target;

    [Header("Offset & Suavização")]
    [Tooltip("Distância fixa entre a câmera e o target")]
    [SerializeField] private Vector3 offset = new Vector3(0f, 1.5f, -10f);
    [Tooltip("Tempo de suavização do movimento")]
    [SerializeField] private float smoothTime = 0.2f;

    [Header("Limites da Câmera")]
    [SerializeField] private float minX = 0;
    [SerializeField] private float maxX = 1000;
    [SerializeField] private float minY = 0;
    [SerializeField] private float maxY = 1000;

    private Vector3 _velocity = Vector3.zero;

    private void Start()
    {
        FindAndSetTarget();
    }

    private void LateUpdate()
    {
        if (target == null)
        {
            FindAndSetTarget();
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
            Debug.LogWarning("GameObject 'Cyborg' não encontrado na cena. A câmera não seguirá ninguém.");
        }
    }

    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
    }

    public void FindTargetAgain()
    {
        FindAndSetTarget();
    }
}