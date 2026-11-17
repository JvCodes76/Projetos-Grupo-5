using UnityEngine;

/// <summary>
/// Movimento contínuo e discreto (faixas de fundo, selos, avisos): balanço de posição, de rotação e pulso de escala
/// em senoide. Tempo real, sem alocação; custo de três senos por frame.
/// Não combinar com <see cref="UiEnter"/> no mesmo objeto (os dois escrevem a pose): use pai e filho.
/// </summary>
[AddComponentMenu("UI/Kit/Drift")]
[DisallowMultipleComponent]
public sealed class UiDrift : MonoBehaviour
{
    [Tooltip("Amplitude do vai-e-vem (px).")]
    [SerializeField] private Vector2 move = new Vector2(12f, 0f);

    [Tooltip("Amplitude do balanço (graus).")]
    [SerializeField] private float angle;

    [Tooltip("Amplitude do pulso de escala (0,05 = ±5%).")]
    [SerializeField] private float scale;

    [Tooltip("Ciclos por segundo.")]
    [Min(0f)] [SerializeField] private float speed = 0.25f;

    [Tooltip("Defasagem (0..1) para objetos vizinhos não andarem juntos.")]
    [Range(0f, 1f)] [SerializeField] private float phase;

    private RectTransform rect;
    private Vector2 restPosition;
    private Vector3 restScale;
    private float restAngle;
    private bool captured;

    /// <summary>Configura por código (usado pelo construtor de telas).</summary>
    public void Setup(Vector2 moveAmplitude, float angleAmplitude, float scaleAmplitude, float cyclesPerSecond, float phaseOffset)
    {
        move = moveAmplitude;
        angle = angleAmplitude;
        scale = scaleAmplitude;
        speed = cyclesPerSecond;
        phase = phaseOffset;
    }

    private void OnEnable()
    {
        if (captured) return;
        captured = true;
        rect = (RectTransform)transform;
        restPosition = rect.anchoredPosition;
        restScale = rect.localScale;
        restAngle = rect.localEulerAngles.z;
    }

    private void OnDisable()
    {
        if (!captured) return;
        rect.anchoredPosition = restPosition;
        rect.localScale = restScale;
        rect.localRotation = Quaternion.Euler(0f, 0f, restAngle);
    }

    private void Update()
    {
        float t = (Time.unscaledTime * speed + phase) * (Mathf.PI * 2f);
        float s = Mathf.Sin(t);

        if (move != Vector2.zero) rect.anchoredPosition = restPosition + move * s;
        if (angle != 0f) rect.localRotation = Quaternion.Euler(0f, 0f, restAngle + angle * Mathf.Sin(t * 0.73f + 1.3f));
        if (scale != 0f) rect.localScale = restScale * (1f + scale * Mathf.Sin(t * 1.31f + 0.4f));
    }
}
