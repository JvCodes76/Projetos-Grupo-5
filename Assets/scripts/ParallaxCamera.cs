using UnityEngine;

/// <summary>
/// Avisa as camadas de parallax quando a câmera anda em X. Roda no LateUpdate DEPOIS da câmera
/// (CameraFollow = 50, este = 60): antes ele lia a posição no Update e o parallax andava um frame atrasado (RF-46).
/// </summary>
[ExecuteInEditMode]
[DefaultExecutionOrder(60)]
public class ParallaxCamera : MonoBehaviour
{
    public delegate void ParallaxCameraDelegate(float deltaMovement);
    public ParallaxCameraDelegate onCameraTranslate;
    private float oldPosition;

    private void Start()
    {
        oldPosition = transform.position.x;
    }

    private void LateUpdate()
    {
        if (transform.position.x != oldPosition)
        {
            if (onCameraTranslate != null)
            {
                float delta = oldPosition - transform.position.x;
                onCameraTranslate(delta);
            }

            oldPosition = transform.position.x;
        }
    }
}
