using UnityEngine;

namespace Roguelike.Movement
{
    /// <summary>
    /// Leitura do jogador para apresentação e câmera (SPEC §2.1): cópia do que as views precisam depois de um tick.
    /// As views nunca escrevem no núcleo; só leem isto e os eventos.
    /// </summary>
    public readonly struct PlayerSnapshot
    {
        public PlayerSnapshot(in MotorState s, in MovementStats st, Vector2 bodySize, float maxFall, long tick)
        {
            Tick = tick;
            Position = s.Position;
            PrevPosition = s.PrevPosition;
            Velocity = s.Velocity;
            State = s.State;
            Grounded = s.Grounded;
            Facing = s.Facing;
            WallSlideSide = s.WallSlideSide;
            AirJumpsLeft = s.AirJumpsLeft;
            MaxAirJumps = st.MaxAirJumps;
            DashesLeft = s.DashesLeft;
            MaxDashes = st.MaxDashes;
            MaxSpeed = st.MaxSpeed;
            MaxFall = maxFall;
            BodySize = bodySize;
            Teleported = s.Teleported;
            GrapplePhase = s.GrapplePhase;
            GrappleAnchor = s.GrappleAnchor;
            HookTip = s.GrapplePhase == GrapplePhase.Travel && s.HookTravelTotal > 0
                ? Vector2.Lerp(s.HookOrigin, s.GrappleAnchor, 1f - s.HookTravelTicksLeft / (float)s.HookTravelTotal)
                : s.GrappleAnchor;
            HasGrappleTarget = s.HasGrappleTarget;
            GrappleTarget = s.GrappleTarget;
            GrappleUnlocked = st.Has(Roguelike.Upgrades.AbilityFlags.GrapplingHook);
            Visible = s.State != MotorStateId.Respawning;
        }

        public long Tick { get; }
        /// <summary>Pés do jogador (pose de simulação).</summary>
        public Vector2 Position { get; }
        public Vector2 PrevPosition { get; }
        public Vector2 Velocity { get; }
        public MotorStateId State { get; }
        public bool Grounded { get; }
        public sbyte Facing { get; }
        public sbyte WallSlideSide { get; }
        public int AirJumpsLeft { get; }
        public int MaxAirJumps { get; }
        public int DashesLeft { get; }
        public int MaxDashes { get; }
        public float MaxSpeed { get; }
        public float MaxFall { get; }
        public Vector2 BodySize { get; }
        /// <summary>O último tick foi um teleporte (spawn, respawn): a câmera corta, a interpolação não arrasta.</summary>
        public bool Teleported { get; }
        public GrapplePhase GrapplePhase { get; }
        public Vector2 GrappleAnchor { get; }
        /// <summary>Posição da ponta do gancho (interpolada na viagem; a âncora no puxão).</summary>
        public Vector2 HookTip { get; }
        /// <summary>Retícula do gancho (preview da mira).</summary>
        public bool HasGrappleTarget { get; }
        public Vector2 GrappleTarget { get; }
        public bool GrappleUnlocked { get; }
        /// <summary>O sprite deve aparecer (oculto durante o respawn).</summary>
        public bool Visible { get; }
        public bool IsWallSliding => WallSlideSide != 0;
        public Vector2 BodyCenter => Position + new Vector2(0f, BodySize.y * 0.5f);
    }
}
