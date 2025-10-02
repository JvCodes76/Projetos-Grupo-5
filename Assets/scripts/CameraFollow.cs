using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    [Header("Target")]
    [Tooltip("O Transform do GameObject que a c�mera deve seguir")]
    [SerializeField] private Transform target;

    [Header("Offset & Suaviza��o")]
    [Tooltip("Dist�ncia fixa entre a c�mera e o target")]
    [SerializeField] private Vector3 offset = new Vector3(0f, 1.5f, -10f);
    [Tooltip("Tempo de suaviza��o do movimento")]
    [SerializeField] private float smoothTime = 0.2f;

    private Vector3 _velocity = Vector3.zero;

    private void LateUpdate()
    {
        if (target == null)
            return;

        Vector3 desiredPosition = target.position + offset;
        transform.position = Vector3.SmoothDamp(
            transform.position,
            desiredPosition,
            ref _velocity,
            smoothTime
        );
    }
}