using System.Runtime.InteropServices;
using UnityEngine;

namespace Roguelike.Movement
{
    /// <summary>
    /// Hash FNV-1a de 64 bits sobre todo o <see cref="MotorState"/> (RNF-02): mesma entrada por tick, mesmo build e
    /// plataforma ⇒ mesmo hash. Floats entram pelos bits (sem arredondamento). Lista explícita de campos, sem
    /// reflexão; um campo novo no MotorState entra aqui no mesmo PR.
    /// </summary>
    public static class MotorStateHash
    {
        private const ulong OffsetBasis = 14695981039346656037UL;
        private const ulong Prime = 1099511628211UL;

        public static ulong Compute(in MotorState s)
        {
            ulong h = OffsetBasis;

            Add(ref h, s.Position);
            Add(ref h, s.PrevPosition);
            Add(ref h, s.Velocity);
            Add(ref h, (int)s.State);
            Add(ref h, s.Grounded);
            Add(ref h, s.Facing);
            Add(ref h, s.Teleported);

            Add(ref h, s.TicksSinceGrounded);
            Add(ref h, s.CoyoteArmed);
            Add(ref h, s.AirStartTick);
            Add(ref h, s.VarJumpTicks);
            Add(ref h, s.VarJumpSpeed);
            Add(ref h, s.JumpStartTick);
            Add(ref h, s.MaxFallCurrent);
            Add(ref h, s.LandingImpact);
            Add(ref h, s.AirJumpsLeft);

            Add(ref h, s.ForceMoveX);
            Add(ref h, s.ForceMoveXTicks);

            Add(ref h, s.WallSlideSide);
            Add(ref h, s.LastWallSlideSide);
            Add(ref h, s.TicksSinceWallSlide);

            Add(ref h, s.RetainedVx);
            Add(ref h, s.RetentionTicks);

            Add(ref h, s.JumpBuffer);
            Add(ref h, s.DashBuffer);
            Add(ref h, s.GrappleBuffer);
            Add(ref h, s.EatenWatchTicks);

            Add(ref h, s.DashesLeft);
            Add(ref h, s.DashCooldownTicks);
            Add(ref h, s.DashRefillCooldownTicks);
            Add(ref h, s.DashTicksLeft);
            Add(ref h, (int)s.DashPhase);
            Add(ref h, s.DashDir);
            Add(ref h, s.BeforeDashVelocity);

            Add(ref h, (int)s.GrapplePhase);
            Add(ref h, s.GrappleAnchor);
            Add(ref h, s.HookTravelTicksLeft);
            Add(ref h, s.HookTravelTotal);
            Add(ref h, s.HookOrigin);
            Add(ref h, s.GrappleCooldownTicks);
            Add(ref h, s.GrappleTicks);
            Add(ref h, s.GrappleLaunchSpeedCaptured);
            Add(ref h, s.LastPullDir);
            Add(ref h, s.GrappleStuckRefDistance);
            Add(ref h, s.GrappleStuckTicks);
            Add(ref h, s.HasGrappleTarget);
            Add(ref h, s.GrappleTarget);

            Add(ref h, s.SpawnFeet);
            Add(ref h, s.KillPlaneY);
            Add(ref h, s.LastSafeGround);
            Add(ref h, s.SafeGroundStableTicks);
            Add(ref h, s.RespawnTicksLeft);
            Add(ref h, s.RespawnTarget);
            Add(ref h, (int)s.RespawnReason);
            Add(ref h, s.RestartHeldTicks);
            Add(ref h, s.DiedEmitted);
            Add(ref h, (int)s.DeathCause);

            Add(ref h, s.JumpsFromBuffer);
            Add(ref h, s.BufferExpired);
            Add(ref h, s.EatenInputs);
            Add(ref h, s.CoyoteJumps);
            Add(ref h, s.CornerCorrections);
            Add(ref h, s.ActionsDenied);
            Add(ref h, s.FellOuts);
            Add(ref h, s.TicksInLevel);

            return h;
        }

        private static void Add(ref ulong h, byte value)
        {
            h ^= value;
            h *= Prime;
        }

        private static void Add(ref ulong h, int value)
        {
            unchecked
            {
                Add(ref h, (byte)value);
                Add(ref h, (byte)(value >> 8));
                Add(ref h, (byte)(value >> 16));
                Add(ref h, (byte)(value >> 24));
            }
        }

        private static void Add(ref ulong h, long value)
        {
            Add(ref h, (int)value);
            Add(ref h, (int)(value >> 32));
        }

        private static void Add(ref ulong h, sbyte value) => Add(ref h, unchecked((byte)value));

        private static void Add(ref ulong h, bool value) => Add(ref h, value ? (byte)1 : (byte)0);

        private static void Add(ref ulong h, float value) => Add(ref h, new FloatBits { Float = value }.Int);

        private static void Add(ref ulong h, Vector2 value)
        {
            Add(ref h, value.x);
            Add(ref h, value.y);
        }

        // Bits de um float sem alocar (BitConverter.SingleToInt32Bits não existe na API .NET Framework).
        [StructLayout(LayoutKind.Explicit)]
        private struct FloatBits
        {
            [FieldOffset(0)] public float Float;
            [FieldOffset(0)] public int Int;
        }

        private static void Add(ref ulong h, in BufferedPress press)
        {
            Add(ref h, press.Active);
            Add(ref h, press.Age);
            Add(ref h, press.PressTick);
        }
    }
}
