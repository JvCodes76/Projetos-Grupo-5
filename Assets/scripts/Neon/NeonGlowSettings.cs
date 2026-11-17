using System;
using UnityEngine;

/// <summary>
/// Configuração única do brilho de neon do jogo (asset em Assets/Resources/NeonGlowSettings). Mexer aqui muda todas
/// as fases de uma vez, sem tocar em cena, prefab ou material; em Play Mode os ajustes valem na hora.
///
/// Duas metades:
/// - Detecção + brilho: o shader "Roguelike/2D/Neon Sprite" acha o neon pela cor do pixel (saturado e claro) e o
///   empurra para HDR. O <see cref="NeonGlow"/> troca o material padrão dos tilemaps e sprites por ele ao carregar a
///   fase, então repintar ou refazer um level não exige nenhum passo extra.
/// - Bloom: Volume global criado em runtime, que espalha a luz só do que passou de 1.0 (ou seja, só do neon).
///
/// Sem este asset em Resources o sistema inteiro fica desligado.
/// </summary>
[CreateAssetMenu(menuName = "Roguelike/Neon Glow Settings", fileName = "NeonGlowSettings")]
public sealed class NeonGlowSettings : ScriptableObject
{
    /// <summary>Caminho em Resources (sem extensão).</summary>
    public const string ResourcePath = "NeonGlowSettings";

    public const string ShaderName = "Roguelike/2D/Neon Sprite";

    public enum BloomQuality
    {
        Baixa,
        Media,
        Alta,
    }

    [Header("Geral")]
    [Tooltip("Desligado, o jogo volta ao visual original (materiais padrão e sem bloom).")]
    public bool active = true;

    [Tooltip("Shader do neon. Referenciado aqui para entrar no build.")]
    public Shader neonShader;

    [Header("Detecção do neon (pela cor do pixel)")]
    [Tooltip("Saturação mínima para o pixel contar como neon. Suba se coisas demais estiverem brilhando; 0 inclui branco e cinza claros.")]
    [Range(0f, 1f)] public float minSaturation = 0.55f;

    [Tooltip("Brilho mínimo para o pixel contar como neon. Suba para sobrar só as partes mais acesas.")]
    [Range(0f, 1f)] public float minBrightness = 0.6f;

    [Tooltip("Largura da transição entre 'não é neon' e 'é neon'. Maior = borda mais suave, menor = recorte seco.")]
    [Range(0.01f, 0.5f)] public float softness = 0.15f;

    [Header("Brilho")]
    [Tooltip("Quanto o neon passa do branco (HDR). 0 = sem brilho. É o que alimenta o bloom.")]
    [Range(0f, 8f)] public float glowIntensity = 1.6f;

    [Tooltip("Tinta aplicada ao brilho (branco = cor original do sprite).")]
    [ColorUsage(false)] public Color glowTint = Color.white;

    [Header("Animação")]
    [Tooltip("Respiração lenta do brilho (0 = fixo).")]
    [Range(0f, 1f)] public float pulse = 0.15f;

    [Tooltip("Falhas rápidas, como letreiro com mau contato (0 = nunca falha). Zerado com a opção 'reduzir flashes'.")]
    [Range(0f, 1f)] public float flicker = 0.25f;

    [Tooltip("Velocidade da animação.")]
    [Min(0f)] public float speed = 0.6f;

    [Tooltip("Tamanho (unidades) do bloco que pisca junto. Menor = cada placa com ritmo próprio.")]
    [Min(0.25f)] public float flickerCellSize = 4f;

    [Header("Bloom (pós-processamento)")]
    public bool bloom = true;

    [Tooltip("Só o que passar deste valor espalha luz. Em 1, apenas o neon (que está em HDR) brilha.")]
    [Min(0f)] public float bloomThreshold = 1f;

    [Min(0f)] public float bloomIntensity = 1.2f;

    [Tooltip("Quão longe a luz se espalha.")]
    [Range(0f, 1f)] public float bloomScatter = 0.65f;

    [ColorUsage(false)] public Color bloomTint = Color.white;

    [Tooltip("Baixa = 1/4 da resolução e menos passadas (mais leve); Alta = filtro de alta qualidade.")]
    public BloomQuality bloomQuality = BloomQuality.Media;

    [Tooltip("Prioridade do Volume global criado em runtime (maior vence outros Volumes da cena).")]
    public float volumePriority;

    [Header("Onde aplicar (automático ao carregar cada fase)")]
    [Tooltip("Troca o material padrão dos TilemapRenderers pelo de neon.")]
    public bool applyToTilemaps = true;

    [Tooltip("Troca o material padrão dos SpriteRenderers (placas, props) pelo de neon.")]
    public bool applyToSprites = true;

    [Tooltip("Sorting layers que nunca recebem o neon. Para excluir um objeto só, use o componente NeonExclude.")]
    public string[] excludedSortingLayers = { "Player", "Enemies", "HUD", "GIF" };

    /// <summary>Algum valor mudou no Inspector (só Editor; em runtime chame <see cref="NotifyChanged"/>).</summary>
    public static event Action Changed;

    private static readonly int ParamsId = Shader.PropertyToID("_NeonParams");
    private static readonly int TintId = Shader.PropertyToID("_NeonTint");
    private static readonly int FlickerId = Shader.PropertyToID("_NeonFlicker");

    private void OnValidate()
    {
        Changed?.Invoke();
    }

    /// <summary>Avisa o <see cref="NeonGlow"/> depois de mudar valores por código.</summary>
    public void NotifyChanged()
    {
        Changed?.Invoke();
    }

    /// <summary>Envia os parâmetros para o shader (globais). <paramref name="reduceFlashes"/> zera as falhas rápidas.</summary>
    public void ApplyShaderGlobals(bool reduceFlashes)
    {
        if (!active)
        {
            ClearShaderGlobals();
            return;
        }

        Shader.SetGlobalVector(ParamsId, new Vector4(minSaturation, minBrightness, softness, glowIntensity));
        Shader.SetGlobalVector(TintId, new Vector4(glowTint.r, glowTint.g, glowTint.b, 1f));
        Shader.SetGlobalVector(FlickerId, new Vector4(pulse, reduceFlashes ? 0f : flicker, speed, 1f / Mathf.Max(0.25f, flickerCellSize)));
    }

    /// <summary>Desliga o brilho no shader (intensidade 0): o sprite sai igual ao material padrão.</summary>
    public static void ClearShaderGlobals()
    {
        Shader.SetGlobalVector(ParamsId, Vector4.zero);
        Shader.SetGlobalVector(TintId, Vector4.zero);
        Shader.SetGlobalVector(FlickerId, Vector4.zero);
    }
}
