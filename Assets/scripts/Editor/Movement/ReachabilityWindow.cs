using System.Collections.Generic;
using System.IO;
using System.Text;
using Roguelike.Movement;
using Roguelike.MovementPhysics;
using Roguelike.Upgrades;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

/// <summary>
/// Analisador de alcançabilidade (SPEC §14.2, M.12): extrai a fase para AABBs, roda o PlayerMotor com o perfil novo
/// para 4 kits (base, +aéreo, +parede, +ambos) e grava Temp/Reachability_&lt;Cena&gt;.md com ✅/❌ por kit, a fronteira
/// e os trechos que bloqueiam. Opcionalmente aplica os LevelPatch da cena como patches virtuais (valida a edição antes
/// de mexer no YAML). Menu: Tools/Movement/Reachability. Batch: ReachabilityWindow.AnalyzePhasesBatch.
/// </summary>
public sealed class ReachabilityWindow : EditorWindow
{
    public static readonly string[] PhaseScenes =
    {
        "Assets/Scenes/PrimeiraFase.unity",
        "Assets/Scenes/QuartaFase 1.unity",
        "Assets/Scenes/QuintaFase.unity",
    };

    private bool virtualPatches = true;
    private bool allKits = true;
    private bool softLocks;
    private string lastSummary = string.Empty;
    private Vector2 scroll;

    [MenuItem("Tools/Movement/Reachability")]
    public static void Open()
    {
        GetWindow<ReachabilityWindow>("Alcançabilidade");
    }

    private void OnGUI()
    {
        virtualPatches = EditorGUILayout.Toggle("Patches virtuais", virtualPatches);
        allKits = EditorGUILayout.Toggle("4 kits (senão só o base)", allKits);
        softLocks = EditorGUILayout.Toggle("Soft-locks do kit base (lento)", softLocks);
        if (GUILayout.Button("Analisar a cena aberta"))
        {
            lastSummary = Analyze(SceneManager.GetActiveScene(), virtualPatches, allKits, softLocks);
        }

        if (GUILayout.Button("Analisar as 3 fases"))
        {
            var sb = new StringBuilder();
            foreach (string path in PhaseScenes) sb.AppendLine(AnalyzePath(path, virtualPatches, allKits, softLocks));
            lastSummary = sb.ToString();
        }

        scroll = EditorGUILayout.BeginScrollView(scroll);
        EditorGUILayout.TextArea(lastSummary, GUILayout.ExpandHeight(true));
        EditorGUILayout.EndScrollView();
    }

    /// <summary>Batch: analisa as 3 fases (4 kits, patches virtuais, soft-locks do kit base) e sai.</summary>
    public static void AnalyzePhasesBatch()
    {
        foreach (string path in PhaseScenes) Debug.Log("[Movement] - " + AnalyzePath(path, true, true, true));
        EditorApplication.Exit(0);
    }

    public static string AnalyzePath(string scenePath, bool virtualPatches, bool allKits, bool softLocks = false)
    {
        Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        return Analyze(scene, virtualPatches, allKits, softLocks);
    }

    public static List<ReachabilityReport> AnalyzeReports(Scene scene, bool virtualPatches, bool allKits, out ReachabilityModel model,
        out ExtractedLevel level)
    {
        return AnalyzeReports(scene, virtualPatches, allKits, out model, out level, out _, out _);
    }

    public static List<ReachabilityReport> AnalyzeReports(Scene scene, bool virtualPatches, bool allKits, out ReachabilityModel model,
        out ExtractedLevel level, out MovementStats baseStats, out int firstPatchBox)
    {
        var profile = AssetDatabase.LoadAssetAtPath<MovementProfile>(MovementProjectSetup.ProfilePath);
        if (profile == null) profile = CreateInstance<MovementProfile>();

        level = LevelAabbExtractor.Extract(scene);
        firstPatchBox = level.World.BoxCount;
        if (virtualPatches) ApplyPatchesVirtually(scene, level.World);

        float killPlane = level.Bounds.yMin - 4f;
        var boundary = FindAnyObjectByType<CameraBoundary>();
        if (boundary != null) killPlane = Mathf.Max(killPlane, boundary.KillPlaneY);

        model = new ReachabilityModel(profile, level.World, killPlane);
        var kits = new List<(string name, KitStatInput input)>
        {
            ("base", new KitStatInput(profile, AbilityFlags.None, 0, 0)),
        };
        if (allKits)
        {
            kits.Add(("+aéreo", new KitStatInput(profile, AbilityFlags.None, 1, 0)));
            kits.Add(("+parede", new KitStatInput(profile, AbilityFlags.WallJump, 0, 0)));
            kits.Add(("+ambos", new KitStatInput(profile, AbilityFlags.WallJump, 1, 0)));
        }

        var reports = new List<ReachabilityReport>();
        baseStats = MovementStatsResolver.Resolve(profile, kits[0].input);
        Vector2 spawn = level.HasSpawn ? level.Spawn : Vector2.zero;
        foreach ((string name, KitStatInput input) in kits)
        {
            reports.Add(model.Run(MovementStatsResolver.Resolve(profile, input), spawn, level.Goals, name));
        }

        return reports;
    }

