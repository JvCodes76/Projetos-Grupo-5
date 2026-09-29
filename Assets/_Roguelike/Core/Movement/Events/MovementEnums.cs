namespace Roguelike.Movement
{
    /// <summary>Estado do controlador (SPEC §6). WallSlide é sub-modo de Normal.</summary>
    public enum MotorStateId : byte
    {
        Normal = 0,
        Dash = 1,
        Grapple = 2,
        Respawning = 3,
        Disabled = 4,
        Dead = 5,
    }

    /// <summary>Fase do dash (SPEC §7.6).</summary>
    public enum DashPhase : byte
    {
        None = 0,
        /// <summary>Tick do aperto: v = 0, freeze pedido; a direção é lida no primeiro tick depois do freeze.</summary>
        AwaitingDirection = 1,
        Moving = 2,
    }

    /// <summary>Fase do gancho (SPEC §7.7). Travel acontece dentro do estado Normal; Pull é o estado Grapple.</summary>
    public enum GrapplePhase : byte
    {
        None = 0,
        Travel = 1,
        Pull = 2,
    }

    /// <summary>Tipo de pulo do chão (payload de PlayerJumped).</summary>
    public enum JumpKind : byte
    {
        Ground = 0,
        Coyote = 1,
        Air = 2,
        /// <summary>Pulo que cancela o puxão do gancho (regra do pulo do chão, sem gastar aéreo).</summary>
        GrappleCancel = 3,
    }

    public enum WallSide : sbyte
    {
        Left = -1,
        Right = 1,
    }

    public enum MovementResource : byte
    {
        AirJump = 0,
        Dash = 1,
    }

    public enum DeniedAction : byte
    {
        Dash = 0,
        AirJump = 1,
        Grapple = 2,
    }

    public enum DenyReason : byte
    {
        /// <summary>Sem carga (aperto expirou no buffer).</summary>
        NoCharge = 0,
        /// <summary>Em cooldown (aperto expirou no buffer).</summary>
        Cooldown = 1,
        /// <summary>Habilidade não liberada (na hora, sem buffer).</summary>
        Locked = 2,
        /// <summary>Nenhum alvo de gancho alcançável (na hora).</summary>
        NoTarget = 3,
    }

    public enum RespawnReason : byte
    {
        FellOut = 0,
        ReturnToSpawn = 1,
    }

    public enum GrappleReleaseReason : byte
    {
        Launched = 0,
        CancelledByJump = 1,
        CancelledByDash = 2,
        Interrupted = 3,
    }
}
