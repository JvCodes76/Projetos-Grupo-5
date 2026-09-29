using System;
using System.Collections.Generic;
using Roguelike.Movement;
using Roguelike.Upgrades;
using UnityEngine;

namespace Roguelike.Tests.Movement
{
    /// <summary>Atalhos de input por tick para os roteiros dos testes.</summary>
    internal static class In
    {
        public static readonly TickInput None = default;

        public static TickInput Move(int x, int y = 0) => new TickInput((sbyte)x, (sbyte)y, ButtonBits.None, ButtonBits.None);

        /// <summary>Aperta e segura no mesmo tick.</summary>
        public static TickInput Press(ButtonBits bits, int x = 0, int y = 0) => new TickInput((sbyte)x, (sbyte)y, bits, bits);

        /// <summary>Segura (sem aperto novo).</summary>
        public static TickInput Hold(ButtonBits bits, int x = 0, int y = 0) => new TickInput((sbyte)x, (sbyte)y, ButtonBits.None, bits);

        /// <summary>Aperta e solta antes do tick (toque): Pressed sem Held.</summary>
        public static TickInput Tap(ButtonBits bits, int x = 0, int y = 0) => new TickInput((sbyte)x, (sbyte)y, bits, ButtonBits.None);
    }

    /// <summary>
    /// Monta mundo AABB + perfil padrão + motor e roda roteiros de input (SPEC §16). Mede ápice, tempo e distância.
    /// Destrói o perfil em <see cref="Dispose"/> (chame no TearDown ou use using).
    /// </summary>
    internal sealed class MotorHarness : IDisposable
    {
        public readonly MovementProfile Profile;
        public readonly AabbCollisionWorld World = new AabbCollisionWorld();
        public readonly int TickRate;
        public KitStatInput Kit;
        public PlayerMotor Motor;
        public long Tick;

        /// <summary>Fatos de cada tick rodado desde o spawn (índice = ordem).</summary>
        public readonly List<TickEvents> EventLog = new List<TickEvents>();

        private readonly bool ownsProfile;

        public MotorHarness(int tickRate = 60, MovementProfile profile = null)
        {
            TickRate = tickRate;
            ownsProfile = profile == null;
            Profile = profile != null ? profile : ScriptableObject.CreateInstance<MovementProfile>();
            Kit = new KitStatInput(Profile);
        }

        public ref readonly MotorState S => ref Motor.State;

        public ref readonly TickEvents Ev => ref Motor.Events;

        public Vector2 Pos => Motor.State.Position;

        public Vector2 Vel => Motor.State.Velocity;

        public MovementStats Stats => MovementStatsResolver.Resolve(Profile, Kit, TickRate);

        /// <summary>Chão de Ground com topo em <paramref name="y"/>.</summary>
        public MotorHarness Floor(float y = 0f, float minX = -500f, float maxX = 500f)
        {
            World.AddBox(new Vector2(minX, y - 4f), new Vector2(maxX, y), AabbKind.Ground);
            return this;
        }

        public MotorHarness Box(float minX, float minY, float maxX, float maxY, AabbKind kind = AabbKind.Ground)
        {
            World.AddBox(new Vector2(minX, minY), new Vector2(maxX, maxY), kind);
            return this;
        }

        public MotorHarness Abilities(AbilityFlags flags, int airJumps = -1, int dashes = -1)
        {
            Kit.Flags = flags;
            if (airJumps >= 0) Kit.Set(StatType.MaxAirJumps, airJumps);
            if (dashes >= 0) Kit.Set(StatType.MaxDashes, dashes);
            return this;
        }

        public MotorHarness Spawn(Vector2 feet)
        {
            Motor = new PlayerMotor(Profile, World, Stats, feet, TickRate);
            Tick = 0;
            EventLog.Clear();
            return this;
        }

        /// <summary>Spawn em pé no chão (roda até pousar).</summary>
        public MotorHarness SpawnGrounded(Vector2 feet)
        {
            Spawn(feet);
            for (int i = 0; i < 30 && !S.Grounded; i++) Step(In.None);
            if (!S.Grounded) throw new InvalidOperationException("O jogador não pousou no spawn.");
            return this;
        }

        public void Step(in TickInput input)
        {
            Tick++;
            Motor.Tick(input, Tick);
            EventLog.Add(Motor.Events);
        }

        public void Run(int ticks, TickInput input)
        {
            for (int i = 0; i < ticks; i++) Step(input);
        }

        /// <summary>Roda um roteiro: input(i) para i = 0..ticks−1.</summary>
        public void Run(int ticks, Func<int, TickInput> script)
        {
            for (int i = 0; i < ticks; i++) Step(script(i));
        }

        /// <summary>Roda até <paramref name="condition"/> ou o limite; devolve os ticks rodados (−1 se não aconteceu).</summary>
        public int RunUntil(Func<bool> condition, Func<int, TickInput> script, int maxTicks = 600)
        {
            for (int i = 0; i < maxTicks; i++)
            {
                Step(script(i));
                if (condition()) return i + 1;
            }

            return -1;
        }

        public int RunUntil(Func<bool> condition, TickInput input, int maxTicks = 600)
        {
            return RunUntil(condition, _ => input, maxTicks);
        }

        public bool Happened(MovementEventFlags flag) => (Motor.Events.Flags & flag) != 0;

        public int CountEvents(MovementEventFlags flag)
        {
            int n = 0;
            foreach (var ev in EventLog) if ((ev.Flags & flag) != 0) n++;
            return n;
        }

        /// <summary>
        /// Pula do chão (já apoiado) segurando por <paramref name="holdTicks"/> ticks (1 = toque) e devolve
        /// (ápice acima do ponto de partida, ticks até o ápice, ticks no ar até pousar no mesmo nível).
        /// </summary>
        public (float apex, int ticksToApex, int airTicks) MeasureJump(int holdTicks, int moveX = 0)
        {
            float startY = Pos.y;
            float apex = 0f;
            int apexTick = 0;
            int tick = 0;
            bool left = false;
            while (tick < 600)
            {
                bool held = tick < holdTicks;
                var input = tick == 0
                    ? new TickInput((sbyte)moveX, 0, ButtonBits.Jump, held ? ButtonBits.Jump : ButtonBits.None)
                    : new TickInput((sbyte)moveX, 0, ButtonBits.None, held ? ButtonBits.Jump : ButtonBits.None);
                Step(input);
                tick++;
                float h = Pos.y - startY;
                if (h > apex)
                {
                    apex = h;
                    apexTick = tick;
                }

                if (!S.Grounded) left = true;
                if (left && S.Grounded) break;
            }

            return (apex, apexTick, tick);
        }

        public void Dispose()
        {
            if (ownsProfile && Profile != null) UnityEngine.Object.DestroyImmediate(Profile);
        }
    }
}
