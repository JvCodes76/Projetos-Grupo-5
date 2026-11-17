using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Atalhos do neon no Editor (menu Tools → Neon) e envio dos parâmetros ao shader fora do Play Mode, para um material
/// de neon atribuído à mão aparecer aceso na Scene View. O bloom em si só existe em Play Mode.
/// </summary>
[InitializeOnLoad]
public static class NeonGlowMenu
{
    private const string SettingsPath = "Assets/Resources/" + NeonGlowSettings.ResourcePath + ".asset";
    private const string MaterialPath = "Assets/Shaders/Neon/NeonSprite.mat";

    static NeonGlowMenu()
    {
        NeonGlowSettings.Changed += PushGlobalsInEditMode;
        EditorApplication.playModeStateChanged += HandlePlayModeChanged;
        EditorApplication.delayCall += PushGlobalsInEditMode; // o AssetDatabase ainda não está pronto no construtor
    }

    private static void HandlePlayModeChanged(PlayModeStateChange change)
    {
        if (change == PlayModeStateChange.EnteredEditMode) PushGlobalsInEditMode();
    }

    private static void PushGlobalsInEditMode()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;

        var settings = AssetDatabase.LoadAssetAtPath<NeonGlowSettings>(SettingsPath);
        if (settings != null) settings.ApplyShaderGlobals(false);
        else NeonGlowSettings.ClearShaderGlobals();

        SceneView.RepaintAll();
    }

    [MenuItem("Tools/Neon/Abrir configurações")]
    private static void OpenSettings()
    {
        var settings = AssetDatabase.LoadAssetAtPath<NeonGlowSettings>(SettingsPath);
        if (settings == null)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath));
            settings = ScriptableObject.CreateInstance<NeonGlowSettings>();
            settings.neonShader = Shader.Find(NeonGlowSettings.ShaderName);
            AssetDatabase.CreateAsset(settings, SettingsPath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Neon] - Configurações criadas em {SettingsPath}");
        }

        Selection.activeObject = settings;
        EditorGUIUtility.PingObject(settings);
    }

    [MenuItem("Tools/Neon/Criar material de neon (uso manual)")]
    private static void CreateMaterial()
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (material == null)
        {
            Shader shader = Shader.Find(NeonGlowSettings.ShaderName);
            if (shader == null)
            {
                Debug.LogError($"[Neon] - Shader '{NeonGlowSettings.ShaderName}' não encontrado");
                return;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(MaterialPath));
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, MaterialPath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Neon] - Material criado em {MaterialPath}");
        }

        Selection.activeObject = material;
        EditorGUIUtility.PingObject(material);
    }
}
