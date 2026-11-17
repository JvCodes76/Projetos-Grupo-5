using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Gera imagens das telas sem entrar em Play Mode (Tools → UI → Capturar prévias): clona o canvas para uma cena de
/// pré-visualização, liga o painel pedido e renderiza num JPG em Temp/UiPreview. Nada é salvo em cena ou prefab.
/// Serve para conferir o resultado depois de mexer no tema; as animações de entrada aparecem na pose final.
/// </summary>
public static class UiPreview
{
    public const string OutputDir = "Temp/UiPreview";

    [MenuItem("Tools/UI/Capturar prévias (Temp/UiPreview)")]
    private static void CaptureAllMenu()
    {
        CaptureAll();
        EditorUtility.RevealInFinder(OutputDir);
    }

    public static void CaptureAll()
    {
        const string run = "Assets/_Roguelike/Prefabs/RunSystems.prefab";
        CapturePrefab(run, "RunUI", "RunHud/Panel", "hud");
        CapturePrefab(run, "RunUI", "LevelResult/Panel", "resultado");
        CapturePrefab(run, "RunUI", "UpgradeSelection/Panel", "upgrades");
        CapturePrefab(run, "RunUI", "RunEnd/Panel", "fim");
        CaptureScene("Assets/Scenes/MainMenu.unity", "Canvas", null, "menu");
        CaptureScene("Assets/Scenes/SettingsMenu.unity", "Canvas", null, "config");
    }

    /// <summary>Captura um canvas de dentro de um prefab, ligando o painel em <paramref name="panelPath"/>.</summary>
    public static string CapturePrefab(string prefabPath, string canvasPath, string panelPath, string fileName)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab == null) return null;

        var instance = Object.Instantiate(prefab);
        instance.hideFlags = HideFlags.HideAndDontSave;
        try
        {
            Transform canvas = instance.transform.Find(canvasPath);
            return canvas != null ? Render(canvas.gameObject, panelPath, fileName, instance) : null;
        }
        finally
        {
            Object.DestroyImmediate(instance);
        }
    }

    /// <summary>Captura um canvas de uma cena (aberta temporariamente, sem salvar).</summary>
    public static string CaptureScene(string scenePath, string canvasName, string panelPath, string fileName)
    {
        Scene scene = SceneManager.GetSceneByPath(scenePath);
        bool wasLoaded = scene.IsValid() && scene.isLoaded;
        if (!wasLoaded) scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);

        GameObject clone = null;
        try
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name != canvasName) continue;
                clone = Object.Instantiate(root);
                clone.hideFlags = HideFlags.HideAndDontSave;
                break;
            }

            return clone != null ? Render(clone, panelPath, fileName, clone) : null;
        }
        finally
        {
            if (clone != null) Object.DestroyImmediate(clone);
            if (!wasLoaded) EditorSceneManager.CloseScene(scene, true);
        }
    }

    private static string Render(GameObject canvasObject, string panelPath, string fileName, GameObject owner)
    {
        const int width = 1920;
        const int height = 1080;

        Scene preview = EditorSceneManager.NewPreviewScene();
        var cameraObject = new GameObject("UiPreviewCamera") { hideFlags = HideFlags.HideAndDontSave };
        var texture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
        try
        {
            SceneManager.MoveGameObjectToScene(owner, preview);
            SceneManager.MoveGameObjectToScene(cameraObject, preview);

            var camera = cameraObject.AddComponent<Camera>();
            camera.scene = preview;
            camera.orthographic = true;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.16f, 0.2f, 0.3f, 1f); // faz as vezes do jogo atrás da UI
            camera.targetTexture = texture;
            camera.transform.position = new Vector3(0f, 0f, -100f);

            if (!string.IsNullOrEmpty(panelPath))
            {
                Transform panel = canvasObject.transform.Find(panelPath);
                if (panel != null) panel.gameObject.SetActive(true);
            }

            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 10f;

            // Refaz a escala do canvas para o tamanho da textura.
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            if (scaler != null)
            {
                scaler.enabled = false;
                scaler.enabled = true;
            }

            // Imagem animada sem sprite inicial (ex.: RainFX) apareceria como um bloco branco fora do Play Mode.
            foreach (Animator animator in canvasObject.GetComponentsInChildren<Animator>(true))
            {
                var animated = animator.GetComponent<Image>();
                if (animated != null && animated.sprite == null) animated.enabled = false;
            }

            Canvas.ForceUpdateCanvases();

            var request = new RenderPipeline.StandardRequest { destination = texture };
            if (RenderPipeline.SupportsRenderRequest(camera, request)) RenderPipeline.SubmitRenderRequest(camera, request);
            else camera.Render();

            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = texture;
            var image = new Texture2D(width, height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            image.Apply();
            RenderTexture.active = previous;

            Directory.CreateDirectory(OutputDir);
            string path = $"{OutputDir}/{fileName}.jpg";
            File.WriteAllBytes(path, image.EncodeToJPG(80));
            Object.DestroyImmediate(image);
            return path;
        }
        finally
        {
            Cleanup(cameraObject, texture);
            EditorSceneManager.ClosePreviewScene(preview);
        }
    }

    private static void Cleanup(GameObject cameraObject, RenderTexture texture)
    {
        var camera = cameraObject.GetComponent<Camera>();
        if (camera != null) camera.targetTexture = null;
        texture.Release();
        Object.DestroyImmediate(texture);
        Object.DestroyImmediate(cameraObject);
    }
}
