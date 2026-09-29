using System;
using System.Collections.Generic;
using UnityEngine;

namespace Roguelike.Movement
{
    /// <summary>Tipo de uma caixa do <see cref="AabbCollisionWorld"/> (equivale à layer do colisor real, SPEC §5.2).</summary>
    public enum AabbKind : byte
    {
        /// <summary>Layer 6: chão, parede, teto; vale para wall jump, chão seguro e linha de visão do gancho.</summary>
        Ground = 0,
        /// <summary>Layer 0 (paredes invisíveis): bloqueia e conta como teto, sem wall jump (RF-22, Q6).</summary>
        Boundary = 1,
        /// <summary>Layer 10: corpo de inimigo; sólido, sem wall jump (Q9); ignorado se já sobrepõe no início (DS-11).</summary>
        Enemy = 2,
        /// <summary>Layer 7: trigger de zona de morte.</summary>
        KillZone = 3,
    }

    /// <summary>
    /// Mundo de colisão analítico de caixas alinhadas aos eixos (SPEC §2.4): usado por todos os testes do núcleo,
    /// pelo analisador de alcançabilidade e pelo fuzz. Broadphase por grade uniforme de 4 u.
    /// Mesma semântica do adaptador da física 2D (asmdef Roguelike.Movement.Physics): faces que apenas se tocam não sobrepõem; um cast que começa
    /// sobrepondo um sólido acerta em distância 0, exceto inimigos (DS-11). Consultas não alocam.
    /// </summary>
    public sealed class AabbCollisionWorld : ICollisionWorld
    {
        /// <summary>Caixa do mundo (mín./máx. em unidades de mundo).</summary>
        public struct Box
        {
            public Vector2 Min;
            public Vector2 Max;
            public AabbKind Kind;

            public Vector2 Center => (Min + Max) * 0.5f;
            public Vector2 Size => Max - Min;
        }

        private const float CellSize = 4f;
        private const float DefaultGrappleTargetRadius = 0.05f;

        private readonly List<Box> boxes = new List<Box>();
        private readonly Dictionary<long, List<int>> grid = new Dictionary<long, List<int>>();
        private readonly List<Vector2> grapplePoints = new List<Vector2>();
        private int[] visitStamp = new int[64];
        private int stamp;

        /// <summary>Raio do colisor de cada alvo do gancho (o do DroneHook é 0,05 u).</summary>
        public float GrappleTargetRadius = DefaultGrappleTargetRadius;

        /// <summary>Consultas feitas desde o último <see cref="ResetQueryCount"/> (orçamento, RNF-09).</summary>
        public int QueryCount { get; private set; }

        public int BoxCount => boxes.Count;

        public IReadOnlyList<Vector2> GrapplePoints => grapplePoints;

        public Box GetBox(int index) => boxes[index];

        public void ResetQueryCount() => QueryCount = 0;

        /// <summary>Adiciona uma caixa por canto mínimo e máximo; devolve o índice.</summary>
        public int AddBox(Vector2 min, Vector2 max, AabbKind kind)
        {
            if (max.x <= min.x || max.y <= min.y) throw new ArgumentException("Caixa sem área.");

            int index = boxes.Count;
            boxes.Add(new Box { Min = min, Max = max, Kind = kind });
            if (visitStamp.Length < boxes.Count) Array.Resize(ref visitStamp, boxes.Count * 2);
            InsertInGrid(index);
            return index;
        }

        /// <summary>Adiciona uma caixa por centro e tamanho.</summary>
        public int AddBoxCentered(Vector2 center, Vector2 size, AabbKind kind)
        {
            Vector2 half = size * 0.5f;
            return AddBox(center - half, center + half, kind);
        }

        /// <summary>Move uma caixa existente (inimigo andando nos testes).</summary>
        public void SetBox(int index, Vector2 min, Vector2 max)
        {
            RemoveFromGrid(index);
            Box box = boxes[index];
            box.Min = min;
            box.Max = max;
            boxes[index] = box;
            InsertInGrid(index);
        }

        public void AddGrapplePoint(Vector2 point)
        {
            grapplePoints.Add(point);
        }

        public void Clear()
        {
            boxes.Clear();
            grid.Clear();
            grapplePoints.Clear();
        }

