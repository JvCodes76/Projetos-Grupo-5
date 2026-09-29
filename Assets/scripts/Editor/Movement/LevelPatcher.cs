using System.Collections.Generic;
using System.Linq;
using Roguelike.Movement;
using Roguelike.MovementPhysics;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

/// <summary>
/// Aplica os <see cref="LevelPatch"/> de uma cena de forma idempotente (SPEC §14.2) em TODOS os Tilemaps sólidos da
/// cena (TilemapCollider2D na layer 6): nas fases do LDtk, o "IntGrid" tem a colisão e o "AutoLayer" o visual (na Quarta
/// os tiles do AutoLayer nem têm collider; na Quinta têm). Cada Tilemap recebe o próprio tile mais usado (o com collider,
/// se houver). Regenera o CompositeCollider2D e registra as mudanças como overrides da instância do prefab do LDtk
/// (sem isso a geometria do composite não é salva e o Physics2D não vê as células novas). Menu:
/// Tools/Movement/Apply Level Patches. Rode de novo depois de qualquer reimport do LDtk (o reimport apaga os overrides).
/// </summary>
public static class LevelPatcher
{
    [MenuItem("Tools/Movement/Apply Level Patches (cena aberta)")]
    public static void ApplyToOpenScene()
    {
        Scene scene = SceneManager.GetActiveScene();
        int applied = ApplyAll(scene);
        if (applied > 0) EditorSceneManager.SaveScene(scene);
        Debug.Log($"[Movement] - {applied} patch(es) aplicados em '{scene.name}'");
    }

    public static IEnumerable<LevelPatch> PatchesFor(string sceneName)
    {
        foreach (string guid in AssetDatabase.FindAssets("t:LevelPatch"))
        {
            var patch = AssetDatabase.LoadAssetAtPath<LevelPatch>(AssetDatabase.GUIDToAssetPath(guid));
            if (patch != null && patch.SceneName == sceneName) yield return patch;
        }
    }

    public static int ApplyAll(Scene scene)
    {
        int count = 0;
        foreach (LevelPatch patch in PatchesFor(scene.name).OrderBy(p => p.name))
        {
            if (Apply(patch, scene)) count++;
        }

        return count;
    }

    public static bool Apply(LevelPatch patch, Scene scene)
    {
        List<Tilemap> tilemaps = SolidTilemaps(scene);
        if (tilemaps.Count == 0)
        {
            Debug.LogError($"[Movement] - '{scene.name}' sem Tilemap sólido (layer 6 + TilemapCollider2D)");
            return false;
        }

        foreach (Tilemap tilemap in tilemaps)
        {
            TileBase tile = FillTileFor(tilemap);
            foreach (LevelPatch.CellRect r in patch.Rects)
            {
                for (int x = r.Min.x; x <= r.Max.x; x++)
                {
                    for (int y = r.Min.y; y <= r.Max.y; y++)
                    {
                        var cell = new Vector3Int(x, y, 0);
                        TileBase wanted = r.Operation == LevelPatch.Operation.Fill ? tile : null;
                        if (tilemap.GetTile(cell) != wanted) tilemap.SetTile(cell, wanted);
                    }
                }
            }

            // O TilemapCollider2D só processa os tiles novos no próximo frame; sem isto o composite regenera (e salva)
            // a geometria antiga, e o Physics2D não vê as células do patch.
            var tilemapCollider = tilemap.GetComponent<TilemapCollider2D>();
            if (tilemapCollider != null) tilemapCollider.ProcessTilemapChanges();
            var composite = tilemap.GetComponent<CompositeCollider2D>();
            if (composite != null) composite.GenerateGeometry();
            RecordChange(tilemap);
            RecordChange(composite);
        }

        EditorSceneManager.MarkSceneDirty(scene);
        return true;
    }

    /// <summary>
    /// O Tilemap de colisão da fase (referência de coordenadas das células): o Tilemap sólido com mais células com
    /// collider. Todos os Tilemaps sólidos das fases dividem a mesma Grid, então as células coincidem.
    /// </summary>
    public static Tilemap FindCollisionTilemap(Scene scene)
    {
        Tilemap best = null;
        int bestCount = -1;
        foreach (Tilemap tm in SolidTilemaps(scene))
        {
            int n = ColliderCellCount(tm);
            if (n > bestCount)
            {
                best = tm;
                bestCount = n;
            }
        }

        return best;
    }

    /// <summary>Tilemaps com TilemapCollider2D na layer 6 (Ground).</summary>
    public static List<Tilemap> SolidTilemaps(Scene scene)
    {
        var result = new List<Tilemap>();
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (TilemapCollider2D tc in root.GetComponentsInChildren<TilemapCollider2D>(true))
            {
                if (tc.gameObject.layer != MovementLayers.Ground) continue;
                var tm = tc.GetComponent<Tilemap>();
                if (tm != null && !result.Contains(tm)) result.Add(tm);
            }
        }

        return result;
    }

    private static int ColliderCellCount(Tilemap tilemap)
    {
        int n = 0;
        foreach (Vector3Int c in tilemap.cellBounds.allPositionsWithin)
        {
            if (tilemap.HasTile(c) && tilemap.GetColliderType(c) != Tile.ColliderType.None) n++;
        }

        return n;
    }

    // O tile mais usado entre as células com collider; num Tilemap só visual, o mais usado de todos.
    private static TileBase FillTileFor(Tilemap tilemap)
    {
        var solid = new Dictionary<TileBase, int>();
        var any = new Dictionary<TileBase, int>();
        foreach (Vector3Int c in tilemap.cellBounds.allPositionsWithin)
        {
            TileBase t = tilemap.GetTile(c);
            if (t == null) continue;
            any.TryGetValue(t, out int a);
            any[t] = a + 1;
            if (tilemap.GetColliderType(c) == Tile.ColliderType.None) continue;
            solid.TryGetValue(t, out int s);
            solid[t] = s + 1;
        }

        Dictionary<TileBase, int> pool = solid.Count > 0 ? solid : any;
        TileBase best = null;
        int bestCount = -1;
        foreach (KeyValuePair<TileBase, int> kv in pool)
        {
            if (kv.Value > bestCount)
            {
                best = kv.Key;
                bestCount = kv.Value;
            }
        }

        return best;
    }

    private static void RecordChange(Object target)
    {
        if (target == null) return;
        EditorUtility.SetDirty(target);
        if (PrefabUtility.IsPartOfPrefabInstance(target)) PrefabUtility.RecordPrefabInstancePropertyModifications(target);
    }
}
