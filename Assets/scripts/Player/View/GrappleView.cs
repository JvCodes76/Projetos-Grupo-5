using Roguelike.Movement;
using UnityEngine;

/// <summary>
/// Visual do gancho e da teia (SPEC §7.7, RF-32): a mira (linha do corpo até o ponto mirado + retícula + um pip por
/// uso restante na fase), a ponta (HookTip) viajando e presa, e a corda (LineRenderer) do FirePoint até a ponta.
/// A mira aparece quando o jogador tem o gancho ou a teia e ainda sobra uso na fase: cheia quando o disparo
/// prenderia numa parede, apagada quando não há parede no alcance (ou ela está perto demais).
/// Reaproveita os filhos HookTip/RopeVisual/FirePoint do prefab. Só lê o snapshot; o gancho é um estado do PlayerMotor.
/// </summary>
[DefaultExecutionOrder(44)]
public sealed class GrappleView : MonoBehaviour
{
    private const float PixelsPerUnit = 32f; // RuntimeSprites.WhitePixel
    private const int ReticleTicks = 4;
    private const int MaxPips = 8;

    [SerializeField] private PlayerController controller;
    [SerializeField] private Transform firePoint;
    [SerializeField] private Transform hookTip;
    [SerializeField] private LineRenderer rope;

    [Header("Mira")]
    [Tooltip("Cor da mira quando o disparo prenderia (parede ou alvo no alcance).")]
    [SerializeField] private Color reticleColor = new Color(0.3f, 1f, 1f, 0.95f);
    [Tooltip("Cor da mira sem alvo (sem parede no alcance ou perto demais).")]
    [SerializeField] private Color noTargetColor = new Color(1f, 1f, 1f, 0.3f);
    [Tooltip("Multiplicador de alfa da mira durante o cooldown do gancho.")]
    [SerializeField, Range(0f, 1f)] private float cooldownAlpha = 0.45f;
    [Tooltip("Distância dos traços da retícula ao ponto mirado (u).")]
    [SerializeField] private float reticleSize = 0.35f;
    [Tooltip("Comprimento e espessura de cada traço da retícula (u).")]
    [SerializeField] private Vector2 reticleTick = new Vector2(0.22f, 0.07f);
    [Tooltip("Espessura da linha de mira (u). 0 = sem linha.")]
    [SerializeField] private float aimLineWidth = 0.04f;
    [Tooltip("Alfa da linha de mira em relação à retícula.")]
    [SerializeField, Range(0f, 1f)] private float aimLineAlpha = 0.35f;
    [Tooltip("A linha de mira começa a esta distância do centro do corpo (u).")]
    [SerializeField] private float aimLineStart = 0.8f;
    [Tooltip("Lado de cada pip de uso restante (u).")]
    [SerializeField] private float pipSize = 0.12f;
    [Tooltip("Distância entre pips (u).")]
    [SerializeField] private float pipSpacing = 0.2f;
    [SerializeField] private int sortingOrder = 50;

    private readonly SpriteRenderer[] reticle = new SpriteRenderer[ReticleTicks];
    private readonly SpriteRenderer[] pips = new SpriteRenderer[MaxPips];
    private SpriteRenderer reticleDot;
    private SpriteRenderer aimLine;

    private void Awake()
    {
        if (controller == null) controller = GetComponent<PlayerController>();

        for (int i = 0; i < reticle.Length; i++) reticle[i] = CreateSprite("Reticle" + i);
        for (int i = 0; i < pips.Length; i++) pips[i] = CreateSprite("UsePip" + i);
        reticleDot = CreateSprite("ReticleDot");
        aimLine = CreateSprite("AimLine");

        if (hookTip != null) hookTip.gameObject.SetActive(false);
        if (rope != null) rope.enabled = false;
    }

    private SpriteRenderer CreateSprite(string objectName)
    {
        var go = new GameObject(objectName);
        go.transform.SetParent(transform, false);
        var r = go.AddComponent<SpriteRenderer>();
        r.sprite = RuntimeSprites.WhitePixel;
        r.sortingOrder = sortingOrder;
        r.enabled = false;
        return r;
    }

    private void LateUpdate()
    {
        if (controller == null || controller.Motor == null) return;
        PlayerSnapshot snap = controller.Snapshot;
        Vector2 renderOffset = controller.RenderPosition - snap.Position;

        DrawAim(snap, snap.BodyCenter + renderOffset);
        DrawHook(snap, renderOffset);
    }

    // Mira: linha do corpo ao ponto mirado, retícula (quatro traços + ponto) e os usos restantes logo abaixo.
    private void DrawAim(in PlayerSnapshot snap, Vector2 origin)
    {
        bool show = snap.GrappleAimVisible && snap.Visible;
        Vector2 target = snap.GrappleTarget;
        Color color = snap.HasGrappleTarget ? reticleColor : noTargetColor;
        if (snap.GrappleOnCooldown) color.a *= cooldownAlpha;

        for (int i = 0; i < reticle.Length; i++)
        {
            SpriteRenderer tick = reticle[i];
            tick.enabled = show;
            if (!show) continue;

            float angle = i * Mathf.PI * 0.5f;
            Vector2 offset = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * reticleSize;
            Place(tick, target + offset, i % 2 == 0 ? reticleTick : new Vector2(reticleTick.y, reticleTick.x), 0f, color);
        }

        // O ponto central só aparece com alvo: é ele que marca "o gancho prende aqui".
        reticleDot.enabled = show && snap.HasGrappleTarget;
        if (reticleDot.enabled) Place(reticleDot, target, new Vector2(reticleTick.y, reticleTick.y) * 1.5f, 0f, color);

        Vector2 toTarget = target - origin;
        float length = toTarget.magnitude - aimLineStart - reticleSize;
        aimLine.enabled = show && aimLineWidth > 0f && length > 0.05f;
        if (aimLine.enabled)
        {
            Vector2 dir = toTarget.normalized;
            Vector2 middle = origin + dir * (aimLineStart + length * 0.5f);
            Color lineColor = color;
            lineColor.a *= aimLineAlpha;
            Place(aimLine, middle, new Vector2(length, aimLineWidth), Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg, lineColor);
        }

        int uses = show ? Mathf.Min(snap.GrappleUsesLeft, pips.Length) : 0;
        float rowStart = -(uses - 1) * 0.5f * pipSpacing;
        for (int i = 0; i < pips.Length; i++)
        {
            pips[i].enabled = i < uses;
            if (i >= uses) continue;

            Vector2 position = target + new Vector2(rowStart + i * pipSpacing, -(reticleSize + reticleTick.x + pipSize));
            Place(pips[i], position, new Vector2(pipSize, pipSize), 0f, color);
        }
    }

    private static void Place(SpriteRenderer renderer, Vector2 position, Vector2 size, float angleDegrees, Color color)
    {
        Transform t = renderer.transform;
        t.SetPositionAndRotation(new Vector3(position.x, position.y, t.position.z), Quaternion.Euler(0f, 0f, angleDegrees));
        t.localScale = new Vector3(size.x * PixelsPerUnit, size.y * PixelsPerUnit, 1f);
        renderer.color = color;
    }

    // Ponta e corda.
    private void DrawHook(in PlayerSnapshot snap, Vector2 renderOffset)
    {
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