        public bool CastBox(Vector2 center, Vector2 size, Vector2 dir, float distance, QueryLayer layer, out BoxHit hit)
        {
            QueryCount++;
            hit = default;
            Vector2 half = size * 0.5f;
            float sweepX = dir.x * distance;
            float sweepY = dir.y * distance;
            Vector2 regionMin = new Vector2(center.x - half.x + Math.Min(0f, sweepX), center.y - half.y + Math.Min(0f, sweepY));
            Vector2 regionMax = new Vector2(center.x + half.x + Math.Max(0f, sweepX), center.y + half.y + Math.Max(0f, sweepY));

            float best = float.PositiveInfinity;
            Vector2 bestNormal = Vector2.zero;
            bool horizontal = dir.x != 0f;

            BeginVisit();
            ForEachCell(regionMin, regionMax, out int cx0, out int cy0, out int cx1, out int cy1);
            for (int cx = cx0; cx <= cx1; cx++)
            {
                for (int cy = cy0; cy <= cy1; cy++)
                {
                    if (!grid.TryGetValue(Key(cx, cy), out List<int> cell)) continue;
                    for (int k = 0; k < cell.Count; k++)
                    {
                        int i = cell[k];
                        if (!Visit(i)) continue;
                        Box b = boxes[i];
                        if (!Matches(b.Kind, layer)) continue;

                        bool overlapX = center.x + half.x > b.Min.x && center.x - half.x < b.Max.x;
                        bool overlapY = center.y + half.y > b.Min.y && center.y - half.y < b.Max.y;
                        if (overlapX && overlapY)
                        {
                            if (b.Kind == AabbKind.Enemy) continue; // DS-11
                            if (0f < best)
                            {
                                best = 0f;
                                bestNormal = -dir;
                            }
                            continue;
                        }

                        float gap;
                        if (horizontal)
                        {
                            if (!overlapY) continue;
                            gap = dir.x > 0f ? b.Min.x - (center.x + half.x) : (center.x - half.x) - b.Max.x;
                        }
                        else
                        {
                            if (!overlapX) continue;
                            gap = dir.y > 0f ? b.Min.y - (center.y + half.y) : (center.y - half.y) - b.Max.y;
                        }

                        if (gap < 0f || gap > distance || gap >= best) continue;
                        best = gap;
                        bestNormal = horizontal ? new Vector2(-Math.Sign(dir.x), 0f) : new Vector2(0f, -Math.Sign(dir.y));
                    }
                }
            }

            if (float.IsPositiveInfinity(best)) return false;
            hit = new BoxHit(best, bestNormal);
            return true;
        }

        public bool OverlapBox(Vector2 center, Vector2 size, QueryLayer layer)
        {
            QueryCount++;
            Vector2 half = size * 0.5f;
            Vector2 min = center - half;
            Vector2 max = center + half;

            BeginVisit();
            ForEachCell(min, max, out int cx0, out int cy0, out int cx1, out int cy1);
            for (int cx = cx0; cx <= cx1; cx++)
            {
                for (int cy = cy0; cy <= cy1; cy++)
                {
                    if (!grid.TryGetValue(Key(cx, cy), out List<int> cell)) continue;
                    for (int k = 0; k < cell.Count; k++)
                    {
                        int i = cell[k];
                        if (!Visit(i)) continue;
                        Box b = boxes[i];
                        if (!Matches(b.Kind, layer)) continue;
                        if (max.x > b.Min.x && min.x < b.Max.x && max.y > b.Min.y && min.y < b.Max.y) return true;
                    }
                }
            }

            return false;
        }

        public bool Raycast(Vector2 origin, Vector2 dir, float distance, QueryLayer layer, out BoxHit hit)
        {
            QueryCount++;
            hit = default;
            Vector2 end = origin + dir * distance;
            Vector2 regionMin = Vector2.Min(origin, end);
            Vector2 regionMax = Vector2.Max(origin, end);

            float best = float.PositiveInfinity;
            Vector2 bestNormal = Vector2.zero;

            BeginVisit();
            ForEachCell(regionMin, regionMax, out int cx0, out int cy0, out int cx1, out int cy1);
            for (int cx = cx0; cx <= cx1; cx++)
            {
                for (int cy = cy0; cy <= cy1; cy++)
                {
                    if (!grid.TryGetValue(Key(cx, cy), out List<int> cell)) continue;
                    for (int k = 0; k < cell.Count; k++)
                    {
                        int i = cell[k];
                        if (!Visit(i)) continue;
                        Box b = boxes[i];
                        if (!Matches(b.Kind, layer)) continue;
                        if (RaySlab(origin, dir, distance, b, out float t, out Vector2 normal) && t < best)
                        {
                            best = t;
                            bestNormal = normal;
                        }
                    }
                }
            }

            if (float.IsPositiveInfinity(best)) return false;
            hit = new BoxHit(best, bestNormal);
            return true;
        }

