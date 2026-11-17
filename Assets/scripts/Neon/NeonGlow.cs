using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

/// <summary>
/// Liga o brilho de neon no jogo inteiro a partir do asset <see cref="NeonGlowSettings"/>, sem setup por cena:
/// - cria-se sozinho no boot (objeto persistente), se o asset existir em Resources;
/// - a cada fase carregada, troca o material padrão (Sprite-Lit-Default) dos tilemaps e sprites pelo de neon. Só troca
///   material "de fábrica": quem já usa material próprio fica como está;
/// - mantém um Volume global com Bloom e liga o pós-processamento na câmera principal.
///
/// Custo: a troca de materiais roda uma vez por carregamento de fase; por frame sobra uma comparação de câmera.
/// Todos os renderers trocados compartilham UM material, então o batching de sprites continua igual.
/// </summary>
public sealed class NeonGlow : MonoBehaviour
{
    private const string DefaultLitShaderName = "Universal Render Pipeline/2D/Sprite-Lit-Default";

    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int MaskTexId = Shader.PropertyToID("_MaskTex");
    private static readonly int NormalMapId = Shader.PropertyToID("_NormalMap");

    private static NeonGlow instance;

    private NeonGlowSettings settings;
    private Shader defaultLitShader;
    private Material neonMaterial;
    private bool warnedShader;
    private bool dirty = true;

    private Volume volume;
    private VolumeProfile profile;
    private Bloom bloomOverride;

    private Camera lastCamera;
    private Camera postCamera;   // câmera em que ESTE script ligou o pós-processamento
    private bool postWasOn;      // valor que ela tinha antes, para devolver

    // Pares renderer → material original, para desfazer a troca ao desligar o sistema.
    private readonly List<Renderer> swapped = new List<Renderer>();
    private readonly List<Material> originals = new List<Material>();
    private readonly HashSet<int> excludedLayers = new HashSet<int>();

    // ───────────────────────────── API pública ─────────────────────────────

    /// <summary>Aplica o material de neon aos renderers de um objeto criado depois do carregamento da fase.</summary>
    public static void ApplyTo(GameObject root)
    {
        if (instance == null || root == null || !instance.IsActive || !instance.EnsureMaterial()) return;

        instance.RebuildExcludedLayers();
        if (instance.settings.applyToTilemaps) instance.Swap(root.GetComponentsInChildren<TilemapRenderer>(true));
        if (instance.settings.applyToSprites) instance.Swap(root.GetComponentsInChildren<SpriteRenderer>(true));
    }

    /// <summary>Refaz tudo no próximo frame (depois de mudar o asset por código).</summary>
    public static void Refresh()
    {
        if (instance != null) instance.dirty = true;
    }

    // ───────────────────────────── Boot ─────────────────────────────

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        instance = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (instance != null) return;

        var loaded = Resources.Load<NeonGlowSettings>(NeonGlowSettings.ResourcePath);
        if (loaded == null) return; // sem o asset, o sistema fica desligado

