using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using Roguelike.Movement;
using Roguelike.Upgrades;
using UnityEngine;

namespace Roguelike.Tests.Levels
{
    /// <summary>
    /// Varredura das fases no Physics2D real (SPEC §16, RF-38): todo trecho de chão percorrido a 10 e 27 u/s nos dois
    /// sentidos e toda parede descida em wall slide, sem paradas espúrias nas emendas do composite.
    /// </summary>
    [Category("Levels")]
    [Category("Slow")]
    public class LevelSweepTests
    {
        private const float EdgeMargin = 0.35f;

        private static IEnumerable<string> Phases => LevelTestContext.PhaseScenes;

        [TestCaseSource(nameof(Phases))]
        public void EveryFloorSegment_RunsWithoutSpuriousStops(string scenePath)
        {
            LevelTestContext ctx = LevelTestContext.Open(scenePath);
            MovementStats stats = ctx.Stats();
            var motor = new PlayerMotor(ctx.Profile, ctx.Physics, stats, new Vector2(0f, 1e5f));
            List<Surface> surfaces = ReachabilityModel.FindSurfaces(ctx.Level.World, ctx.Profile.BodySize);
            var failures = new StringBuilder();
            int failed = 0, runs = 0;

            foreach (Surface s in surfaces)
            {
                if (s.Length < 2f * EdgeMargin + 0.5f) continue;
                foreach (float speed in new[] { 10f, 27f })
                {
                    for (int dir = -1; dir <= 1; dir += 2)
                    {
                        float start = dir > 0 ? s.MinX + EdgeMargin : s.MaxX - EdgeMargin;
                        float end = dir > 0 ? s.MaxX - EdgeMargin : s.MinX + EdgeMargin;
                        runs++;
                        string problem = RunAlong(motor, stats, ctx.KillPlaneY, new Vector2(start, s.Y), dir, speed, end);
                        if (problem == null) continue;
                        failed++;
                        if (failed <= 20) failures.AppendLine($"{s} a {speed} u/s, sentido {dir}: {problem}");
                    }
                }
            }

            Assert.Greater(runs, 0);
            Assert.AreEqual(0, failed, $"{ctx.Name}: {failed}/{runs} percursos com parada espúria\n{failures}");
        }

        [TestCaseSource(nameof(Phases))]
        public void EveryWall_SlidesWithoutSpuriousStops(string scenePath)
        {
            LevelTestContext ctx = LevelTestContext.Open(scenePath);
            MovementStats stats = ctx.Stats(AbilityFlags.WallJump);
            var motor = new PlayerMotor(ctx.Profile, ctx.Physics, stats, new Vector2(0f, 1e5f));
            Vector2 body = ctx.Profile.BodySize;
            var failures = new StringBuilder();
            int failed = 0, runs = 0;

            foreach (WallFace w in WallFaces(ctx.Level.World))
            {
                if (w.MaxY - w.MinY < body.y + 1f) continue;
                // Corpo encostado na face, cabeça 0,1 u abaixo do topo livre, segurando na direção da parede.
                float x = w.Side > 0 ? w.X - body.x * 0.5f : w.X + body.x * 0.5f;
                var feet = new Vector2(x, w.MaxY - 0.1f - body.y);
                if (ctx.Physics.OverlapBox(feet + new Vector2(0f, body.y * 0.5f), body, QueryLayer.Solids)) continue;
                runs++;
                string problem = SlideDown(motor, stats, ctx.KillPlaneY, feet, w);
                if (problem == null) continue;
                failed++;
                if (failed <= 20) failures.AppendLine($"parede x={w.X:0.###} lado {w.Side} y [{w.MinY:0.##}; {w.MaxY:0.##}]: {problem}");
            }

            Assert.Greater(runs, 0);
            Assert.AreEqual(0, failed, $"{ctx.Name}: {failed}/{runs} slides com parada espúria\n{failures}");
        }

        private static string RunAlong(PlayerMotor motor, in MovementStats stats, float killPlaneY, Vector2 feet, int dir, float speed, float end)
        {
            motor.DebugReset(feet, killPlaneY, new Vector2(dir * speed, 0f), true, stats);
            var input = new TickInput((sbyte)dir, 0, ButtonBits.None, ButtonBits.None);
            float lastX = feet.x;
            for (int t = 1; t <= 600; t++)
            {
                motor.Tick(input, t);
                ref readonly MotorState s = ref motor.State;
                if (dir > 0 ? s.Position.x >= end : s.Position.x <= end) return null;
                if (!s.Grounded) return $"saiu do chão em x={s.Position.x:0.###} (tick {t})";
                if (Mathf.Abs(s.Position.y - feet.y) > 0.01f) return $"pé mudou para y={s.Position.y:0.###} em x={s.Position.x:0.###}";
                if ((s.Position.x - lastX) * dir <= 1e-4f) return $"parou em x={s.Position.x:0.###} (tick {t}, vx={s.Velocity.x:0.##})";
                lastX = s.Position.x;
            }

            return "não chegou ao fim em 600 ticks";
        }

