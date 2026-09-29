using Roguelike.Events;
using Roguelike.Movement;
using UnityEngine;

/// <summary>
/// Indicador de recurso no corpo (SPEC §10.3, RF-58, RF-66): um "visor" (pixel gerado em runtime) mostra as cargas de
/// dash por cor E forma (0 = cinza e encolhido; 1 = ciano; 2 = magenta pulsando) e "pips" acima da cabeça mostram os
/// pulos aéreos disponíveis (contagem, não só cor). Flash na recarga (com "reduzir flashes": pulso de escala).
/// Oculto quando o kit não tem dash nem pulo aéreo.
/// </summary>
[DefaultExecutionOrder(43)]
public sealed class ResourceIndicatorView : MonoBehaviour
{
    private const int MaxPips = 3;

    [SerializeField] private PlayerController controller;
    [SerializeField] private FeedbackTuning tuning;
    [Tooltip("Pai do visor e dos pips (o Sprite, para acompanhar squash e flip).")]
    [SerializeField] private Transform anchor;
    [Tooltip("Posição local do visor no pai (ajustar olhando a arte).")]
    [SerializeField] private Vector2 visorLocalPosition = new Vector2(0.06f, 0.12f);
    [SerializeField] private Vector2 visorSize = new Vector2(2f, 1f);
    [Tooltip("Posição local do primeiro pip (acima da cabeça).")]
    [SerializeField] private Vector2 pipsLocalPosition = new Vector2(-0.06f, 0.42f);
    [SerializeField] private float pipSpacing = 0.07f;
    [SerializeField] private int sortingOrderOffset = 1;

    private SpriteRenderer visor;
    private SpriteRenderer[] pips;
    private SpriteRenderer source;
    private float flashTimer;

    private void Awake()
    {
        if (controller == null) controller = GetComponent<PlayerController>();
        if (tuning == null) tuning = ScriptableObject.CreateInstance<FeedbackTuning>();
        if (anchor == null) anchor = transform;
        source = anchor.GetComponent<SpriteRenderer>();

        visor = CreatePixel("Visor", visorLocalPosition, visorSize);
        pips = new SpriteRenderer[MaxPips];
        for (int i = 0; i < MaxPips; i++)
        {
            pips[i] = CreatePixel("Pip" + i, pipsLocalPosition + new Vector2(i * pipSpacing, 0f), new Vector2(1.4f, 1.4f));
        }
    }

    private SpriteRenderer CreatePixel(string name, Vector2 localPosition, Vector2 pixelSize)
    {
        var go = new GameObject(name);
        go.transform.SetParent(anchor, false);
        go.transform.localPosition = localPosition;
        // O pixel tem 1/32 u; o pai tem escala 1,7: compensa para ficar em pixels de tela do cenário.
        Vector3 parentScale = anchor.lossyScale;
        go.transform.localScale = new Vector3(pixelSize.x / Mathf.Max(0.01f, parentScale.x), pixelSize.y / Mathf.Max(0.01f, parentScale.y), 1f);
        var r = go.AddComponent<SpriteRenderer>();
        r.sprite = RuntimeSprites.WhitePixel;
        if (source != null)
        {
            r.sortingLayerID = source.sortingLayerID;
            r.sortingOrder = source.sortingOrder + sortingOrderOffset;
        }

        return r;
    }

    private void OnEnable()
    {
        EventBus<PlayerAbilityRefilled>.Subscribe(HandleRefilled);
    }

    private void OnDisable()
    {
        EventBus<PlayerAbilityRefilled>.Unsubscribe(HandleRefilled);
    }

    private void HandleRefilled(PlayerAbilityRefilled evt)
    {
        flashTimer = tuning.RefillFlashTime;
    }

    private void LateUpdate()
    {
        if (controller == null || controller.Motor == null) return;
        PlayerSnapshot snap = controller.Snapshot;

        bool show = snap.Visible && (snap.MaxDashes > 0 || snap.MaxAirJumps > 0);
        flashTimer = Mathf.Max(0f, flashTimer - Time.deltaTime);
        bool flashing = flashTimer > 0f;
        bool reduceFlashes = FeedbackSettings.ReduceFlashes;

        // Visor: cargas de dash.
        visor.enabled = show && snap.MaxDashes > 0;
        if (visor.enabled)
        {
            Color color = snap.DashesLeft <= 0 ? tuning.DashEmptyColor : snap.DashesLeft == 1 ? tuning.DashOneColor : tuning.DashTwoColor;
            float shape = snap.DashesLeft <= 0 ? tuning.VisorEmptyScale : 1f;
            if (snap.DashesLeft >= 2 && !reduceFlashes)
            {
                float pulse = 0.75f + 0.25f * Mathf.Sin(Time.time * tuning.VisorPulseRate * Mathf.PI * 2f);
                color = Color.Lerp(color * 0.6f, color, pulse);
            }

            if (flashing)
            {
                if (reduceFlashes) shape *= 1.25f;
                else color = tuning.RefillFlashColor;
            }

            color.a = 1f;
            visor.color = color;
            Vector3 baseScale = PixelScale(visorSize);
            visor.transform.localScale = new Vector3(baseScale.x * shape, baseScale.y * Mathf.Lerp(1f, shape, 0.5f), 1f);
        }

        // Pips: um por pulo aéreo disponível.
        for (int i = 0; i < pips.Length; i++)
        {
            bool on = show && i < snap.MaxAirJumps;
            pips[i].enabled = on;
            if (!on) continue;
            bool available = i < snap.AirJumpsLeft;
            Color c = available ? tuning.DashOneColor : tuning.DashEmptyColor;
            if (flashing && !reduceFlashes && available) c = tuning.RefillFlashColor;
            c.a = available ? 1f : 0.45f;
            pips[i].color = c;
        }
    }

    private Vector3 PixelScale(Vector2 pixelSize)
    {
        Vector3 parentScale = anchor.lossyScale;
        return new Vector3(pixelSize.x / Mathf.Max(0.01f, Mathf.Abs(parentScale.x)), pixelSize.y / Mathf.Max(0.01f, Mathf.Abs(parentScale.y)), 1f);
    }
}
