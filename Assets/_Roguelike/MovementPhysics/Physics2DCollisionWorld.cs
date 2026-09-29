using System;
using Roguelike.Movement;
using UnityEngine;

namespace Roguelike.MovementPhysics
{
    /// <summary>
    /// Mundo de colisão do jogo sobre o Physics2D (SPEC §2.4). Um <see cref="ContactFilter2D"/> por
    /// <see cref="QueryLayer"/> (layer mask + triggers explícitos, sobrescrevendo o queriesHitTriggers = 1 do projeto),
    /// buffers pré-alocados (não aloca por consulta) e o menor distance válido (não confia na ordem do array).
    ///
    /// Compensação medida na M.8 (Unity 6000.1, Physics2D.defaultContactOffset = 0,01): o Box2D arredonda as formas,
    /// então um BoxCast acusa o contato 0,005 u antes da geometria e um OverlapBox acusa sobreposição até 0,02 u de
    /// folga. Para que as consultas batam com a geometria (e com o AabbCollisionWorld dos testes, ± 0,001 u), a caixa
    /// do cast é encolhida em <see cref="CastInflation"/> por lado e a do overlap em <see cref="OverlapInflation"/>.
    /// Raycasts são exatos. CollisionParityTests congela esse contrato.
    /// </summary>
    public sealed class Physics2DCollisionWorld : ICollisionWorld
    {
        private const float MinQuerySize = 0.001f;

        private readonly ContactFilter2D[] filters = new ContactFilter2D[5];
        private readonly ContactFilter2D grappleFilter;
        private readonly ContactFilter2D enemyFilter;
        private readonly RaycastHit2D[] hits = new RaycastHit2D[8];
        private readonly Collider2D[] overlaps = new Collider2D[16];

        public Physics2DCollisionWorld()
        {
            for (int i = 0; i < filters.Length; i++)
            {
                var layer = (QueryLayer)i;
                filters[i] = CreateFilter(MovementLayers.MaskFor(layer), MovementLayers.UsesTriggers(layer));
            }

            grappleFilter = CreateFilter(MovementLayers.GrappleTargetMask, true);
            enemyFilter = CreateFilter(1 << MovementLayers.Enemy, false);

            float offset = Physics2D.defaultContactOffset;
            CastInflation = offset * 0.5f;
            OverlapInflation = offset * 2f + 0.0001f;
        }

        /// <summary>Quanto o BoxCast do Physics2D antecipa o contato (u, por lado).</summary>
        public float CastInflation { get; set; }

        /// <summary>Folga que o OverlapBox do Physics2D ainda conta como sobreposição (u, por lado).</summary>
        public float OverlapInflation { get; set; }

        /// <summary>Consultas desde o último <see cref="ResetQueryCount"/> (overlay, RNF-09).</summary>
        public int QueryCount { get; private set; }

        public void ResetQueryCount() => QueryCount = 0;

        public bool CastBox(Vector2 center, Vector2 size, Vector2 dir, float distance, QueryLayer layer, out BoxHit hit)
        {
            QueryCount++;
            hit = default;
            Vector2 querySize = Shrink(size, CastInflation);
            int count = Physics2D.BoxCast(center, querySize, 0f, dir, filters[(int)layer], hits, distance);

            float best = float.PositiveInfinity;
            Vector2 bestNormal = Vector2.zero;
            for (int i = 0; i < count; i++)
            {
                RaycastHit2D h = hits[i];
                if (h.collider == null) continue;
                float d = Math.Max(0f, h.distance);

                // DS-11: inimigo que já sobrepõe a caixa no início é ignorado (andando, não prende nem empurra).
                if (d <= 1e-4f && h.collider.gameObject.layer == MovementLayers.Enemy && StartsInside(center, size, h.collider)) continue;

                if (d < best)
                {
                    best = d;
                    bestNormal = h.normal;
                }
            }

            if (float.IsPositiveInfinity(best)) return false;
            hit = new BoxHit(best, bestNormal);
            return true;
        }

        public bool OverlapBox(Vector2 center, Vector2 size, QueryLayer layer)
        {
            QueryCount++;
            return Physics2D.OverlapBox(center, Shrink(size, OverlapInflation), 0f, filters[(int)layer], overlaps) > 0;
        }

        public bool Raycast(Vector2 origin, Vector2 dir, float distance, QueryLayer layer, out BoxHit hit)
        {
            QueryCount++;
            hit = default;
            int count = Physics2D.Raycast(origin, dir, filters[(int)layer], hits, distance);

            float best = float.PositiveInfinity;
            Vector2 bestNormal = Vector2.zero;
            for (int i = 0; i < count; i++)
            {
                if (hits[i].collider == null) continue;
                float d = Math.Max(0f, hits[i].distance);
                if (d < best)
                {
                    best = d;
                    bestNormal = hits[i].normal;
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

            int count = Physics2D.OverlapCircle(center, radius, grappleFilter, overlaps);
            int written = 0;
            for (int i = 0; i < count && written < results.Length; i++)
            {
                if (overlaps[i] == null) continue;
                results[written++] = overlaps[i].bounds.center;
            }

            return written;
        }

        private bool StartsInside(Vector2 center, Vector2 size, Collider2D collider)
        {
            int count = Physics2D.OverlapBox(center, Shrink(size, OverlapInflation), 0f, enemyFilter, overlaps);
            for (int i = 0; i < count; i++)
            {
                if (overlaps[i] == collider) return true;
            }

            return false;
        }

        private static Vector2 Shrink(Vector2 size, float perSide)
        {
            return new Vector2(Math.Max(MinQuerySize, size.x - 2f * perSide), Math.Max(MinQuerySize, size.y - 2f * perSide));
        }

        private static ContactFilter2D CreateFilter(int mask, bool useTriggers)
        {
            var filter = new ContactFilter2D
            {
                useLayerMask = true,
                layerMask = mask,
                useTriggers = useTriggers,
            };
            return filter;
        }
    }
}
