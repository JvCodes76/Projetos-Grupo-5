using Roguelike.Movement;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.Tilemaps;

/// <summary>
/// Gera Assets/Scenes/Dev/MovementGym.unity (SPEC §14.2) com o mesmo pipeline de colisão das fases (Tilemap +
/// TilemapCollider2D + CompositeCollider2D Outlines + o tile de colisão real), para reproduzir as emendas. Contém:
/// chão plano com marcas a cada 1 u; degraus de 1/2/3/4 u; vãos de 3/5/7/9 u; teto a 4,8 u; teto baixo (RF-36);
/// quinas de teto a 0,2 e 0,3 u (RF-35); chaminé de 3 × 20 u (RF-18); placa de 0,15 u (RF-39); parede invisível na
/// layer Default (RF-22); moeda (trigger) sob um teto (RF-22); 2 drones de gancho a 9 u (RF-32); poço com KillZone
/// (RF-42); queda de 20 u (RF-48). Rótulos com as medidas. O DevPlayBootstrap dá o "Play direto" com o kit completo.
/// </summary>
public static class MovementGymBuilder
{
    public const string ScenePath = "Assets/Scenes/Dev/MovementGym.unity";
    private const string TilePath = "Assets/Sprites/tileset/5. Industrial Tileset - Starter Pack 32p/1_Industrial_Tileset_1B_1.asset";
    private const string DronePath = "Assets/Prefabs/DroneHook.prefab";
    private const string SpawnPointPath = "Assets/Prefabs/SpawnPoint.prefab";

    private static Tilemap tilemap;
    private static TileBase tile;
    private static Sprite tileSprite;
    private static Font font;