        var go = new GameObject(nameof(NeonGlow));
        DontDestroyOnLoad(go);
        instance = go.AddComponent<NeonGlow>();
        instance.settings = loaded;
        instance.defaultLitShader = Shader.Find(DefaultLitShaderName);
    }

    private bool IsActive => settings != null && settings.active;

    private void OnEnable()
    {
        SceneManager.sceneLoaded += HandleSceneLoaded;
        NeonGlowSettings.Changed += MarkDirty;
        FeedbackSettings.Changed += MarkDirty; // "reduzir flashes" desliga as falhas do letreiro
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        NeonGlowSettings.Changed -= MarkDirty;
        FeedbackSettings.Changed -= MarkDirty;
    }

    private void OnDestroy()
    {
        Revert();
        RestoreCameraPost();
        NeonGlowSettings.ClearShaderGlobals();

        if (profile != null) Destroy(profile);
        if (neonMaterial != null) Destroy(neonMaterial);
        if (instance == this) instance = null;
    }

    private void MarkDirty()
    {
        dirty = true;
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Roda antes do Start dos objetos da cena: o primeiro frame da fase já sai com o neon.
        PruneDestroyed();
        if (IsActive) ApplyMaterials();
    }

    private void LateUpdate()
    {
        if (settings == null) return;

        Camera main = Camera.main;
        if (main != lastCamera)
        {
            lastCamera = main;
            UpdateCameraPost(main);
        }

        if (dirty)
        {
            dirty = false;
            ApplyAll();
        }
    }

    private void ApplyAll()
    {
        settings.ApplyShaderGlobals(FeedbackSettings.ReduceFlashes);

        // Desfaz e refaz: assim mudanças em "onde aplicar" valem na hora durante o Play Mode.
        Revert();
        if (IsActive) ApplyMaterials();

        ApplyBloom();
        UpdateCameraPost(lastCamera);
    }

    // ───────────────────────────── Materiais ─────────────────────────────

    private bool EnsureMaterial()
    {
        if (neonMaterial != null) return true;

        Shader shader = settings.neonShader != null ? settings.neonShader : Shader.Find(NeonGlowSettings.ShaderName);
        if (shader == null || !shader.isSupported)
        {
            if (!warnedShader)
            {
                warnedShader = true;
                Debug.LogWarning($"[Neon] - Shader '{NeonGlowSettings.ShaderName}' ausente ou com erro; os sprites ficam com o material padrão (o bloom continua)");
            }

            return false;
        }

        neonMaterial = new Material(shader) { name = "NeonSprite (runtime)", hideFlags = HideFlags.HideAndDontSave };
        return true;
    }

    private void ApplyMaterials()
    {
        if (!EnsureMaterial()) return;

        RebuildExcludedLayers();
        if (settings.applyToTilemaps)
        {
            Swap(FindObjectsByType<TilemapRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None));
        }

        if (settings.applyToSprites)
        {
            Swap(FindObjectsByType<SpriteRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None));
        }
    }

    private void RebuildExcludedLayers()
    {
        excludedLayers.Clear();
        string[] names = settings.excludedSortingLayers;
        if (names == null) return;

        for (int i = 0; i < names.Length; i++)
        {
            if (!string.IsNullOrEmpty(names[i])) excludedLayers.Add(SortingLayer.NameToID(names[i]));
        }
    }

    private void Swap(Renderer[] renderers)
    {
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer r = renderers[i];
            Material current = r.sharedMaterial;
            if (current == null || current == neonMaterial || !IsStockLitMaterial(current)) continue;
            if (excludedLayers.Contains(r.sortingLayerID)) continue;
            if (r.GetComponentInParent<NeonExclude>(true) != null) continue;

            swapped.Add(r);
            originals.Add(current);
            r.sharedMaterial = neonMaterial;
        }
    }

    // Só o material padrão sem personalização: mesmo shader, sem tinta, máscara ou normal map próprios.
    private bool IsStockLitMaterial(Material material)
    {
        Shader shader = material.shader;
        if (defaultLitShader == null)
        {
            if (shader == null || shader.name != DefaultLitShaderName) return false;
            defaultLitShader = shader;
        }
        else if (shader != defaultLitShader)
        {
            return false;
        }

        return material.GetColor(ColorId) == Color.white
            && material.GetTexture(MaskTexId) == null
            && material.GetTexture(NormalMapId) == null;
    }

    private void Revert()
    {
        for (int i = 0; i < swapped.Count; i++)
        {
            Renderer r = swapped[i];
            if (r != null && r.sharedMaterial == neonMaterial) r.sharedMaterial = originals[i];
        }

        swapped.Clear();
        originals.Clear();
    }

    private void PruneDestroyed()
    {
        for (int i = swapped.Count - 1; i >= 0; i--)
        {
            if (swapped[i] != null) continue;
            swapped.RemoveAt(i);
            originals.RemoveAt(i);
        }
    }

    // ───────────────────────────── Bloom ─────────────────────────────

    private void ApplyBloom()
    {
        bool on = IsActive && settings.bloom;
        if (!on)
        {
            if (volume != null) volume.enabled = false;
            return;
        }

        if (volume == null)
        {
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            profile.name = "NeonGlow (runtime)";
            profile.hideFlags = HideFlags.HideAndDontSave;
            bloomOverride = profile.Add<Bloom>();

            volume = gameObject.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = profile;
        }

        volume.enabled = true;
        volume.priority = settings.volumePriority;

        bloomOverride.threshold.Override(settings.bloomThreshold);
        bloomOverride.intensity.Override(settings.bloomIntensity);
        bloomOverride.scatter.Override(settings.bloomScatter);
        bloomOverride.tint.Override(settings.bloomTint);

        switch (settings.bloomQuality)
        {
            case NeonGlowSettings.BloomQuality.Baixa:
                bloomOverride.downscale.Override(BloomDownscaleMode.Quarter);
                bloomOverride.maxIterations.Override(4);
                bloomOverride.highQualityFiltering.Override(false);
                break;

            case NeonGlowSettings.BloomQuality.Alta:
                bloomOverride.downscale.Override(BloomDownscaleMode.Half);
                bloomOverride.maxIterations.Override(6);
                bloomOverride.highQualityFiltering.Override(true);
                break;

            default:
                bloomOverride.downscale.Override(BloomDownscaleMode.Half);
                bloomOverride.maxIterations.Override(5);
                bloomOverride.highQualityFiltering.Override(false);
                break;
        }
    }

    // O bloom só aparece com "Post Processing" ligado na câmera. Liga na câmera principal atual e devolve o valor
    // antigo quando ela deixa de ser a principal ou o bloom é desligado.
    private void UpdateCameraPost(Camera cam)
    {
        bool want = IsActive && settings.bloom;
        if (postCamera != null && (postCamera != cam || !want)) RestoreCameraPost();
        if (!want || cam == null || postCamera == cam) return;

        UniversalAdditionalCameraData data = cam.GetUniversalAdditionalCameraData();
        postWasOn = data.renderPostProcessing;
        data.renderPostProcessing = true;
        postCamera = cam;
    }

    private void RestoreCameraPost()
    {
        if (postCamera != null && !postWasOn)
        {
            postCamera.GetUniversalAdditionalCameraData().renderPostProcessing = false;
        }

        postCamera = null;
    }
}
