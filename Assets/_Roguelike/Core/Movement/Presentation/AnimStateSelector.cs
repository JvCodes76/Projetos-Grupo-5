using System;

namespace Roguelike.Movement
{
    /// <summary>Valor do parâmetro int "State" do Cyborg_Motor.controller (SPEC §10.2, DS-15). Nunca renumerar.</summary>
    public enum AnimState
    {
        Idle = 0,
        Run = 1,
        Rise = 2,
        Apex = 3,
        Fall = 4,
        Land = 5,
        WallSlide = 6,
        AirJump = 7,
        Dash = 8,
        GrapplePull = 9,
        Dead = 10,
    }

    /// <summary>
    /// Escolha do estado de animação a partir do snapshot (SPEC §10.2). Lógica pura: o PlayerAnimatorDriver só aplica.
    /// Usa a velocidade REAL (P17) e nunca olha de costas para a parede (o facing do motor já aponta para ela).
    /// </summary>
    public static class AnimStateSelector
    {
        /// <summary>|vx| abaixo disto = parado.</summary>
        public const float RunThreshold = 0.5f;

        /// <summary>|vy| até isto no ar = ápice.</summary>
        public const float ApexThreshold = 5f;

        /// <summary>Ticks de "Land" depois de pousar parado.</summary>
        public const int LandTicks = 6;

        /// <summary>Ticks de "AirJump" depois de um pulo aéreo.</summary>
        public const int AirJumpTicks = 18;

        /// <param name="ticksSinceLanded">Ticks desde o último PlayerLanded (grande se nunca).</param>
        /// <param name="ticksSinceAirJump">Ticks desde o último PlayerJumped(Air) (grande se nunca).</param>
        public static AnimState Select(in PlayerSnapshot snap, int ticksSinceLanded, int ticksSinceAirJump)
        {
            switch (snap.State)
            {
                case MotorStateId.Dead: return AnimState.Dead;
                case MotorStateId.Dash: return AnimState.Dash;
                case MotorStateId.Grapple: return AnimState.GrapplePull;
            }

            if (snap.WallSlideSide != 0) return AnimState.WallSlide;

            float vx = Math.Abs(snap.Velocity.x);
            if (snap.Grounded)
            {
                // Pousar correndo = Run no mesmo frame (vence o Land).
                if (vx > RunThreshold) return AnimState.Run;
                return ticksSinceLanded < LandTicks ? AnimState.Land : AnimState.Idle;
            }

            if (ticksSinceAirJump < AirJumpTicks) return AnimState.AirJump;

            float vy = snap.Velocity.y;
            if (vy > ApexThreshold) return AnimState.Rise;
            if (vy < -ApexThreshold) return AnimState.Fall;
            return AnimState.Apex;
        }

        /// <summary>Velocidade do clipe de corrida: clamp(|vx| / MaxSpeed, 0,5, 1,5).</summary>
        public static float RunSpeedMultiplier(float vx, float maxSpeed)
        {
            if (!(maxSpeed > 0f)) return 1f;
            return Math.Min(Math.Max(Math.Abs(vx) / maxSpeed, 0.5f), 1.5f);
        }
    }
}