    [MenuItem("Tools/Movement/Build Movement Gym")]
    public static void Build()
    {
        MovementProjectSetup.EnsureFolder("Assets/Scenes/Dev");
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        tile = AssetDatabase.LoadAssetAtPath<TileBase>(TilePath);
        tileSprite = (tile as Tile) != null ? ((Tile)tile).sprite : null;
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        var grid = new GameObject("Grid", typeof(Grid));
        var tmGo = new GameObject("Collision", typeof(Tilemap), typeof(TilemapRenderer));
        tmGo.transform.SetParent(grid.transform, false);
        tmGo.layer = 6;
        tilemap = tmGo.GetComponent<Tilemap>();

        // Chão plano (topo em y = 0) com marcas a cada 1 u e rótulo a cada 5 u.
        Fill(-12, -3, 60, 0);
        Fill(-14, -3, -12, 30);
        for (int x = -10; x <= 58; x++)
        {
            Marker(new Vector2(x, 0.12f), new Vector2(0.03f, x % 5 == 0 ? 0.35f : 0.18f));
            if (x % 5 == 0) Label($"{x} u", new Vector2(x, -0.6f), 0.08f);
        }

        // Degraus de 1/2/3/4 u (subida máxima do kit base: 3 u).
        Fill(8, 0, 10, 1); Label("degrau 1 u", new Vector2(9f, 1.6f));
        Fill(13, 0, 15, 2); Label("degrau 2 u", new Vector2(14f, 2.6f));
        Fill(18, 0, 20, 3); Label("degrau 3 u", new Vector2(19f, 3.6f));
        Fill(23, 0, 25, 4); Label("degrau 4 u (só com habilidade)", new Vector2(24f, 4.6f));

        // Quinas de teto: parado na marca e pulando reto, a cabeça sobrepõe a quina por 0,2 u (corrige) ou 0,3 u (bate).
        const float halfWidth = 0.255f;
        Box("Quina 0,2", new Vector2(2f + halfWidth - 0.2f, 2.2f), new Vector2(5f, 3f), 6);
        Marker(new Vector2(2f, 0.2f), new Vector2(0.1f, 0.4f));
        Label("pule parado aqui: quina 0,2 u (corrige)", new Vector2(2f, 3.5f));
        Box("Quina 0,3", new Vector2(-4f, 2.2f), new Vector2(-2f - halfWidth + 0.3f, 3f), 6);
        Marker(new Vector2(-2f, 0.2f), new Vector2(0.1f, 0.4f));
        Label("pule parado aqui: quina 0,3 u (bate)", new Vector2(-3f, 3.5f));

        // Chaminé de 3 u × 20 u (Salto de Parede).
        Fill(29, 0, 30, 20);
        Fill(33, 0, 34, 20);
        Label("chaminé 3 × 20 u (Salto de Parede)", new Vector2(31.5f, 20.8f));

        // Teto a 4,8 u (pé-direito mínimo do pulo máximo) com uma moeda (trigger) embaixo (RF-22: não corta o pulo).
        Box("Teto 4,8 u", new Vector2(37f, 4.8f), new Vector2(45f, 5.8f), 6);
        Label("teto a 4,8 u", new Vector2(41f, 6.3f));
        Trigger("Moeda (trigger)", new Vector2(41f, 3.2f), new Vector2(0.4f, 0.4f), 0, "Coin");
        Label("moeda (trigger) não corta o pulo", new Vector2(41f, 2.4f), 0.07f);

        // Teto baixo (RF-36): 0,3 u acima da cabeça.
        Box("Teto baixo", new Vector2(47f, 1.56f), new Vector2(51f, 2.56f), 6);
        Label("teto baixo (tolerância de teto)", new Vector2(49f, 3f));

        // Parede invisível na layer Default (RF-22, Q6): bloqueia, sem wall jump.
        var invisible = new GameObject("Parede invisível (Default)");
        invisible.layer = 0;
        invisible.transform.position = new Vector3(55.25f, 3f, 0f);
        invisible.AddComponent<BoxCollider2D>().size = new Vector2(0.5f, 6f);
        Label("parede invisível (Default): sem wall jump", new Vector2(55f, 6.6f));

        // Vãos de 3/5/7/9 u (kit base: ≤ 5 u no mesmo nível).
        Fill(63, -3, 68, 0);
        Fill(73, -3, 78, 0);
        Fill(85, -3, 90, 0);
        Fill(99, -3, 130, 0);
        Label("vão 3 u", new Vector2(61.5f, 1.5f));
        Label("vão 5 u", new Vector2(70.5f, 1.5f));
        Label("vão 7 u", new Vector2(81.5f, 1.5f));
        Label("vão 9 u (poço com KillZone)", new Vector2(94.5f, 1.5f));
        Trigger("KillZone", new Vector2(94.5f, -3.5f), new Vector2(9f, 1f), 7, null);

        // Placa de 0,15 u (RF-39: 27 u/s não atravessa).
        Box("Placa 0,15 u", new Vector2(103f, 3f), new Vector2(106f, 3.15f), 6);
        Label("placa 0,15 u", new Vector2(104.5f, 3.6f));

        // 2 drones de gancho a 9 u do chão.
        var drone = AssetDatabase.LoadAssetAtPath<GameObject>(DronePath);
        if (drone != null)
        {
            ((GameObject)PrefabUtility.InstantiatePrefab(drone)).transform.position = new Vector3(112f, 9f, 0f);
            ((GameObject)PrefabUtility.InstantiatePrefab(drone)).transform.position = new Vector3(121f, 9f, 0f);
        }

        Label("drones do gancho a 9 u", new Vector2(116.5f, 10.5f));

        // Queda de 20 u (RF-48): escada de 2 em 2 u até uma borda a 20 u.
        for (int i = 0; i < 10; i++) Fill(130 + 2 * i, 0, 132 + 2 * i, 2 * (i + 1));
        Fill(150, 0, 152, 20);
        Fill(152, -3, 175, 0);
        Fill(175, -3, 177, 40);
        Label("borda a 20 u: queda (câmera)", new Vector2(151f, 21f));

        var collider = tmGo.AddComponent<TilemapCollider2D>();
        collider.compositeOperation = Collider2D.CompositeOperation.Merge;
        var body = tmGo.AddComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Static;
        var composite = tmGo.AddComponent<CompositeCollider2D>();
        composite.geometryType = CompositeCollider2D.GeometryType.Outlines;
        composite.GenerateGeometry();

        // Spawn, limites da câmera, luz global e bootstrap.
        var spawnPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SpawnPointPath);
        GameObject spawn = spawnPrefab != null ? (GameObject)PrefabUtility.InstantiatePrefab(spawnPrefab) : new GameObject("SpawnPoint");
        spawn.transform.position = new Vector3(0f, 0.05f, 0f);
        if (spawnPrefab == null) spawn.tag = "SpawnPoint";