        private static string SlideDown(PlayerMotor motor, in MovementStats stats, float killPlaneY, Vector2 feet, WallFace w)
        {
            motor.DebugReset(feet, killPlaneY, Vector2.zero, false, stats);
            var input = new TickInput((sbyte)w.Side, 0, ButtonBits.None, ButtonBits.None);
            float lastY = feet.y;
            bool slid = false;
            // Paredes externas passam de 60 u: o limite acompanha a altura (slide a WallSlideSpeed, com folga).
            int maxTicks = 120 + (int)((w.MaxY - w.MinY) / Mathf.Max(0.5f, motor.Profile.WallSlideSpeed) * 60f * 1.5f);
            for (int t = 1; t <= maxTicks; t++)
            {
                motor.Tick(input, t);
                ref readonly MotorState s = ref motor.State;
                if (s.Grounded || s.State != MotorStateId.Normal) return null;
                // Saiu da face (a parte de baixo da parede acabou ou tem uma quina): fim do trecho.
                if (s.Position.y + motor.Profile.BodySize.y * 0.5f < w.MinY) return null;
                if (s.WallSlideSide != 0) slid = true;
                if (slid && s.WallSlideSide == 0 && s.Position.y > w.MinY + 0.5f) return $"soltou da parede em y={s.Position.y:0.###} (tick {t})";
                if (t > 2 && lastY - s.Position.y <= 1e-4f) return $"parou em y={s.Position.y:0.###} (tick {t}, vy={s.Velocity.y:0.##})";
                lastY = s.Position.y;
            }

            return $"não terminou a descida em {maxTicks} ticks";
        }

        /// <summary>Face vertical livre de uma parede de Ground. Side = +1: parede à direita do corpo (face voltada para −x).</summary>
        private struct WallFace
        {
            public float X, MinY, MaxY;
            public int Side;
        }

        // Faces verticais das caixas de Ground, juntadas quando colineares e contíguas, menos os trechos tapados por
        // outra caixa sólida encostada do lado de fora.
        private static List<WallFace> WallFaces(AabbCollisionWorld world)
        {
            var boxes = new List<AabbCollisionWorld.Box>();
            for (int i = 0; i < world.BoxCount; i++) boxes.Add(world.GetBox(i));
            var raw = new List<WallFace>();
            foreach (AabbCollisionWorld.Box b in boxes)
            {
                if (b.Kind != AabbKind.Ground) continue;
                raw.Add(new WallFace { X = b.Min.x, MinY = b.Min.y, MaxY = b.Max.y, Side = 1 });
                raw.Add(new WallFace { X = b.Max.x, MinY = b.Min.y, MaxY = b.Max.y, Side = -1 });
            }

            raw.Sort((a, b) => a.Side != b.Side ? a.Side.CompareTo(b.Side) : a.X != b.X ? a.X.CompareTo(b.X) : a.MinY.CompareTo(b.MinY));
            var merged = new List<WallFace>();
            foreach (WallFace f in raw)
            {
                if (merged.Count > 0)
                {
                    WallFace last = merged[merged.Count - 1];
                    if (last.Side == f.Side && Mathf.Abs(last.X - f.X) < 1e-3f && f.MinY <= last.MaxY + 1e-3f)
                    {
                        last.MaxY = Mathf.Max(last.MaxY, f.MaxY);
                        merged[merged.Count - 1] = last;
                        continue;
                    }
                }

                merged.Add(f);
            }

            // Tira os trechos com sólido encostado do lado de fora (0,05 u além da face).
            var result = new List<WallFace>();
            foreach (WallFace f in merged)
            {
                var pieces = new List<Vector2> { new Vector2(f.MinY, f.MaxY) };
                float probe = f.Side > 0 ? f.X - 0.05f : f.X + 0.05f;
                foreach (AabbCollisionWorld.Box c in boxes)
                {
                    if (!AabbCollisionWorld.Matches(c.Kind, QueryLayer.Solids)) continue;
                    if (probe <= c.Min.x || probe >= c.Max.x) continue;
                    for (int i = pieces.Count - 1; i >= 0; i--)
                    {
                        Vector2 iv = pieces[i];
                        if (c.Max.y <= iv.x || c.Min.y >= iv.y) continue;
                        pieces.RemoveAt(i);
                        if (c.Min.y > iv.x) pieces.Add(new Vector2(iv.x, c.Min.y));
                        if (c.Max.y < iv.y) pieces.Add(new Vector2(c.Max.y, iv.y));
                    }
                }

                foreach (Vector2 iv in pieces) result.Add(new WallFace { X = f.X, MinY = iv.x, MaxY = iv.y, Side = f.Side });
            }

            return result;
        }
    }
}
