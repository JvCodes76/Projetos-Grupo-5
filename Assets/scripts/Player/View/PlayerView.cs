using Roguelike.Events;
using Roguelike.Movement;
using UnityEngine;

/// <summary>
/// Visual do Cyborg (SPEC §10.1): squash & stretch no SquashPivot (o colisor nunca muda, RF-55), flip do sprite pelo
/// facing do motor (em wall slide ele olha para a parede, RF-54) e sprite oculto durante o respawn. Só lê o snapshot
/// e os eventos; a lógica do squash é o <see cref="SquashModel"/> (puro).
/// </summary>
[DefaultExecutionOrder(40)]
public sealed class PlayerView : MonoBehaviour
{
    [SerializeField] private PlayerController controller;
    [SerializeField] private FeedbackTuning tuning;
    [Tooltip("Pivô nos pés que recebe a escala do squash.")]
    [SerializeField] private Transform squashPivot;
    [SerializeField] private SpriteRenderer sprite;

    private SquashModel squash = SquashModel.Identity;
    private Vector3 spriteBaseLocal;

    public SpriteRenderer Sprite => sprite;

    private void Awake()
    {
        if (controller == null) controller = GetComponent<PlayerController>();
        if (tuning == null) tuning = ScriptableObject.CreateInstance<FeedbackTuning>();
        if (sprite != null) spriteBaseLocal = sprite.transform.localPosition;
    }

    private void OnEnable()
    {
        EventBus<PlayerJumped>.Subscribe(HandleJumped);
        EventBus<PlayerWallJumped>.Subscribe(HandleWallJumped);
        EventBus<PlayerLanded>.Subscribe(HandleLanded);
        EventBus<PlayerRespawned>.Subscribe(HandleRespawned);
    }

    private void OnDisable()
    {
        EventBus<PlayerJumped>.Unsubscribe(HandleJumped);
        EventBus<PlayerWallJumped>.Unsubscribe(HandleWallJumped);
        EventBus<PlayerLanded>.Unsubscribe(HandleLanded);
        EventBus<PlayerRespawned>.Unsubscribe(HandleRespawned);
    }

    private void LateUpdate()
    {
        if (controller == null || controller.Motor == null) return;
        PlayerSnapshot snap = controller.Snapshot;

        squash.Update(Time.deltaTime, tuning);
        Vector2 scale = snap.State == MotorStateId.Dash ? tuning.DashSquash : squash.Scale;
        if (squashPivot != null) squashPivot.localScale = new Vector3(scale.x, scale.y, 1f);

        if (sprite != null)
        {
            sprite.enabled = snap.Visible;
            bool left = snap.Facing < 0;
            sprite.flipX = left;
            float offsetX = Mathf.Abs(spriteBaseLocal.x) * (left ? -1f : 1f);
            sprite.transform.localPosition = new Vector3(offsetX, spriteBaseLocal.y, spriteBaseLocal.z);
        }
    }

    private void HandleJumped(PlayerJumped evt) => squash.OnJump(tuning);

    private void HandleWallJumped(PlayerWallJumped evt) => squash.OnJump(tuning);

    private void HandleLanded(PlayerLanded evt) => squash.OnLanded(evt.ImpactSpeed, tuning);

    private void HandleRespawned(PlayerRespawned evt) => squash.OnRespawn(tuning);
}
