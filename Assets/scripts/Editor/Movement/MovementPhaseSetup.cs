using System.Collections.Generic;
using System.Text;
using Roguelike.MovementPhysics;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

/// <summary>
/// Ajustes de cena da M.16 (SPEC §14.3) que não são tiles: CameraBoundary para o ortho 5,625 com killPlaneY explícito,
/// offset do composite da Primeira (P20), KillZones nos 3 vãos entre os pilares da Primeira (RF-42) e a lista de
/// colisores não-trigger na layer 9 (Decorations). Também aplica os LevelPatch de cada fase. Idempotente: os valores
/// são absolutos (rodar de novo não desloca nada). Menu: Tools/Movement/Setup/Configure Phases.
/// Batch: MovementPhaseSetup.ConfigurePhasesBatch.
/// </summary>
public static class MovementPhaseSetup
{
    private struct BoundaryValues
    {
        public float MinX, MaxX, MinY, MaxY, KillPlaneY;

        public BoundaryValues(float minX, float maxX, float minY, float maxY, float killPlaneY)
        {
            MinX = minX;
            MaxX = maxX;
            MinY = minY;
            MaxY = maxY;
            KillPlaneY = killPlaneY;
        }
    }

    // Limites do centro reajustados do ortho 5 (17,8 × 10 u) para o 5,625 (20 × 11,25 u): cada lado entra 1,11 u em x e
    // 0,625 u em y, para a área visível nas bordas continuar a mesma de antes. killPlaneY = minY − 5,625 − 1 (o padrão do
    // SPEC §11), agora explícito: abaixo do chão mais baixo de cada fase e nunca acima da borda de baixo da câmera.
    private static readonly Dictionary<string, BoundaryValues> Boundaries = new Dictionary<string, BoundaryValues>
    {
        { "PrimeiraFase", new BoundaryValues(-18.89f, 87.39f, 0.63f, 53.38f, -6f) },   // antes: −20 / 88,5 / 0 / 54
        { "QuartaFase 1", new BoundaryValues(-18.39f, 37.89f, 0.63f, 49.38f, -6f) },   // antes: −19,5 / 39 / 0 / 50
        { "QuintaFase", new BoundaryValues(3.11f, 59.19f, -0.38f, 59.38f, -7f) },      // antes: 2 / 60,3 / −1 / 60
    };

    private struct KillZoneSpec
    {
        public string Name;
        public float MinX, MaxX, MinY, MaxY;

        public KillZoneSpec(string name, float minX, float maxX, float minY, float maxY)
        {
            Name = name;
            MinX = minX;
            MaxX = maxX;
            MinY = minY;
            MaxY = maxY;
        }
    }

    // Vãos entre os pilares da Primeira (já sem o offset −0,25 do composite): topos dos pilares em y 26,1 / 25,1 / 27,1 /
    // 29,1. A zona começa 6 u abaixo do pilar mais baixo do vão: o kit base cai e volta ao chão seguro em ~0,7 s (RF-42),
    // e quem tem Salto de Parede ainda sobe a chaminé de 2–3 u se reagir antes.
    private static readonly KillZoneSpec[] PrimeiraKillZones =
    {
        new KillZoneSpec("KillZone_Vao1", 36.5f, 39.5f, -12f, 19.1f),
        new KillZoneSpec("KillZone_Vao2", 43.5f, 45.5f, -12f, 19.1f),
        new KillZoneSpec("KillZone_Vao3", 49.5f, 51.5f, -12f, 21.1f),
    };

    private const string KillZoneParentName = "KillZones";
    private const int DecorationsLayer = 9;