        var boundary = new GameObject("CameraBoundary").AddComponent<CameraBoundary>();
        boundary.minX = -4f;
        boundary.maxX = 168f;
        boundary.minY = 3f;
        boundary.maxY = 40f;
        boundary.useKillPlane = true;
        boundary.killPlaneY = -8f;

        var light = new GameObject("Global Light 2D").AddComponent<Light2D>();
        light.lightType = Light2D.LightType.Global;
        light.intensity = 1f;

        var bootstrapPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(MovementProjectSetup.BootstrapPrefabPath);
        if (bootstrapPrefab != null)
        {
            var bootstrap = (GameObject)PrefabUtility.InstantiatePrefab(bootstrapPrefab);
            MovementProjectSetup.SetRef(bootstrap.GetComponent<DevPlayBootstrap>(), "kit",
                AssetDatabase.LoadAssetAtPath<MovementKit>(MovementProjectSetup.KitAllPath));
        }

        Label("GYM DE MOVIMENTO · F1 overlay (kit) · F5/F6 gravar/reproduzir · segurar R = voltar ao spawn", new Vector2(0f, 7f), 0.1f);

        EditorSceneManager.SaveScene(scene, ScenePath);
        Debug.Log($"[Movement] - Gym gerado em {ScenePath}");
        tilemap = null;
    }

    private static void Fill(int x0, int y0, int x1, int y1)
    {
        for (int x = x0; x < x1; x++)
        {
            for (int y = y0; y < y1; y++) tilemap.SetTile(new Vector3Int(x, y, 0), tile);
        }
    }

    private static void Box(string name, Vector2 min, Vector2 max, int layer)
    {
        var go = new GameObject(name);
        go.layer = layer;
        Vector2 size = max - min;
        go.transform.position = (min + max) * 0.5f;
        go.AddComponent<BoxCollider2D>().size = size;
        if (tileSprite != null)
        {
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = tileSprite;
            sr.drawMode = SpriteDrawMode.Tiled;
            sr.size = size;
        }
    }

    private static void Trigger(string name, Vector2 center, Vector2 size, int layer, string tag)
    {
        var go = new GameObject(name);
        go.layer = layer;
        if (!string.IsNullOrEmpty(tag)) go.tag = tag;
        go.transform.position = center;
        var box = go.AddComponent<BoxCollider2D>();
        box.size = size;
        box.isTrigger = true;
    }

    private static void Marker(Vector2 position, Vector2 size)
    {
        if (tileSprite == null) return;
        var go = new GameObject("marca");
        go.transform.position = position;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = tileSprite;
        sr.drawMode = SpriteDrawMode.Sliced;
        sr.size = size;
        sr.color = new Color(1f, 1f, 0.4f, 0.8f);
        sr.sortingOrder = 5;
    }

    private static void Label(string text, Vector2 position, float size = 0.09f)
    {
        var go = new GameObject("Rótulo: " + text);
        go.transform.position = new Vector3(position.x, position.y, -1f);
        var mesh = go.AddComponent<TextMesh>();
        mesh.text = text;
        mesh.font = font;
        mesh.fontSize = 48;
        mesh.characterSize = size;
        mesh.anchor = TextAnchor.MiddleCenter;
        mesh.alignment = TextAlignment.Center;
        mesh.color = Color.white;
        var renderer = go.GetComponent<MeshRenderer>();
        if (font != null) renderer.sharedMaterial = font.material;
        renderer.sortingOrder = 20;
    }
}
