using Roguelike.Movement;
using UnityEngine;

/// <summary>
/// Visual do gancho (SPEC §7.7, RF-32): retícula no alvo previsto (snapshot), ponta (HookTip) viajando e presa, e a
/// corda (LineRenderer) do FirePoint até a ponta. Reaproveita os filhos HookTip/RopeVisual/FirePoint do prefab.
/// Só lê o snapshot; o gancho em si é um estado do PlayerMotor.
/// </summary>
[DefaultExecutionOrder(44)]
public sealed class GrappleView : MonoBehaviour
{
    [SerializeField] private PlayerController controller;
    [SerializeField] private Transform firePoint;
    [SerializeField] private Transform hookTip;
    [SerializeField] private LineRenderer rope;
    [Tooltip("Cor da retícula do alvo do gancho.")]
    [SerializeField] private Color reticleColor = new Color(0.3f, 1f, 1f, 0.9f);
    [SerializeField] private float reticleSize = 0.35f;

    private SpriteRenderer[] reticle;

    private void Awake()
    {
        if (controller == null) controller = GetComponent<PlayerController>();
        reticle = new SpriteRenderer[4];
        for (int i = 0; i < reticle.Length; i++)
        {
            var go = new GameObject("Reticle" + i);
            go.transform.SetParent(transform, false);
            var r = go.AddComponent<SpriteRenderer>();
            r.sprite = RuntimeSprites.WhitePixel;
            r.sortingOrder = 50;
            r.color = reticleColor;
            r.enabled = false;
            go.transform.localScale = i % 2 == 0 ? new Vector3(4f, 1f, 1f) : new Vector3(1f, 4f, 1f);
            reticle[i] = r;
        }

        if (hookTip != null) hookTip.gameObject.SetActive(false);
        if (rope != null) rope.enabled = false;
    }

    private void LateUpdate()
    {
        if (controller == null || controller.Motor == null) return;
        PlayerSnapshot snap = controller.Snapshot;
        Vector2 renderOffset = controller.RenderPosition - snap.Position;

        // Retícula: quatro traços em volta do alvo previsto.
        bool showReticle = snap.GrappleUnlocked && snap.HasGrappleTarget && snap.Visible;
        for (int i = 0; i < reticle.Length; i++)
        {
            reticle[i].enabled = showReticle;
            if (!showReticle) continue;
            float angle = i * Mathf.PI * 0.5f;
            Vector2 offset = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * reticleSize;
            reticle[i].transform.position = (Vector3)(snap.GrappleTarget + offset);
        }

        // Ponta e corda.
        bool active = snap.GrapplePhase != GrapplePhase.None && snap.Visible;
        if (hookTip != null)
        {
            if (hookTip.gameObject.activeSelf != active) hookTip.gameObject.SetActive(active);
            if (active) hookTip.position = new Vector3(snap.HookTip.x, snap.HookTip.y, hookTip.position.z);
        }

        if (rope != null)
        {
            rope.enabled = active;
            if (active)
            {
                Vector3 start = firePoint != null ? firePoint.position : (Vector3)(snap.BodyCenter + renderOffset);
                rope.positionCount = 2;
                rope.SetPosition(0, start);
                rope.SetPosition(1, new Vector3(snap.HookTip.x, snap.HookTip.y, start.z));
            }
        }
    }
}
