using System.Collections.Generic;
using Roguelike.Movement;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

/// <summary>
/// Edições de fase da D1 opção B (SPEC §14.3, M.16), escritas como dados reaplicáveis (LevelPatch). Cada retângulo está
/// em coordenadas de MUNDO inteiras (as do mapa ASCII do analisador: coluna x = [x; x+1), linha y = [y; y+1)) e é
/// convertido para células do Tilemap de colisão da própria cena (WorldToCell, com os offsets do colisor). Rode
/// "Tools/Movement/Setup/Create Level Patches" para (re)gerar os assets; aplique nas cenas com o LevelPatcher.
/// Cada edição tem um motivo (o trecho em que o kit base parava, pelo relatório do analisador).
/// </summary>
public static class MovementLevelPatches
{
    public const string Folder = "Assets/_Roguelike/Data/Movement";

    private struct Edit
    {
        public string Note;
        public int X0, Y0, X1, Y1;
        public LevelPatch.Operation Operation;

        public Edit(string note, int x0, int y0, int x1, int y1, LevelPatch.Operation op = LevelPatch.Operation.Fill)
        {
            Note = note;
            X0 = x0;
            Y0 = y0;
            X1 = x1;
            Y1 = y1;
            Operation = op;
        }
    }

    private static readonly Dictionary<string, Edit[]> Edits = new Dictionary<string, Edit[]>
    {
        {
            "QuintaFase", new[]
            {
                // O kit base parava no ressalto y = 25 (x −5..−3): o próximo apoio era o topo do pilar em y = 30 (+5 u).
                // Um degrau de 2 u sobre o ressalto deixa a subida em +3 (entalhe y = 24 → degrau y = 27) e +3 (→ pilar y = 30).
                new Edit("degrau entre o ressalto y=25 e o pilar y=30", -5, 25, -3, 26),
                // Do topo do pilar (y = 30, x −1..3) até a laje (y = 31, x 7..42) havia um vão de 4 u sob um beiral
                // (x 6..11, y 33..34): o pulo cheio batia na face do beiral e caía no vão. Uma ponte no nível da base da
                // laje fecha o vão e deixa um degrau de +1 sob o beiral (a cabeça bate no beiral já acima da laje).
                new Edit("ponte entre o pilar y=30 e a laje y=31", 3, 29, 6, 29),
                // Da plataforma y = 36 (x 27..34) à escada diagonal (y = 40, x 35..39) eram +4 u: acima do pulo (3,5).
                // Um bloco 2×2 na ponta direita divide em +2 e +2.
                new Edit("bloco 2x2 antes da escada diagonal y=40", 32, 36, 33, 37),
            }
        },
        {
            "QuartaFase 1", new[]
            {
                // Do ressalto do spawn (y = 40, x −30..−26) ao topo do beiral (y = 46, x −21..−13) eram +6 u sem apoio.
                // Dois blocos formam uma escada de +2, +2 e +2 (o bloco de cima encosta na face do beiral).
                new Edit("degrau 1 da escada até o beiral (topo y=42)", -25, 41, -24, 41),
                new Edit("degrau 2 da escada até o beiral (topo y=44)", -23, 42, -22, 43),
                // Da plataforma flutuante (y = 43, x 1..6) à torre (y = 39, x 16..26) eram 10 u com −4: o pulo cheio
                // descia em y = 39 perto de x = 14. A torre ganha uma aba de 4 u à esquerda.
                new Edit("aba à esquerda da torre y=39", 12, 38, 15, 38),
                // Soft-locks: quem cai na região de baixo (o caminho natural à direita do spawn desce para lá) não voltava
                // ao objetivo sem pulo duplo/parede. Rota de volta: sala (piso y = 11) → 2 apoios → escada diagonal
                // (degraus de +1 de y = 18 a 30) → 2 apoios → aba da torre.
                new Edit("apoio da sala 1 (topo y=14)", 28, 13, 29, 13),
                // Base em y = 16: pé-direito de 5 u sobre o piso (o pulo até o apoio 1 não bate a cabeça nele).
                new Edit("apoio da sala 2 (topo y=17), encostado na escada diagonal", 26, 16, 27, 16),
                new Edit("apoio 1 entre o topo da escada diagonal e a aba da torre (topo y=33)", 11, 32, 12, 32),
                new Edit("apoio 2 entre o topo da escada diagonal e a aba da torre (topo y=36)", 9, 35, 10, 35),
                // Bolsão sob a pilastra (piso y = 14, x −3..1): a coluna à direita (topo y = 19) ficava a +5.
                new Edit("degrau no bolsão sob a pilastra (topo y=17)", 0, 14, 1, 16),
            }
        },
    };

