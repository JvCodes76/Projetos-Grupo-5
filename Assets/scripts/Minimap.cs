using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Tilemaps;
using UnityEngine.UI;
using Roguelike.Events;

// Minimapa gerado a partir de uma "screenshot" da fase atual.
// No início da fase, uma câmera temporária fotografa a fase inteira (limites dos tilemaps)
// numa RenderTexture uma única vez; ela é exibida num RawImage e o marcador do jogador anda por cima.
public class Minimap : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private RawImage mapImage;
    [SerializeField] private RectTransform playerMarker;
    [Tooltip("Ajusta a proporção do minimapa à da fase (opcional)")]
    [SerializeField] private AspectRatioFitter aspectFitter;

    [Header("Captura")]
    [Tooltip("Resolução da foto, em pixels por unidade do mundo")]
    [SerializeField] private float pixelsPerUnit = 6f;
    [SerializeField] private int maxTextureSize = 2048;
    [Tooltip("Layers que aparecem na foto (jogador, inimigos e UI ficam de fora por padrão)")]
    [SerializeField] private LayerMask captureLayers = ~0;
    [SerializeField] private Color backgroundColor = new Color(0.05f, 0.05f, 0.1f, 1f);
    [Tooltip("Margem em volta da fase, em unidades do mundo")]
    [SerializeField] private float padding = 1f;
    [Tooltip("O parallax acompanha a câmera, então fica fora de lugar na foto")]
    [SerializeField] private bool hideParallax = true;
    [Tooltip("Objetos com essas tags somem da foto (ex.: moedas, que são coletadas durante a fase)")]
    [SerializeField] private string[] hiddenTags = { "Coin" };

    [Header("Limites (opcional)")]
    [Tooltip("Se ligado, usa customBounds em vez de calcular pelos tilemaps")]
    [SerializeField] private bool useCustomBounds = false;
    [SerializeField] private Rect customBounds = new Rect(0f, 0f, 50f, 30f);

    private Transform player;
    private Rect levelBounds;
    private RenderTexture mapTexture;

    private void Reset()
    {
        captureLayers = ~LayerMask.GetMask("Player", "Enemy", "UI");
    }

    private void OnEnable()
    {
        EventBus<PlayerSpawned>.Subscribe(HandlePlayerSpawned);
    }

    private void OnDisable()
    {
        EventBus<PlayerSpawned>.Unsubscribe(HandlePlayerSpawned);
    }

    private void HandlePlayerSpawned(PlayerSpawned evt)
    {
        player = evt.Player.transform;
    }

    private void Start()
    {
        if (mapImage == null)
        {
            Debug.LogWarning("Minimap: RawImage do mapa não atribuído!");
            enabled = false;
            return;
        }

        if (!TryCalculateLevelBounds(out levelBounds))
        {
            Debug.LogWarning("Minimap: não achei tilemaps nem CameraBoundary para definir os limites da fase.");
            gameObject.SetActive(false);
            return;
        }

        CaptureLevel();
    }

    private void LateUpdate()
    {
        if (playerMarker == null) return;

        // O jogador é atribuído via PlayerSpawned, que pode chegar depois do Start deste script
        playerMarker.gameObject.SetActive(player != null);
        if (player == null) return;

        Vector2 normalized = Rect.PointToNormalized(levelBounds, player.position);
        playerMarker.anchorMin = normalized;
        playerMarker.anchorMax = normalized;
        playerMarker.anchoredPosition = Vector2.zero;
    }

    private void OnDestroy()
    {
        if (mapTexture != null)
        {
            mapTexture.Release();
            Destroy(mapTexture);
        }
    }

    // Tira a "screenshot" da fase inteira e coloca no RawImage
    private void CaptureLevel()
    {
        float ppu = Mathf.Min(pixelsPerUnit, maxTextureSize / Mathf.Max(levelBounds.width, levelBounds.height));
        int width = Mathf.Max(1, Mathf.RoundToInt(levelBounds.width * ppu));
        int height = Mathf.Max(1, Mathf.RoundToInt(levelBounds.height * ppu));

        mapTexture = new RenderTexture(width, height, 24)
        {
            name = "MinimapCapture",
            filterMode = FilterMode.Bilinear
        };

        GameObject cameraObject = new GameObject("MinimapCaptureCamera");
        Camera captureCamera = cameraObject.AddComponent<Camera>();
        captureCamera.enabled = false;
        captureCamera.orthographic = true;
        captureCamera.orthographicSize = levelBounds.height / 2f;
        captureCamera.aspect = levelBounds.width / levelBounds.height;
        captureCamera.transform.position = new Vector3(levelBounds.center.x, levelBounds.center.y, -100f);
        captureCamera.nearClipPlane = 0.01f;
        captureCamera.farClipPlane = 1000f;
        captureCamera.cullingMask = captureLayers;
        captureCamera.clearFlags = CameraClearFlags.SolidColor;
        captureCamera.backgroundColor = backgroundColor;
        captureCamera.targetTexture = mapTexture;

        List<Renderer> hiddenRenderers = HideRenderersForCapture();

        RenderPipeline.StandardRequest request = new RenderPipeline.StandardRequest();
        if (RenderPipeline.SupportsRenderRequest(captureCamera, request))
        {
            request.destination = mapTexture;
            RenderPipeline.SubmitRenderRequest(captureCamera, request);
        }
        else
        {
            captureCamera.Render();
        }

        foreach (Renderer hidden in hiddenRenderers)
        {
            hidden.enabled = true;
        }

        captureCamera.targetTexture = null;
        Destroy(cameraObject);

        mapImage.texture = mapTexture;
        if (aspectFitter != null)
        {
            aspectFitter.aspectRatio = levelBounds.width / levelBounds.height;
        }

        Debug.Log($"Minimap: fase capturada ({width}x{height}px, limites {levelBounds})");
    }

    private List<Renderer> HideRenderersForCapture()
    {
        List<Renderer> hidden = new List<Renderer>();

        if (hideParallax)
        {
            foreach (ParallaxBackground parallax in FindObjectsByType<ParallaxBackground>(FindObjectsSortMode.None))
            {
                HideRenderers(parallax.gameObject, hidden);
            }
        }

        foreach (string tag in hiddenTags)
        {
            if (string.IsNullOrEmpty(tag)) continue;

            foreach (GameObject tagged in GameObject.FindGameObjectsWithTag(tag))
            {
                HideRenderers(tagged, hidden);
            }
        }

        return hidden;
    }

    private static void HideRenderers(GameObject root, List<Renderer> hidden)
    {
        foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>())
        {
            if (!renderer.enabled) continue;
            renderer.enabled = false;
            hidden.Add(renderer);
        }
    }

    // Limites da fase: união dos tilemaps visíveis na foto; se não houver, usa o CameraBoundary + meia tela
    private bool TryCalculateLevelBounds(out Rect bounds)
    {
        if (useCustomBounds)
        {
            bounds = customBounds;
            return true;
        }

        bool found = false;
        Bounds union = new Bounds();

        foreach (TilemapRenderer tilemapRenderer in FindObjectsByType<TilemapRenderer>(FindObjectsSortMode.None))
        {
            if (!tilemapRenderer.enabled || (captureLayers & (1 << tilemapRenderer.gameObject.layer)) == 0) continue;

            Tilemap tilemap = tilemapRenderer.GetComponent<Tilemap>();
            tilemap.CompressBounds();
            if (tilemap.GetUsedTilesCount() == 0) continue;

            if (!found)
            {
                union = tilemapRenderer.bounds;
                found = true;
            }
            else
            {
                union.Encapsulate(tilemapRenderer.bounds);
            }
        }

        if (!found)
        {
            CameraBoundary boundary = FindFirstObjectByType<CameraBoundary>();
            Camera mainCamera = Camera.main;
            if (boundary == null || mainCamera == null)
            {
                bounds = default;
                return false;
            }

            float halfHeight = mainCamera.orthographicSize;
            float halfWidth = halfHeight * mainCamera.aspect;
            union.SetMinMax(
                new Vector3(boundary.minX - halfWidth, boundary.minY - halfHeight),
                new Vector3(boundary.maxX + halfWidth, boundary.maxY + halfHeight));
        }

        bounds = Rect.MinMaxRect(
            union.min.x - padding, union.min.y - padding,
            union.max.x + padding, union.max.y + padding);
        return true;
    }
}