    public static string Analyze(Scene scene, bool virtualPatches, bool allKits, bool softLocks = false)
    {
        List<ReachabilityReport> reports = AnalyzeReports(scene, virtualPatches, allKits, out ReachabilityModel model, out ExtractedLevel level,
            out MovementStats baseStats, out int firstPatchBox);
        string markdown = model.ToMarkdown(scene.name, reports, level.Goals);
        int softLockCount = -1;
        if (softLocks)
        {
            List<int> locked = model.FindSoftLocks(baseStats, level.HasSpawn ? level.Spawn : Vector2.zero, level.Goals, out int reachable);
            softLockCount = locked.Count;
            markdown += model.SoftLocksToMarkdown(reports[0].KitName, locked, reachable);
        }

        string dir = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Temp");
        Directory.CreateDirectory(dir);
        string file = Path.Combine(dir, $"Reachability_{scene.name}.md");
        File.WriteAllText(file, markdown);

        // Em batchmode a Unity apaga o Temp ao sair: -reachabilityOut <pasta> guarda uma cópia (com o mapa ASCII).
        string[] args = System.Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i] != "-reachabilityOut") continue;
            Directory.CreateDirectory(args[i + 1]);
            File.WriteAllText(Path.Combine(args[i + 1], $"Reachability_{scene.name}.md"), markdown);
            File.WriteAllText(Path.Combine(args[i + 1], $"Map_{scene.name}.txt"), CellMappingLine(scene) + "\n" + AsciiMap(level, model, reports[0], firstPatchBox));
            var tsv = new StringBuilder("tipo\tminX\tminY\tmaxX\tmaxY\talcançada(kit base)\n");
            for (int b = 0; b < level.World.BoxCount; b++)
            {
                AabbCollisionWorld.Box box = level.World.GetBox(b);
                tsv.Append(box.Kind).Append('\t').Append(box.Min.x).Append('\t').Append(box.Min.y).Append('\t')
                    .Append(box.Max.x).Append('\t').Append(box.Max.y).Append("\t\n");
            }

            var reachedBase = new HashSet<int>(reports[0].ReachedSurfaces);
            for (int s = 0; s < model.Surfaces.Count; s++)
            {
                Surface surface = model.Surfaces[s];
                tsv.Append("Surface\t").Append(surface.MinX).Append('\t').Append(surface.Y).Append('\t')
                    .Append(surface.MaxX).Append('\t').Append(surface.Y).Append('\t').Append(reachedBase.Contains(s) ? "sim" : "não").Append('\n');
            }

            File.WriteAllText(Path.Combine(args[i + 1], $"Geometry_{scene.name}.tsv"), tsv.ToString().Replace(',', '.'));
        }

        var sb = new StringBuilder();
        sb.Append(scene.name).Append(": ");
        foreach (ReachabilityReport r in reports) sb.Append(r.KitName).Append(r.GoalReached ? " ✅  " : " ❌  ");
        if (softLockCount >= 0) sb.Append("soft-locks: ").Append(softLockCount).Append("  ");
        sb.Append("→ ").Append(file);
        return sb.ToString();
    }

    /// <summary>
    /// Mapa ASCII da fase (1 caractere = 1 u; linha de cima = y maior) para desenhar LevelPatches. A coluna x e a linha y
    /// mostram a célula de mundo [x; x+1) × [y; y+1), marcada quando o centro dela está dentro da caixa; superfícies ficam
    /// na linha da altura dos pés. '#' sólido · '+' sólido vindo de patch · '|' parede invisível · 'E' inimigo ·
    /// 'K' KillZone · '=' superfície alcançada pelo kit · '-' superfície não alcançada · 'S' spawn · 'G' objetivo ·
    /// 'o' alvo do gancho.
    /// </summary>
    public static string AsciiMap(ExtractedLevel level, ReachabilityModel model, ReachabilityReport report, int firstPatchBox = int.MaxValue)
    {
        int x0 = Mathf.FloorToInt(level.Bounds.xMin) - 1;
        int x1 = Mathf.CeilToInt(level.Bounds.xMax) + 1;
        int y0 = Mathf.FloorToInt(level.Bounds.yMin) - 1;
        int y1 = Mathf.CeilToInt(level.Bounds.yMax) + 2;
        int w = x1 - x0;
        int h = y1 - y0;
        var grid = new char[h, w];
        for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) grid[y, x] = ' ';

        void Put(float wx, float wy, char c)
        {
            int cx = Mathf.FloorToInt(wx) - x0;
            int cy = Mathf.FloorToInt(wy) - y0;
            if (cx >= 0 && cx < w && cy >= 0 && cy < h) grid[cy, cx] = c;
        }

        for (int i = 0; i < level.World.BoxCount; i++)
        {
            AabbCollisionWorld.Box b = level.World.GetBox(i);
            char c = b.Kind == AabbKind.Ground ? (i >= firstPatchBox ? '+' : '#') : b.Kind == AabbKind.Boundary ? '|' : b.Kind == AabbKind.Enemy ? 'E' : 'K';
            // Centros de célula (k + 0,5) dentro da caixa: sem o deslocamento de uma coluna quando a grade tem offset (−0,002).
            for (float y = Mathf.Ceil(b.Min.y - 0.5f) + 0.5f; y < b.Max.y; y += 1f)
            {
                for (float x = Mathf.Ceil(b.Min.x - 0.5f) + 0.5f; x < b.Max.x; x += 1f) Put(x, y, c);
            }
        }

        var reached = new HashSet<int>(report.ReachedSurfaces);
        for (int i = 0; i < model.Surfaces.Count; i++)
        {
            Surface s = model.Surfaces[i];
            for (float x = Mathf.Floor(s.MinX) + 0.5f; x <= s.MaxX; x += 1f) Put(x, s.Y + 0.5f, reached.Contains(i) ? '=' : '-');
        }

        foreach (Vector2 p in level.World.GrapplePoints) Put(p.x, p.y, 'o');
        foreach (Rect g in level.Goals) Put(g.center.x, g.center.y, 'G');
        if (level.HasSpawn) Put(level.Spawn.x, level.Spawn.y + 0.5f, 'S');

        var sb = new StringBuilder();
        sb.Append("      ");
        for (int x = 0; x < w; x++) sb.Append((x0 + x) % 5 == 0 ? '|' : ' ');
        sb.AppendLine();
        sb.Append("      ");
        for (int x = 0; x < w; x++)
        {
            int wx = x0 + x;
            if (wx % 10 == 0)
            {
                string label = wx.ToString();
                sb.Append(label);
                x += label.Length - 1;
            }
            else sb.Append(' ');
        }

        sb.AppendLine();
        for (int y = h - 1; y >= 0; y--)
        {
            sb.Append((y0 + y).ToString().PadLeft(4)).Append("  ");
            for (int x = 0; x < w; x++) sb.Append(grid[y, x]);
            sb.AppendLine();
        }

        return sb.ToString();
    }

    /// <summary>"mundo = célula + (ox; oy)" do Tilemap de colisão (para escrever LevelPatches em células).</summary>
    public static string CellMappingLine(Scene scene)
    {
        Tilemap tilemap = LevelPatcher.FindCollisionTilemap(scene);
        if (tilemap == null) return "sem Tilemap de colisão";
        Vector2 offset = Vector2.zero;
        var tc = tilemap.GetComponent<TilemapCollider2D>();
        var composite = tilemap.GetComponent<CompositeCollider2D>();
        if (tc != null) offset += tc.offset;
        if (composite != null) offset += composite.offset;
        Vector2 origin = (Vector2)tilemap.CellToWorld(Vector3Int.zero) + offset;
        return $"Tilemap '{tilemap.name}': mundo(canto inferior esquerdo) = célula + ({origin.x:0.###}; {origin.y:0.###}); tile = {AssetDatabase.GetAssetPath(tilemap.GetTile(FirstCell(tilemap)))}";
    }

    private static Vector3Int FirstCell(Tilemap tilemap)
    {
        foreach (Vector3Int c in tilemap.cellBounds.allPositionsWithin)
        {
            if (tilemap.HasTile(c)) return c;
        }

        return Vector3Int.zero;
    }

    private static void ApplyPatchesVirtually(Scene scene, AabbCollisionWorld world)
    {
        Tilemap tilemap = LevelPatcher.FindCollisionTilemap(scene);
        if (tilemap == null) return;
        Vector2 offset = Vector2.zero;
        var tc = tilemap.GetComponent<TilemapCollider2D>();
        var composite = tilemap.GetComponent<CompositeCollider2D>();
        if (tc != null) offset += tc.offset;
        if (composite != null) offset += composite.offset;
        Vector3 cellSize = tilemap.layoutGrid != null ? tilemap.layoutGrid.cellSize : Vector3.one;

        foreach (LevelPatch patch in LevelPatcher.PatchesFor(scene.name))
        {
            patch.ApplyVirtual(world, c => (Vector2)tilemap.CellToWorld(new Vector3Int(c.x, c.y, 0)) + offset, cellSize);
        }
    }
}
