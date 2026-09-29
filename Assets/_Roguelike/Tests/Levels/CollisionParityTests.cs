using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using Roguelike.Movement;
using UnityEngine;

namespace Roguelike.Tests.Levels
{
    /// <summary>
    /// Paridade Physics2D × AABB (SPEC §16): 2 000 consultas por fase — casts de caixa nos 4 eixos, overlaps e raycasts —
    /// dão o mesmo acerto e a mesma distância (± 0,001 u). Congela a compensação do Physics2DCollisionWorld (M.8) e o
    /// extrator de fases do analisador de alcançabilidade.
    /// </summary>
    [Category("Levels")]
    [Category("Slow")]
    public class CollisionParityTests
    {
        private const int Queries = 2000;
        private const float Tolerance = 0.001f;

        private static IEnumerable<string> Phases => LevelTestContext.PhaseScenes;

        [TestCaseSource(nameof(Phases))]
        public void PhysicsAndAabbWorldsAgree(string scenePath)
        {
            LevelTestContext ctx = LevelTestContext.Open(scenePath);
            AabbCollisionWorld aabb = ctx.Level.World;
            var rng = new System.Random(20260929);
            Rect bounds = ctx.Level.Bounds;
            var failures = new StringBuilder();
            int failed = 0, checkedCount = 0;
            Vector2[] axes = { Vector2.right, Vector2.left, Vector2.up, Vector2.down };

            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            Vector2 RandomPoint() => new Vector2(R(bounds.xMin, bounds.xMax), R(bounds.yMin, bounds.yMax));
            bool InsideSolid(Vector2 p) => aabb.OverlapBox(p, new Vector2(0.01f, 0.01f), QueryLayer.Solids);

            void Fail(string what)
            {
                failed++;
                if (failed <= 20) failures.AppendLine(what);
            }

            // Sorteia até ter 2 000 consultas válidas (as que começam dentro de sólido ou na borda da tolerância são
            // descartadas; a Primeira tem muito volume sólido).
            for (int q = 0; q < Queries * 10 && checkedCount < Queries; q++)
            {
                int kind = q % 3;
                if (kind == 0)
                {
                    // Cast de caixa (como o MoveX/MoveY do motor): nunca começa sobreposto.
                    Vector2 size = q % 2 == 0 ? ctx.Profile.BodySize : new Vector2(R(0.2f, 1.5f), R(0.2f, 1.5f));
                    Vector2 c = RandomPoint();
                    if (aabb.OverlapBox(c, size + new Vector2(0.004f, 0.004f), QueryLayer.Solids) || ctx.Physics.OverlapBox(c, size, QueryLayer.Solids)) continue;
                    Vector2 dir = axes[rng.Next(4)];
                    float dist = R(0.05f, 6f);
                    bool ha = aabb.CastBox(c, size, dir, dist, QueryLayer.Solids, out BoxHit a);
                    bool hp = ctx.Physics.CastBox(c, size, dir, dist, QueryLayer.Solids, out BoxHit p);
                    // Acerto a menos de 0,002 u do alcance máximo é ambíguo pela tolerância.
                    if (ha != hp && Mathf.Min(ha ? a.Distance : dist, hp ? p.Distance : dist) > dist - 0.002f) continue;
                    checkedCount++;
                    if (ha != hp) Fail($"CastBox c={c} size={size} dir={dir} dist={dist}: AABB {(ha ? a.Distance.ToString("0.0000") : "—")} × Physics2D {(hp ? p.Distance.ToString("0.0000") : "—")}");
                    else if (ha && Mathf.Abs(a.Distance - p.Distance) > Tolerance) Fail($"CastBox c={c} size={size} dir={dir}: distância AABB {a.Distance:0.0000} × Physics2D {p.Distance:0.0000}");
                }
                else if (kind == 1)
                {
                    // Overlap de caixa, fora de sólido fundo (o centro livre) e longe da borda (± 0,002 u).
                    Vector2 size = new Vector2(R(0.1f, 1.5f), R(0.1f, 1.5f));
                    Vector2 c = RandomPoint();
                    if (InsideSolid(c)) continue;
                    QueryLayer layer = q % 5 == 0 ? QueryLayer.KillZones : QueryLayer.Solids;
                    bool shrunk = aabb.OverlapBox(c, size - new Vector2(0.004f, 0.004f), layer);
                    bool grown = aabb.OverlapBox(c, size + new Vector2(0.004f, 0.004f), layer);
                    if (shrunk != grown) continue;
                    checkedCount++;
                    bool ha = aabb.OverlapBox(c, size, layer);
                    bool hp = ctx.Physics.OverlapBox(c, size, layer);
                    if (ha != hp) Fail($"OverlapBox {layer} c={c} size={size}: AABB {ha} × Physics2D {hp}");
                }
                else
                {
                    Vector2 o = RandomPoint();
                    if (InsideSolid(o)) continue;
                    Vector2 dir = q % 2 == 0 ? axes[rng.Next(4)] : new Vector2(R(-1f, 1f), R(-1f, 1f)).normalized;
                    if (dir == Vector2.zero) continue;
                    float dist = R(0.1f, 10f);
                    bool ha = aabb.Raycast(o, dir, dist, QueryLayer.Solids, out BoxHit a);
                    bool hp = ctx.Physics.Raycast(o, dir, dist, QueryLayer.Solids, out BoxHit p);
                    if (ha != hp && Mathf.Min(ha ? a.Distance : dist, hp ? p.Distance : dist) > dist - 0.002f) continue;
                    checkedCount++;
                    if (ha != hp) Fail($"Raycast o={o} dir={dir} dist={dist}: AABB {(ha ? a.Distance.ToString("0.0000") : "—")} × Physics2D {(hp ? p.Distance.ToString("0.0000") : "—")}");
                    else if (ha && Mathf.Abs(a.Distance - p.Distance) > Tolerance) Fail($"Raycast o={o} dir={dir}: distância AABB {a.Distance:0.0000} × Physics2D {p.Distance:0.0000}");
                }
            }

            Assert.AreEqual(Queries, checkedCount, "consultas válidas demais descartadas");
            Assert.AreEqual(0, failed, $"{ctx.Name}: {failed}/{checkedCount} consultas divergentes\n{failures}");
        }
    }
}
