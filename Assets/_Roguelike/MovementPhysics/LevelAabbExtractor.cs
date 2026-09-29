using System.Collections.Generic;
using Roguelike.Movement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

namespace Roguelike.MovementPhysics
{
    /// <summary>Fase extraída para o mundo AABB (analisador de alcançabilidade e testes de fase).</summary>
    public sealed class ExtractedLevel
    {
        public readonly AabbCollisionWorld World = new AabbCollisionWorld();
        public readonly List<Rect> Goals = new List<Rect>();
        public Vector2 Spawn;
        public bool HasSpawn;
        public Rect Bounds;

        /// <summary>Células sólidas (layer 6) do Tilemap de colisão, em coordenadas de mundo do canto inferior esquerdo.</summary>
        public readonly List<Vector2> GroundCells = new List<Vector2>();
    }

    /// <summary>
    /// Extrai a geometria de colisão de uma cena para um <see cref="AabbCollisionWorld"/> (SPEC §14.2): células dos
    /// Tilemaps com TilemapCollider2D (com o offset do CompositeCollider2D), BoxCollider2D sólidos nas layers 0/6/10,
    /// triggers de KillZone (layer 7), alvos do gancho (layer 8), o objetivo (IPlayerTickTrigger) e o SpawnPoint.
    /// Linhas de células vizinhas viram uma caixa só (menos caixas, mesma geometria).
    /// </summary>
    public static class LevelAabbExtractor
    {
        public static ExtractedLevel Extract(Scene scene, string spawnTag = "SpawnPoint")
        {
            var level = new ExtractedLevel();
            bool hasBounds = false;
            Rect bounds = default;

            void Grow(Vector2 min, Vector2 max)
            {
                var r = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
                if (!hasBounds) { bounds = r; hasBounds = true; }
                else bounds = Rect.MinMaxRect(Mathf.Min(bounds.xMin, r.xMin), Mathf.Min(bounds.yMin, r.yMin),
                    Mathf.Max(bounds.xMax, r.xMax), Mathf.Max(bounds.yMax, r.yMax));
            }

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (TilemapCollider2D tc in root.GetComponentsInChildren<TilemapCollider2D>(true))
                {
                    if (!tc.enabled || !tc.gameObject.activeInHierarchy || tc.isTrigger) continue;
                    AabbKind? kind = KindFor(tc.gameObject.layer);
                    if (kind == null) continue;

                    var tilemap = tc.GetComponent<Tilemap>();
                    if (tilemap == null) continue;
                    Vector2 offset = tc.offset;
                    var composite = tc.GetComponent<CompositeCollider2D>();
                    if (composite != null && tc.compositeOperation != Collider2D.CompositeOperation.None) offset += composite.offset;

                    BoundsInt cells = tilemap.cellBounds;
                    Vector3 cellSize = tilemap.layoutGrid != null ? tilemap.layoutGrid.cellSize : Vector3.one;
                    for (int y = cells.yMin; y < cells.yMax; y++)
                    {
                        int runStart = int.MinValue;
                        for (int x = cells.xMin; x <= cells.xMax; x++)
                        {
                            bool solid = x < cells.xMax && IsSolidCell(tilemap, new Vector3Int(x, y, 0));
                            if (solid && runStart == int.MinValue) runStart = x;
                            if (solid || runStart == int.MinValue) continue;

                            Vector3 a = tilemap.CellToWorld(new Vector3Int(runStart, y, 0));
                            Vector3 b = tilemap.CellToWorld(new Vector3Int(x - 1, y, 0)) + new Vector3(cellSize.x * Mathf.Abs(tc.transform.lossyScale.x), cellSize.y * Mathf.Abs(tc.transform.lossyScale.y), 0f);
                            Vector2 min = (Vector2)a + offset;
                            Vector2 max = (Vector2)b + offset;
                            level.World.AddBox(min, max, kind.Value);
                            Grow(min, max);
                            if (kind == AabbKind.Ground)
                            {
                                for (int cx = runStart; cx < x; cx++) level.GroundCells.Add((Vector2)tilemap.CellToWorld(new Vector3Int(cx, y, 0)) + offset);
                            }

                            runStart = int.MinValue;
                        }
                    }
                }

                foreach (BoxCollider2D box in root.GetComponentsInChildren<BoxCollider2D>(true))
                {
                    if (!box.enabled || !box.gameObject.activeInHierarchy) continue;
                    if (box.CompareTag("Player") || box.gameObject.layer == MovementLayers.Player) continue;
                    Bounds b = WorldBounds(box);

                    if (box.GetComponentInParent<IPlayerTickTrigger>() != null && box.isTrigger)
                    {
                        level.Goals.Add(Rect.MinMaxRect(b.min.x, b.min.y, b.max.x, b.max.y));
                        continue;
                    }

                    if (box.isTrigger)
                    {
                        if (box.gameObject.layer == MovementLayers.KillZone) level.World.AddBox(b.min, b.max, AabbKind.KillZone);
                        continue;
                    }

                    AabbKind? kind = KindFor(box.gameObject.layer);
                    if (kind == null) continue;
                    level.World.AddBox(b.min, b.max, kind.Value);
                    Grow(b.min, b.max);
                }

                foreach (Collider2D c in root.GetComponentsInChildren<Collider2D>(true))
                {
                    if (!c.enabled || !c.gameObject.activeInHierarchy) continue;
                    if (c.gameObject.layer == MovementLayers.GrappleTarget) level.World.AddGrapplePoint(c.bounds.center);
                    else if (c.isTrigger && !(c is BoxCollider2D) && c.GetComponentInParent<IPlayerTickTrigger>() != null)
                    {
                        Bounds b = c.bounds;
                        level.Goals.Add(Rect.MinMaxRect(b.min.x, b.min.y, b.max.x, b.max.y));
                    }
                }

                foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                {
                    if (!level.HasSpawn && t.CompareTag(spawnTag))
                    {
                        level.Spawn = t.position;
                        level.HasSpawn = true;
                    }
                }
            }

            level.Bounds = bounds;
            return level;
        }

        private static AabbKind? KindFor(int layer)
        {
            if (layer == MovementLayers.Ground) return AabbKind.Ground;
            if (layer == MovementLayers.Default) return AabbKind.Boundary;
            if (layer == MovementLayers.Enemy) return AabbKind.Enemy;
            return null;
        }

        private static bool IsSolidCell(Tilemap tilemap, Vector3Int cell)
        {
            if (!tilemap.HasTile(cell)) return false;
            return tilemap.GetColliderType(cell) != Tile.ColliderType.None;
        }

        // Bounds do BoxCollider2D sem depender do Physics2D (funciona com a física desligada no Editor).
        private static Bounds WorldBounds(BoxCollider2D box)
        {
            Transform t = box.transform;
            Vector3 center = t.TransformPoint(box.offset);
            Vector3 scale = t.lossyScale;
            var size = new Vector3(Mathf.Abs(box.size.x * scale.x), Mathf.Abs(box.size.y * scale.y), 0f);
            return new Bounds(center, size);
        }
    }
}
