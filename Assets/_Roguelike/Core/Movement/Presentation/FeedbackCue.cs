using System;
using Roguelike.Events;

namespace Roguelike.Movement
{
    /// <summary>Cues de feedback do Cyborg (áudio, partículas, rumble). Valores fixos: o AudioCueSet serializa o int.</summary>
    public enum FeedbackCue
    {
        None = 0,
        Jump = 1,
        AirJump = 2,
        WallJump = 3,
        Land = 4,
        LandHard = 5,
        WallSlideLoop = 6,
        Dash = 7,
        GrappleFire = 8,
        GrappleAttach = 9,
        GrappleRelease = 10,
        Refill = 11,
        Denied = 12,
        Respawn = 13,
        Death = 14,
        Footstep = 15,
        FellOut = 16,
    }

    /// <summary>
    /// Mapa evento → cue (SPEC §10.3). Lógica pura para o teste "todo evento da §9 tem cue de áudio"; o
    /// PlayerFeedback usa as mesmas funções.
    /// </summary>
    public static class FeedbackCueMap
    {
        /// <summary>Tipos de evento de movimento que precisam de cue (PlayerDied incluído; MovementStatsReported não).</summary>
        public static readonly Type[] EventsWithCue =
        {
            typeof(PlayerJumped), typeof(PlayerWallJumped), typeof(PlayerLanded), typeof(PlayerDashed),
            typeof(PlayerWallSlideChanged), typeof(PlayerAbilityRefilled), typeof(PlayerActionDenied),
            typeof(PlayerGrappleFired), typeof(PlayerGrappleAttached), typeof(PlayerGrappleReleased),
            typeof(PlayerFellOut), typeof(PlayerRespawned), typeof(PlayerDied),
        };

        /// <summary>Cue principal de um tipo de evento (sem olhar o payload).</summary>
        public static FeedbackCue CueFor(Type eventType)
        {
            if (eventType == typeof(PlayerJumped)) return FeedbackCue.Jump;
            if (eventType == typeof(PlayerWallJumped)) return FeedbackCue.WallJump;
            if (eventType == typeof(PlayerLanded)) return FeedbackCue.Land;
            if (eventType == typeof(PlayerDashed)) return FeedbackCue.Dash;
            if (eventType == typeof(PlayerWallSlideChanged)) return FeedbackCue.WallSlideLoop;
            if (eventType == typeof(PlayerAbilityRefilled)) return FeedbackCue.Refill;
            if (eventType == typeof(PlayerActionDenied)) return FeedbackCue.Denied;
            if (eventType == typeof(PlayerGrappleFired)) return FeedbackCue.GrappleFire;
            if (eventType == typeof(PlayerGrappleAttached)) return FeedbackCue.GrappleAttach;
            if (eventType == typeof(PlayerGrappleReleased)) return FeedbackCue.GrappleRelease;
            if (eventType == typeof(PlayerFellOut)) return FeedbackCue.FellOut;
            if (eventType == typeof(PlayerRespawned)) return FeedbackCue.Respawn;
            if (eventType == typeof(PlayerDied)) return FeedbackCue.Death;
            return FeedbackCue.None;
        }

        public static FeedbackCue ForJump(JumpKind kind) => kind == JumpKind.Air ? FeedbackCue.AirJump : FeedbackCue.Jump;

        /// <summary>Pouso forte a partir de 50 % da queda máxima (8,5 u/s com 17).</summary>
        public static FeedbackCue ForLanding(float impactSpeed, float hardLandingSpeed)
        {
            return impactSpeed >= hardLandingSpeed ? FeedbackCue.LandHard : FeedbackCue.Land;
        }

        /// <summary>Só NoCharge e Cooldown soam (Locked/NoTarget ficam em silêncio, SPEC §9.1).</summary>
        public static FeedbackCue ForDenied(DenyReason reason)
        {
            return reason == DenyReason.NoCharge || reason == DenyReason.Cooldown ? FeedbackCue.Denied : FeedbackCue.None;
        }
    }
}
