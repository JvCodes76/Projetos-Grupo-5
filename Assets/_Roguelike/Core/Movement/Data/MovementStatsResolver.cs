using System;
using Roguelike.Simulation;
using Roguelike.Upgrades;

namespace Roguelike.Movement
{
    /// <summary>
    /// Monta o <see cref="MovementStats"/> efetivo a partir do perfil e de um <see cref="IMovementStatInput"/> (SPEC §8.2).
    /// Sem acumular: sempre parte do perfil + snapshot final, nunca do MovementStats anterior. Re-aplica os tetos do
    /// perfil (defesa em profundidade: o PlayerStats já os aplica).
    /// </summary>
    public static class MovementStatsResolver
    {
        public static MovementStats Resolve(MovementProfile profile, IMovementStatInput input, int tickRate = TickMath.DefaultTickRate)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            if (input == null) throw new ArgumentNullException(nameof(input));
            if (tickRate <= 0) throw new ArgumentOutOfRangeException(nameof(tickRate));

            var s = new MovementStats { TickRate = tickRate };

            s.MaxSpeed = Get(profile, input, StatType.MaxSpeed);
            s.GroundAccel = Get(profile, input, StatType.Acceleration);
            s.AirMult = profile.AirMultBase * Get(profile, input, StatType.AirControl);
            s.OverspeedDecay = Get(profile, input, StatType.OverspeedDecay);
            s.JumpHBoost = Get(profile, input, StatType.JumpHorizontalBoost);
            s.JumpHeight = Get(profile, input, StatType.JumpHeight);
            s.JumpSpeed = JumpSolver.SolveJumpSpeed(s.JumpHeight, profile, tickRate);
            s.AirJumpSpeed = JumpSolver.SolveJumpSpeed(s.JumpHeight * profile.AirJumpHeightRatio, profile, tickRate);
            s.WallSlideSpeed = Get(profile, input, StatType.WallSlideSpeed);
            s.GrappleRadius = Get(profile, input, StatType.GrappleRadius);
            s.GrappleLaunchSpeed = Get(profile, input, StatType.GrappleLaunchSpeed);

            s.MaxAirJumps = (int)Math.Round(Get(profile, input, StatType.MaxAirJumps), MidpointRounding.AwayFromZero);
            s.MaxDashes = (int)Math.Round(Get(profile, input, StatType.MaxDashes), MidpointRounding.AwayFromZero);

            AbilityFlags flags = AbilityFlags.None;
            if (input.Has(AbilityFlags.WallJump)) flags |= AbilityFlags.WallJump;
            if (input.Has(AbilityFlags.GrapplingHook)) flags |= AbilityFlags.GrapplingHook;
            if (input.Has(AbilityFlags.DashRefillOnWallJump)) flags |= AbilityFlags.DashRefillOnWallJump;
            if (input.Has(AbilityFlags.AirJumpRefillOnGrapple)) flags |= AbilityFlags.AirJumpRefillOnGrapple;
            s.Flags = flags;

            s.CoyoteTicks = TickMath.ToTicks(Get(profile, input, StatType.CoyoteTime), tickRate);
            s.GrappleCooldownTicks = TickMath.ToTicks(Get(profile, input, StatType.GrappleCooldown), tickRate);

            s.VarJumpTicks = TickMath.ToTicks(profile.VarJumpTime, tickRate);
            s.CeilingGraceTicks = TickMath.ToTicks(profile.CeilingVarJumpGrace, tickRate);
            s.JumpBufferTicks = TickMath.ToTicks(profile.JumpBufferTime, tickRate);
            s.DashBufferTicks = TickMath.ToTicks(profile.DashBufferTime, tickRate);
            s.GrappleBufferTicks = TickMath.ToTicks(profile.GrappleBufferTime, tickRate);
            s.RetentionTicks = TickMath.ToTicks(profile.WallSpeedRetentionTime, tickRate);
            s.WallCoyoteTicks = TickMath.ToTicks(profile.WallCoyoteTime, tickRate);
            s.WallJumpForceTicks = TickMath.ToTicks(profile.WallJumpForceTime, tickRate);
            s.DashTicks = TickMath.ToTicks(profile.DashTime, tickRate);
            s.DashCooldownTicks = TickMath.ToTicks(profile.DashCooldown, tickRate);
            s.DashRefillCooldownTicks = TickMath.ToTicks(profile.DashRefillCooldown, tickRate);
            s.DashFreezeTicks = TickMath.ToTicks(profile.DashFreeze, tickRate);
            s.GrappleMaxTicks = TickMath.ToTicks(profile.GrappleMaxTime, tickRate);
            s.GrappleStuckWindowTicks = TickMath.ToTicks(profile.GrappleStuckWindow, tickRate);
            s.RespawnDelayTicks = TickMath.ToTicks(profile.RespawnDelay, tickRate);
            s.ReturnToSpawnHoldTicks = TickMath.ToTicks(profile.ReturnToSpawnHold, tickRate);

            return s;
        }

        /// <summary>Kit base do perfil (sem upgrades, sem habilidades).</summary>
        public static MovementStats ResolveBase(MovementProfile profile, int tickRate = TickMath.DefaultTickRate)
        {
            return Resolve(profile, new KitStatInput(profile), tickRate);
        }

        private static float Get(MovementProfile profile, IMovementStatInput input, StatType stat)
        {
            return profile.ApplyCap(stat, input.Get(stat));
        }
    }
}