    [MenuItem("Tools/Movement/Setup/Configure Phases (M.16)")]
    public static void ConfigurePhasesMenu()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        Debug.Log(ConfigurePhases());
    }

    /// <summary>Batch: (re)cria os LevelPatch_*.asset, configura e salva as 3 fases.</summary>
    public static void ConfigurePhasesBatch()
    {
        MovementLevelPatches.CreatePatches();
        Debug.Log(ConfigurePhases());
        EditorApplication.Exit(0);
    }

    /// <summary>Configura as 3 fases e salva as cenas alteradas. Devolve o relatório (inclui a auditoria da layer 9).</summary>
    public static string ConfigurePhases()
    {
        var report = new StringBuilder("[Movement] - Configuração das fases (M.16)\n");
        foreach (string path in ReachabilityWindow.PhaseScenes)
        {
            Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            report.Append("== ").Append(scene.name).Append('\n');
            bool dirty = ConfigureBoundary(scene, report);
            if (scene.name == "PrimeiraFase")
            {
                dirty |= RemoveCompositeOffset(scene, report);
                dirty |= EnsureKillZones(scene, report);
            }

            int patches = LevelPatcher.ApplyAll(scene);
            if (patches > 0)
            {
                dirty = true;
                report.Append("  LevelPatch aplicados: ").Append(patches).Append('\n');
            }

            CheckSpawnAndGoal(scene, report);
            AuditDecorationColliders(scene, report);
            if (dirty) EditorSceneManager.SaveScene(scene);
        }

        return report.ToString();
    }

    private static bool ConfigureBoundary(Scene scene, StringBuilder report)
    {
        if (!Boundaries.TryGetValue(scene.name, out BoundaryValues v)) return false;
        CameraBoundary boundary = FindInScene<CameraBoundary>(scene);
        if (boundary == null)
        {
            report.Append("  ERRO: sem CameraBoundary\n");
            return false;
        }

        var so = new SerializedObject(boundary);
        SetFloat(so, "minX", v.MinX);
        SetFloat(so, "maxX", v.MaxX);
        SetFloat(so, "minY", v.MinY);
        SetFloat(so, "maxY", v.MaxY);
        so.FindProperty("useKillPlane").boolValue = true;
        SetFloat(so, "killPlaneY", v.KillPlaneY);
        bool changed = so.ApplyModifiedPropertiesWithoutUndo();
        report.Append($"  CameraBoundary: x [{v.MinX}; {v.MaxX}] y [{v.MinY}; {v.MaxY}] killPlaneY {v.KillPlaneY}{(changed ? "" : " (já estava)")}\n");
        return changed;
    }

    private static void SetFloat(SerializedObject so, string name, float value)
    {
        so.FindProperty(name).floatValue = value;
    }

    // P20: na Primeira, o composite do Tilemap "IntGrid" (o outro Tilemap de colisão, além do "AutoLayer") tinha
    // m_Offset.y = −0,25 (override da instância do prefab do LDtk), o que deixava parte da colisão 0,25 u abaixo dos
    // tiles. Zera o offset de todo composite de Tilemap sólido da cena (reverte o override; o prefab tem offset zero).
    private static bool RemoveCompositeOffset(Scene scene, StringBuilder report)
    {
        bool changed = false;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (TilemapCollider2D tc in root.GetComponentsInChildren<TilemapCollider2D>(true))
            {
                if (tc.gameObject.layer != MovementLayers.Ground) continue;
                var composite = tc.GetComponent<CompositeCollider2D>();
                if (composite == null || composite.offset == Vector2.zero) continue;

                Vector2 old = composite.offset;
                var so = new SerializedObject(composite);
                SerializedProperty offset = so.FindProperty("m_Offset");
                if (PrefabUtility.IsPartOfPrefabInstance(composite) && offset.prefabOverride)
                {
                    PrefabUtility.RevertPropertyOverride(offset, InteractionMode.AutomatedAction);
                }

                if (composite.offset != Vector2.zero)
                {
                    composite.offset = Vector2.zero;
                    EditorUtility.SetDirty(composite);
                }

                composite.GenerateGeometry();
                if (PrefabUtility.IsPartOfPrefabInstance(composite)) PrefabUtility.RecordPrefabInstancePropertyModifications(composite);
                changed = true;
                report.Append($"  Composite '{PathOf(composite.transform)}': offset {old} → {composite.offset}\n");
            }
        }

        if (changed) EditorSceneManager.MarkSceneDirty(scene);
        else report.Append("  Composites: nenhum offset a corrigir\n");
        return changed;
    }

    private static bool EnsureKillZones(Scene scene, StringBuilder report)
    {
        bool changed = false;
        GameObject parent = FindRoot(scene, KillZoneParentName);
        if (parent == null)
        {
            parent = new GameObject(KillZoneParentName);
            SceneManager.MoveGameObjectToScene(parent, scene);
            changed = true;
        }

        foreach (KillZoneSpec k in PrimeiraKillZones)
        {
            Transform t = parent.transform.Find(k.Name);
            GameObject go = t != null ? t.gameObject : null;
            if (go == null)
            {
                go = new GameObject(k.Name);
                go.transform.SetParent(parent.transform, false);
                changed = true;
            }

            var center = new Vector3((k.MinX + k.MaxX) * 0.5f, (k.MinY + k.MaxY) * 0.5f, 0f);
            var size = new Vector2(k.MaxX - k.MinX, k.MaxY - k.MinY);
            var box = go.GetComponent<BoxCollider2D>();
            if (box == null)
            {
                box = go.AddComponent<BoxCollider2D>();
                changed = true;
            }

            if (go.layer != MovementLayers.KillZone || go.transform.position != center || go.transform.localScale != Vector3.one
                || !box.isTrigger || box.size != size || box.offset != Vector2.zero)
            {
                go.layer = MovementLayers.KillZone;
                go.transform.position = center;
                go.transform.rotation = Quaternion.identity;
                go.transform.localScale = Vector3.one;
                box.isTrigger = true;
                box.size = size;
                box.offset = Vector2.zero;
                EditorUtility.SetDirty(go);
                changed = true;
            }

            report.Append($"  {k.Name}: x [{k.MinX}; {k.MaxX}] y [{k.MinY}; {k.MaxY}]\n");
        }

        if (changed) EditorSceneManager.MarkSceneDirty(scene);
        return changed;
    }

    // Spawn acima do chão (o motor cai até pousar) e objetivo sobreposto ao corpo em pé no chão mais próximo abaixo dele.
    private static void CheckSpawnAndGoal(Scene scene, StringBuilder report)
    {
        ExtractedLevel level = LevelAabbExtractor.Extract(scene);
        if (!level.HasSpawn)
        {
            report.Append("  ERRO: sem SpawnPoint\n");
            return;
        }

        float spawnFloor = FloorBelow(level, level.Spawn);
        report.Append($"  Spawn ({level.Spawn.x:0.##}; {level.Spawn.y:0.##}): chão em {spawnFloor:0.##} ({(level.Spawn.y >= spawnFloor - 0.001f ? "ok" : "DENTRO DO CHÃO")})\n");
        foreach (Rect goal in level.Goals)
        {
            float floor = FloorBelow(level, new Vector2(goal.center.x, goal.yMax));
            bool overlaps = goal.yMin < floor + 1.26f && goal.yMax > floor;
            report.Append($"  Objetivo {goal}: chão em {floor:0.##} ({(overlaps ? "ok" : "FORA DO ALCANCE DO CORPO EM PÉ")})\n");
        }
    }

    private static float FloorBelow(ExtractedLevel level, Vector2 point)
    {
        float best = float.NegativeInfinity;
        for (int i = 0; i < level.World.BoxCount; i++)
        {
            var b = level.World.GetBox(i);
            if (b.Kind != Roguelike.Movement.AabbKind.Ground) continue;
            if (point.x < b.Min.x || point.x > b.Max.x || b.Max.y > point.y + 0.3f) continue;
            if (b.Max.y > best) best = b.Max.y;
        }

        return best;
    }

    // SPEC §5.2: Decorations (9) fica fora de Solids. Lista os colisores não-trigger dessa layer para revisão manual.
    private static void AuditDecorationColliders(Scene scene, StringBuilder report)
    {
        int count = 0;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Collider2D c in root.GetComponentsInChildren<Collider2D>(true))
            {
                if (c.gameObject.layer != DecorationsLayer || c.isTrigger) continue;
                count++;
                Bounds b = c.bounds;
                report.Append($"  Layer 9 não-trigger: {PathOf(c.transform)} ({c.GetType().Name}, {(c.enabled && c.gameObject.activeInHierarchy ? "ativo" : "inativo")}) ")
                    .Append($"x [{b.min.x:0.##}; {b.max.x:0.##}] y [{b.min.y:0.##}; {b.max.y:0.##}]\n");
            }
        }

        if (count == 0) report.Append("  Layer 9: nenhum colisor não-trigger\n");
    }

    private static string PathOf(Transform t)
    {
        string path = t.name;
        while (t.parent != null)
        {
            t = t.parent;
            path = t.name + "/" + path;
        }

        return path;
    }

    private static GameObject FindRoot(Scene scene, string name)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.name == name) return root;
        }

        return null;
    }

    private static T FindInScene<T>(Scene scene) where T : Component
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            T found = root.GetComponentInChildren<T>(true);
            if (found != null) return found;
        }

        return null;
    }
}
