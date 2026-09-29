using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Roguelike.Movement
{
    /// <summary>Trecho de chão onde o jogador fica em pé (pés em [MinX, MaxX] na altura Y).</summary>
    public struct Surface
    {
        public float MinX;
        public float MaxX;
        public float Y;

        public float Length => MaxX - MinX;

        public override string ToString() => $"y={Y:0.##} x=[{MinX:0.##}; {MaxX:0.##}]";
    }

    /// <summary>Resultado do analisador para um kit.</summary>
    public sealed class ReachabilityReport
    {
        public string KitName;
        public bool GoalReached;
        public int Simulations;
        public int SurfacesTotal;
        public readonly List<int> ReachedSurfaces = new List<int>();
        public float MaxFeetY = float.NegativeInfinity;
        public Vector2 HighestPoint;
        public int StartSurface = -1;
    }

    /// <summary>
    /// Analisador de alcançabilidade (SPEC §14.2, D1 opção B): roda o PRÓPRIO PlayerMotor sobre um AabbCollisionWorld
    /// da fase, com estratégias roteirizadas a partir de pontos de cada superfície alcançada (segurar 0/1/12/30 ticks;
    /// direção mantida ou neutra no ar; pulo aéreo no ápice e na queda; wall jump para longe e neutro), numa busca em
    /// largura sobre as superfícies. Indicativo: falsos negativos (rotas criativas) e positivos (timing muito preciso)
    /// são possíveis. Determinístico. Lógica pura (EditMode), usada pela janela Tools/Movement/Reachability e pelos
    /// ReachabilityTests.
    /// </summary>
    public sealed class ReachabilityModel
    {
        private const int MaxTicksPerSimulation = 300;
        private const float HalfWidthMargin = 0.3f;

        private readonly MovementProfile profile;
        private readonly AabbCollisionWorld world;
        private readonly float killPlaneY;
        private readonly List<Surface> surfaces;
        private readonly PlayerMotor motor;

        public ReachabilityModel(MovementProfile profile, AabbCollisionWorld world, float killPlaneY)
        {
            this.profile = profile != null ? profile : throw new ArgumentNullException(nameof(profile));
            this.world = world ?? throw new ArgumentNullException(nameof(world));
            this.killPlaneY = killPlaneY;
            surfaces = FindSurfaces(world, profile.BodySize);
            // Motor reaproveitado em todas as simulações (DebugReset); nasce longe da geometria.
            motor = new PlayerMotor(profile, world, MovementStatsResolver.ResolveBase(profile), new Vector2(0f, 1e5f));
        }

        public IReadOnlyList<Surface> Surfaces => surfaces;

        /// <summary>Superfícies de Ground onde o corpo cabe em pé (sem nada sólido na altura do corpo).</summary>
        public static List<Surface> FindSurfaces(AabbCollisionWorld world, Vector2 bodySize)
        {
            float half = bodySize.x * 0.5f;
            var boxes = new List<AabbCollisionWorld.Box>(world.BoxCount);
            for (int i = 0; i < world.BoxCount; i++) boxes.Add(world.GetBox(i));

            var result = new List<Surface>();
            var intervals = new List<Vector2>();
            foreach (AabbCollisionWorld.Box b in boxes)
            {
                if (b.Kind != AabbKind.Ground) continue;
                float y = b.Max.y;
                intervals.Clear();
                intervals.Add(new Vector2(b.Min.x, b.Max.x));

                foreach (AabbCollisionWorld.Box c in boxes)
                {
                    if (!AabbCollisionWorld.Matches(c.Kind, QueryLayer.Solids)) continue;
                    if (c.Min.y >= y + bodySize.y + 0.02f || c.Max.y <= y + 0.001f) continue;
                    Subtract(intervals, c.Min.x - half, c.Max.x + half);
                    if (intervals.Count == 0) break;
                }

                foreach (Vector2 iv in intervals)
                {
                    if (iv.y - iv.x >= 0.05f) result.Add(new Surface { MinX = iv.x, MaxX = iv.y, Y = y });
                }
            }

            // Junta trechos colineares que se tocam (caixas vizinhas de linhas diferentes do Tilemap).
            result.Sort((a, b) => a.Y != b.Y ? a.Y.CompareTo(b.Y) : a.MinX.CompareTo(b.MinX));
            var merged = new List<Surface>(result.Count);
            foreach (Surface s in result)
            {
                if (merged.Count > 0)
                {
                    Surface last = merged[merged.Count - 1];
                    if (Mathf.Abs(last.Y - s.Y) < 1e-4f && s.MinX <= last.MaxX + 1e-3f)
                    {
                        last.MaxX = Mathf.Max(last.MaxX, s.MaxX);
                        merged[merged.Count - 1] = last;
                        continue;
                    }
                }

                merged.Add(s);
            }

            return merged;
        }

        private static void Subtract(List<Vector2> intervals, float a, float b)
        {
            for (int i = intervals.Count - 1; i >= 0; i--)
            {
                Vector2 iv = intervals[i];
                if (b <= iv.x || a >= iv.y) continue;
                intervals.RemoveAt(i);
                if (a > iv.x) intervals.Add(new Vector2(iv.x, a));
                if (b < iv.y) intervals.Add(new Vector2(b, iv.y));
            }
        }

        /// <summary>Índice da superfície sob os pés (−1 se nenhuma).</summary>
        public int SurfaceAt(Vector2 feet)
        {
            float half = profile.BodySize.x * 0.5f;
            for (int i = 0; i < surfaces.Count; i++)
            {
                Surface s = surfaces[i];
                if (Mathf.Abs(s.Y - feet.y) < 0.05f && feet.x >= s.MinX - half && feet.x <= s.MaxX + half) return i;
            }

            return -1;
        }

        /// <summary>Busca em largura a partir do spawn. Para ao alcançar um objetivo (ou no limite de simulações).</summary>
        public ReachabilityReport Run(in MovementStats stats, Vector2 spawnFeet, IReadOnlyList<Rect> goals, string kitName,
            int maxSimulations = 250_000)
        {
            var report = new ReachabilityReport { KitName = kitName, SurfacesTotal = surfaces.Count };
            var visited = new bool[surfaces.Count];
            var queue = new Queue<int>();

            // Começa soltando o jogador no spawn até pousar.
            int start = DropFrom(stats, spawnFeet, goals, report);
            report.StartSurface = start;
            if (report.GoalReached || start < 0) return report;
            visited[start] = true;
            report.ReachedSurfaces.Add(start);
            queue.Enqueue(start);

            bool hasAir = stats.MaxAirJumps > 0;
            bool hasWall = stats.Has(Upgrades.AbilityFlags.WallJump);
            int[] holds = { 0, 1, 12, 30 };
            var takeoffs = new List<float>();

            while (queue.Count > 0 && report.Simulations < maxSimulations)
            {
                Surface s = surfaces[queue.Dequeue()];
                takeoffs.Clear();
                float a = s.MinX + Mathf.Min(HalfWidthMargin, s.Length * 0.5f);
                float b = s.MaxX - Mathf.Min(HalfWidthMargin, s.Length * 0.5f);
                takeoffs.Add(a);
                for (float x = a + 2f; x < b - 0.5f; x += 2f) takeoffs.Add(x);
                if (b > a + 0.01f) takeoffs.Add(b);

                foreach (float x in takeoffs)
                {
                    for (int dir = -1; dir <= 1; dir += 2)
                    {
                        for (int run = 0; run <= 1; run++)
                        {
                            foreach (int hold in holds)
                            {
                                for (int air = 0; air <= 1; air++)
                                {
                                    for (int airJump = 0; airJump <= (hasAir ? 2 : 0); airJump++)
                                    {
                                        for (int wall = 0; wall <= (hasWall ? 2 : 0); wall++)
                                        {
                                            var strategy = new Strategy
                                            {
                                                Dir = (sbyte)dir,
                                                StartSpeed = run == 1 ? dir * stats.MaxSpeed : 0f,
                                                HoldTicks = hold,
                                                NeutralAfter = air == 1 ? 10 : int.MaxValue,
                                                AirJump = airJump,
                                                Wall = wall,
                                            };

                                            int landed = Simulate(stats, new Vector2(x, s.Y), strategy, goals, report);
                                            report.Simulations++;
                                            if (report.GoalReached) return report;
                                            if (landed >= 0 && !visited[landed])
                                            {
                                                visited[landed] = true;
                                                report.ReachedSurfaces.Add(landed);
                                                queue.Enqueue(landed);
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }

            return report;
        }

        /// <summary>
        /// Soft-locks (RF-42): superfícies alcançáveis a partir do spawn (busca completa, sem parar no objetivo) das quais o
        /// objetivo deixa de ser alcançável (uma busca por superfície, partindo do meio dela). Lento: uma BFS por superfície.
        /// </summary>
        public List<int> FindSoftLocks(in MovementStats stats, Vector2 spawnFeet, IReadOnlyList<Rect> goals, out int reachableCount)
        {
            var locked = new List<int>();
            ReachabilityReport all = Run(stats, spawnFeet, null, "todas");
            reachableCount = all.ReachedSurfaces.Count;
            if (goals == null || goals.Count == 0) return locked;
            foreach (int i in all.ReachedSurfaces)
            {
                Surface s = surfaces[i];
                ReachabilityReport from = Run(stats, new Vector2((s.MinX + s.MaxX) * 0.5f, s.Y + 0.05f), goals, "soft-lock");
                if (!from.GoalReached) locked.Add(i);
            }

            return locked;
        }

        /// <summary>Seção Markdown dos soft-locks de um kit.</summary>
        public string SoftLocksToMarkdown(string kitName, IReadOnlyList<int> locked, int reachedCount)
        {
            var sb = new StringBuilder();
            sb.AppendLine().AppendLine($"## Soft-locks — {kitName}: {locked.Count}/{reachedCount}").AppendLine();
            var list = new List<Surface>();
            foreach (int i in locked) list.Add(surfaces[i]);
            list.Sort((a, b) => a.Y != b.Y ? a.Y.CompareTo(b.Y) : a.MinX.CompareTo(b.MinX));
            foreach (Surface s in list) sb.AppendLine($"- sem volta ao objetivo a partir de {s}");
            return sb.ToString();
        }

        private struct Strategy
        {
            public sbyte Dir;
            public float StartSpeed;
            public int HoldTicks;
            public int NeutralAfter;
            public int AirJump; // 0 nenhum · 1 no ápice · 2 caindo
            public int Wall;    // 0 nenhum · 1 para longe (alterna paredes) · 2 neutro (sobe a mesma parede)
        }

        private int DropFrom(in MovementStats stats, Vector2 feet, IReadOnlyList<Rect> goals, ReachabilityReport report)
        {
            motor.DebugReset(feet, killPlaneY, Vector2.zero, false, stats);
            for (int t = 1; t <= MaxTicksPerSimulation; t++)
            {
                motor.Tick(default, t);
                if (TouchesGoal(goals)) report.GoalReached = true;
                if (motor.State.Grounded) return SurfaceAt(motor.State.Position);
            }

            return -1;
        }

        private int Simulate(in MovementStats stats, Vector2 feet, in Strategy k, IReadOnlyList<Rect> goals, ReachabilityReport report)
        {
            motor.DebugReset(feet, killPlaneY, new Vector2(k.StartSpeed, 0f), true, stats);
            bool airborne = false;
            bool airJumpUsed = false;
            bool rising = false;
            int dir = k.Dir;
            bool holdingJump = k.HoldTicks > 0;
            int holdUntil = k.HoldTicks;
            int lastWallJump = -100;

            for (int t = 0; t < MaxTicksPerSimulation; t++)
            {
                ref readonly MotorState s = ref motor.State;
                sbyte moveX = (sbyte)(t >= k.NeutralAfter ? 0 : dir);
                ButtonBits pressed = ButtonBits.None;

                if (t == 0 && k.HoldTicks > 0) pressed |= ButtonBits.Jump;

                if (!s.Grounded && s.Velocity.y > 0f) rising = true;
                if (k.AirJump != 0 && !airJumpUsed && airborne && stats.MaxAirJumps > 0)
                {
                    bool atApex = k.AirJump == 1 && rising && s.Velocity.y <= 0f;
                    bool falling = k.AirJump == 2 && s.Velocity.y < -5f;
                    if (atApex || falling)
                    {
                        pressed |= ButtonBits.Jump;
                        airJumpUsed = true;
                        holdingJump = true;
                        holdUntil = t + 30;
                    }
                }

                if (k.Wall != 0 && s.WallSlideSide != 0 && t - lastWallJump > 4)
                {
                    pressed |= ButtonBits.Jump;
                    holdingJump = true;
                    holdUntil = t + 30;
                    lastWallJump = t;
                    if (k.Wall == 1)
                    {
                        dir = -s.WallSlideSide; // alterna para a parede oposta (chaminé)
                        moveX = (sbyte)s.WallSlideSide; // com direção: wall jump com trava, para longe
                    }
                    else
                    {
                        moveX = 0; // neutro: sem trava, volta para a mesma parede
                    }
                }

                ButtonBits held = holdingJump && t < holdUntil ? ButtonBits.Jump : ButtonBits.None;
                motor.Tick(new TickInput(moveX, 0, pressed, held | (pressed & ButtonBits.Jump)), t + 1);
                s = ref motor.State;

                if (TouchesGoal(goals))
                {
                    report.GoalReached = true;
                    return -1;
                }

                if (s.Position.y > report.MaxFeetY)
                {
                    report.MaxFeetY = s.Position.y;
                    report.HighestPoint = s.Position;
                }

                if (motor.Events.Has(MovementEventFlags.FellOut) || s.State == MotorStateId.Respawning) return -1;
                if (!s.Grounded) airborne = true;
                else if (airborne || (t > 2 && Mathf.Abs(s.Velocity.x) < 1e-3f)) return SurfaceAt(s.Position);
            }

            return motor.State.Grounded ? SurfaceAt(motor.State.Position) : -1;
        }

        private bool TouchesGoal(IReadOnlyList<Rect> goals)
        {
            if (goals == null || goals.Count == 0) return false;
            Vector2 center = motor.BodyCenter;
            var body = new Rect(center - profile.BodySize * 0.5f, profile.BodySize);
            for (int i = 0; i < goals.Count; i++)
            {
                if (body.Overlaps(goals[i])) return true;
            }

            return false;
        }

        /// <summary>Relatório em Markdown (Temp/Reachability_&lt;Cena&gt;.md).</summary>
        public string ToMarkdown(string sceneName, IReadOnlyList<ReachabilityReport> reports, IReadOnlyList<Rect> goals)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"# Alcançabilidade — {sceneName}").AppendLine();
            sb.AppendLine($"Superfícies: {surfaces.Count}. Objetivo(s): {(goals == null || goals.Count == 0 ? "nenhum" : string.Join(", ", goals))}.").AppendLine();
            sb.AppendLine("| Kit | Objetivo | Superfícies alcançadas | Maior altura dos pés | Simulações |");
            sb.AppendLine("|---|---|---|---|---|");
            foreach (ReachabilityReport r in reports)
            {
                sb.AppendLine($"| {r.KitName} | {(r.GoalReached ? "✅" : "❌")} | {r.ReachedSurfaces.Count}/{r.SurfacesTotal} | {r.MaxFeetY:0.0} em ({r.HighestPoint.x:0.0}; {r.HighestPoint.y:0.0}) | {r.Simulations} |");
            }

            foreach (ReachabilityReport r in reports)
            {
                if (r.GoalReached) continue;
                sb.AppendLine().AppendLine($"## Fronteira — {r.KitName}").AppendLine();
                var reached = new List<Surface>();
                foreach (int i in r.ReachedSurfaces) reached.Add(surfaces[i]);
                reached.Sort((a, b) => b.Y.CompareTo(a.Y));
                for (int i = 0; i < Math.Min(8, reached.Count); i++) sb.AppendLine($"- alcançada: {reached[i]}");

                var blocking = new List<Surface>();
                var reachedSet = new HashSet<int>(r.ReachedSurfaces);
                for (int i = 0; i < surfaces.Count; i++)
                {
                    if (!reachedSet.Contains(i) && surfaces[i].Y > r.MaxFeetY - 0.1f && surfaces[i].Y < r.MaxFeetY + 6f) blocking.Add(surfaces[i]);
                }

                blocking.Sort((a, b) => a.Y.CompareTo(b.Y));
                for (int i = 0; i < Math.Min(12, blocking.Count); i++) sb.AppendLine($"- não alcançada logo acima: {blocking[i]}");
            }

            return sb.ToString();
        }
    }
}
