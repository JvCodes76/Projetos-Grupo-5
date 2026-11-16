using Roguelike.Events;
using UnityEngine;
using UnityEngine.SceneManagement;

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

    // A câmera é DDOL: ao trocar de cena ela "salta" da fase antiga para o spawn da nova (snap do CameraFollow no
    // LateUpdate seguinte ao PlayerSpawned). Esse salto não é movimento de câmera e não pode virar delta de parallax.
    private bool resyncPending;

    private void Start()
    {
        oldPosition = transform.position.x;
    }

    private void OnEnable()
    {
        if (!Application.isPlaying) return;

        SceneManager.sceneLoaded += HandleSceneLoaded;
        EventBus<PlayerSpawned>.Subscribe(HandlePlayerSpawned);
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        EventBus<PlayerSpawned>.Unsubscribe(HandlePlayerSpawned);
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode) => resyncPending = true;

    private void HandlePlayerSpawned(PlayerSpawned evt) => resyncPending = true;

    private void LateUpdate()
    {
        if (resyncPending)
        {
            resyncPending = false;
            oldPosition = transform.position.x;
            return;
        }

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