    [MenuItem("Tools/Movement/Setup/Create Level Patches")]
    public static void CreatePatchesMenu()
    {
        CreatePatches();
    }

    /// <summary>Cria/atualiza os LevelPatch_*.asset (abre cada cena só para converter coordenadas).</summary>
    public static void CreatePatches()
    {
        foreach (KeyValuePair<string, Edit[]> entry in Edits)
        {
            string scenePath = ScenePathFor(entry.Key);
            if (scenePath == null) continue;
            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            Tilemap tilemap = LevelPatcher.FindCollisionTilemap(scene);
            if (tilemap == null) continue;

            Vector2 offset = Vector2.zero;
            var tc = tilemap.GetComponent<TilemapCollider2D>();
            var composite = tilemap.GetComponent<CompositeCollider2D>();
            if (tc != null) offset += tc.offset;
            if (composite != null) offset += composite.offset;

            LevelPatch patch = MovementProjectSetup.LoadOrCreate<LevelPatch>($"{Folder}/LevelPatch_{entry.Key}.asset");
            patch.SceneName = entry.Key;
            patch.Rects = new List<LevelPatch.CellRect>();
            foreach (Edit e in entry.Value)
            {
                Vector3Int min = tilemap.WorldToCell(new Vector3(e.X0 + 0.5f - offset.x, e.Y0 + 0.5f - offset.y, 0f));
                Vector3Int max = tilemap.WorldToCell(new Vector3(e.X1 + 0.5f - offset.x, e.Y1 + 0.5f - offset.y, 0f));
                patch.Rects.Add(new LevelPatch.CellRect
                {
                    Note = e.Note,
                    Operation = e.Operation,
                    Min = new Vector2Int(Mathf.Min(min.x, max.x), Mathf.Min(min.y, max.y)),
                    Max = new Vector2Int(Mathf.Max(min.x, max.x), Mathf.Max(min.y, max.y)),
                });
            }

            EditorUtility.SetDirty(patch);
            Debug.Log($"[Movement] - LevelPatch_{entry.Key}: {patch.Rects.Count} edição(ões)");
        }

        AssetDatabase.SaveAssets();
    }

    /// <summary>
    /// Batch: cria os patches e roda o analisador nas 3 fases com eles como patches virtuais (sem salvar cenas), com os
    /// soft-locks do kit base.
    /// </summary>
    public static void CreateAndAnalyzeBatch()
    {
        CreatePatches();
        foreach (string path in ReachabilityWindow.PhaseScenes) Debug.Log("[Movement] - " + ReachabilityWindow.AnalyzePath(path, true, true, true));
        EditorApplication.Exit(0);
    }

    /// <summary>Batch: cria os patches e aplica nas cenas (salvando).</summary>
    public static void CreateAndApplyBatch()
    {
        CreatePatches();
        foreach (string sceneName in Edits.Keys)
        {
            string path = ScenePathFor(sceneName);
            if (path == null) continue;
            Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            if (LevelPatcher.ApplyAll(scene) > 0) EditorSceneManager.SaveScene(scene);
        }

        EditorApplication.Exit(0);
    }

    private static string ScenePathFor(string sceneName)
    {
        foreach (string path in ReachabilityWindow.PhaseScenes)
        {
            if (System.IO.Path.GetFileNameWithoutExtension(path) == sceneName) return path;
        }

        return null;
    }
}