        public int FindGrappleTargets(Vector2 center, float radius, Vector2[] results)
        {
            QueryCount++;
            if (results == null) return 0;

            float reach = radius + GrappleTargetRadius;
            float reachSq = reach * reach;
            int count = 0;
            for (int i = 0; i < grapplePoints.Count && count < results.Length; i++)
            {
                if ((grapplePoints[i] - center).sqrMagnitude <= reachSq) results[count++] = grapplePoints[i];
            }

            return count;
        }

        public static bool Matches(AabbKind kind, QueryLayer layer)
        {
            switch (layer)
            {
                case QueryLayer.Solids: return kind == AabbKind.Ground || kind == AabbKind.Boundary || kind == AabbKind.Enemy;
                case QueryLayer.WallJumpable:
                case QueryLayer.SafeGround:
                case QueryLayer.GrappleObstacle: return kind == AabbKind.Ground;
                case QueryLayer.KillZones: return kind == AabbKind.KillZone;
                default: return false;
            }
        }

        // Interseção raio × caixa (método das placas). Origem dentro da caixa acerta em 0 com normal −dir.
        private static bool RaySlab(Vector2 origin, Vector2 dir, float distance, in Box b, out float t, out Vector2 normal)
        {
            t = 0f;
            normal = Vector2.zero;

            bool inside = origin.x > b.Min.x && origin.x < b.Max.x && origin.y > b.Min.y && origin.y < b.Max.y;
            if (inside)
            {
                normal = -dir;
                return true;
            }

            float tMin = 0f;
            float tMax = distance;
            Vector2 enterNormal = Vector2.zero;

            if (!Slab(origin.x, dir.x, b.Min.x, b.Max.x, ref tMin, ref tMax, ref enterNormal, new Vector2(1f, 0f))) return false;
            if (!Slab(origin.y, dir.y, b.Min.y, b.Max.y, ref tMin, ref tMax, ref enterNormal, new Vector2(0f, 1f))) return false;

            t = tMin;
            normal = enterNormal;
            return true;
        }

        private static bool Slab(float o, float d, float min, float max, ref float tMin, ref float tMax, ref Vector2 enterNormal, Vector2 axis)
        {
            if (Math.Abs(d) < 1e-9f)
            {
                return o > min && o < max;
            }

            float t1 = (min - o) / d;
            float t2 = (max - o) / d;
            Vector2 n = -axis;
            if (t1 > t2)
            {
                float tmp = t1;
                t1 = t2;
                t2 = tmp;
                n = axis;
            }

            if (t1 > tMin)
            {
                tMin = t1;
                enterNormal = n;
            }

            if (t2 < tMax) tMax = t2;
            return tMin <= tMax && t2 > 0f;
        }

        private void InsertInGrid(int index)
        {
            Box b = boxes[index];
            ForEachCell(b.Min, b.Max, out int cx0, out int cy0, out int cx1, out int cy1);
            for (int cx = cx0; cx <= cx1; cx++)
            {
                for (int cy = cy0; cy <= cy1; cy++)
                {
                    long key = Key(cx, cy);
                    if (!grid.TryGetValue(key, out List<int> cell))
                    {
                        cell = new List<int>(4);
                        grid.Add(key, cell);
                    }

                    cell.Add(index);
                }
            }
        }

        private void RemoveFromGrid(int index)
        {
            Box b = boxes[index];
            ForEachCell(b.Min, b.Max, out int cx0, out int cy0, out int cx1, out int cy1);
            for (int cx = cx0; cx <= cx1; cx++)
            {
                for (int cy = cy0; cy <= cy1; cy++)
                {
                    if (grid.TryGetValue(Key(cx, cy), out List<int> cell)) cell.Remove(index);
                }
            }
        }

        private static void ForEachCell(Vector2 min, Vector2 max, out int cx0, out int cy0, out int cx1, out int cy1)
        {
            cx0 = (int)Math.Floor(min.x / CellSize);
            cy0 = (int)Math.Floor(min.y / CellSize);
            cx1 = (int)Math.Floor(max.x / CellSize);
            cy1 = (int)Math.Floor(max.y / CellSize);
        }

        private static long Key(int cx, int cy) => ((long)cx << 32) ^ (uint)cy;

        private void BeginVisit()
        {
            stamp++;
            if (stamp == int.MaxValue)
            {
                Array.Clear(visitStamp, 0, visitStamp.Length);
                stamp = 1;
            }
        }

        private bool Visit(int index)
        {
            if (visitStamp[index] == stamp) return false;
            visitStamp[index] = stamp;
            return true;
        }
    }
}
