using UnityEngine;

/// <summary>
/// Animação de entrada: toda vez que o objeto é ativado, ele vem de um deslocamento/escala/rotação até a pose
/// montada no Editor, com um leve "passa do ponto e volta". Usa tempo real (as telas de resultado aparecem com o
/// jogo pausado em timeScale 0). Depois de terminar não faz mais nada por frame.
///
/// Não usar em objeto posicionado por LayoutGroup: anime um filho dele.
/// </summary>
[AddComponentMenu("UI/Kit/Enter")]
[DisallowMultipleComponent]
public sealed class UiEnter : MonoBehaviour
{
    [Tooltip("De onde o objeto vem, relativo à pose final (px).")]
    [SerializeField] private Vector2 fromOffset = new Vector2(-120f, 0f);

    [Tooltip("Escala inicial (1 = sem efeito de escala).")]
    [SerializeField] private float fromScale = 1f;

    [Tooltip("Rotação inicial somada à pose final (graus).")]
    [SerializeField] private float fromAngle;

    [Min(0.01f)] [SerializeField] private float duration = 0.28f;

    [Tooltip("Espera antes de começar (s). Use valores crescentes para entradas em cascata.")]
    [Min(0f)] [SerializeField] private float delay;

    [Tooltip("Quanto passa do ponto antes de assentar (0 = desaceleração simples).")]
    [Range(0f, 3f)] [SerializeField] private float overshoot = 1.4f;

    [Tooltip("Aparece do transparente (usa CanvasGroup, criado se faltar).")]
    [SerializeField] private bool fade = true;

    private RectTransform rect;
    private CanvasGroup group;
    private Vector2 restPosition;
    private Vector3 restScale;
    private float restAngle;
    private float elapsed;
    private bool captured;
    private bool playing;

    /// <summary>Configura por código (usado pelo construtor de telas).</summary>
    public void Setup(Vector2 offset, float delaySeconds, float durationSeconds = 0.28f, float scale = 1f, float angle = 0f)
    {
        fromOffset = offset;
        delay = delaySeconds;
        duration = Mathf.Max(0.01f, durationSeconds);
        fromScale = scale;
        fromAngle = angle;
    }

    private void Capture()
    {
        if (captured) return;
        captured = true;

        rect = (RectTransform)transform;
        restPosition = rect.anchoredPosition;
        restScale = rect.localScale;
        restAngle = rect.localEulerAngles.z;

        if (fade)
        {
            group = GetComponent<CanvasGroup>();
            if (group == null) group = gameObject.AddComponent<CanvasGroup>();
        }
    }

    private void OnEnable()
    {
        Capture();
        elapsed = 0f;
        playing = true;
        Apply(0f);
    }

    private void OnDisable()
    {
        // Volta à pose final: quem ler a posição com o objeto desligado vê o valor do Editor.
        if (captured) Apply(1f);
        playing = false;
    }

    private void Update()
    {
        if (!playing) return;

        elapsed += Time.unscaledDeltaTime;
        float t = Mathf.Clamp01((elapsed - delay) / duration);
        Apply(t);
        if (t >= 1f) playing = false;
    }

    private void Apply(float t)
    {
        float eased = EaseOutBack(t, overshoot);
        rect.anchoredPosition = restPosition + fromOffset * (1f - eased);
        rect.localScale = restScale * Mathf.LerpUnclamped(fromScale, 1f, eased);
        rect.localRotation = Quaternion.Euler(0f, 0f, restAngle + fromAngle * (1f - eased));
        if (group != null) group.alpha = Mathf.Clamp01(t * 3f);
    }

    private static float EaseOutBack(float t, float s)
    {
        float u = t - 1f;
        return 1f + u * u * ((s + 1f) * u + s);
    }
}
