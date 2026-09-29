using Roguelike.Movement;

namespace Roguelike.Events
{
    /// <summary>
    /// Destino dos eventos do movimento (SPEC §9.2): EventBusMovementEvents (EventBus&lt;T&gt;) no jogo, ou
    /// LocalMovementEvents (eventos C# no próprio prefab) se o bus não estiver disponível. A troca é uma linha.
    /// </summary>
    public interface IMovementEvents
    {
        void Raise<T>(in T evt) where T : struct, IEvent;
    }

    /// <summary>
    /// Converte os fatos de um tick (<see cref="TickEvents"/>) em eventos, na ordem fixa do SPEC §9.2:
    /// Died → FellOut → Respawned → GrappleReleased → WallSlideChanged(fim) → Landed → AbilityRefilled → Jumped →
    /// WallJumped → Dashed → GrappleFired → GrappleAttached → WallSlideChanged(início) → ActionDenied.
    /// Síncrono: os ouvintes de feedback reagem no mesmo frame do tick (RF-53). Não aloca.
    /// </summary>
    public static class MovementEventPublisher
    {
        public static void Publish(in TickEvents ev, IMovementEvents sink)
        {
            if (sink == null || ev.Flags == MovementEventFlags.None) return;
            long tick = ev.Tick;

            if (ev.Has(MovementEventFlags.Died))
                sink.Raise(new PlayerDied(ev.DeathCause, ev.DeathPosition, tick));
            if (ev.Has(MovementEventFlags.FellOut))
                sink.Raise(new PlayerFellOut(ev.FellOutPosition, ev.FellOutLastSafeGround, tick));
            if (ev.Has(MovementEventFlags.Respawned))
                sink.Raise(new PlayerRespawned(ev.RespawnPosition, ev.RespawnReason, tick));
            if (ev.Has(MovementEventFlags.GrappleReleased))
                sink.Raise(new PlayerGrappleReleased(ev.GrappleLaunchVelocity, ev.GrappleReleaseReason, tick));
            if (ev.Has(MovementEventFlags.WallSlideEnded))
                sink.Raise(new PlayerWallSlideChanged(false, (WallSide)ev.WallSlideEndSide, tick));
            if (ev.Has(MovementEventFlags.Landed))
                sink.Raise(new PlayerLanded(ev.LandImpactSpeed, ev.LandAirTime, ev.LandPosition, tick));
            if (ev.Has(MovementEventFlags.AirJumpRefilled))
                sink.Raise(new PlayerAbilityRefilled(MovementResource.AirJump, ev.AirJumpRefillAmount, tick));
            if (ev.Has(MovementEventFlags.DashRefilled))
                sink.Raise(new PlayerAbilityRefilled(MovementResource.Dash, ev.DashRefillAmount, tick));
            if (ev.Has(MovementEventFlags.Jumped))
                sink.Raise(new PlayerJumped(ev.JumpKind, ev.JumpFromBuffer, ev.JumpPosition, ev.JumpVelocity, tick));
            if (ev.Has(MovementEventFlags.WallJumped))
                sink.Raise(new PlayerWallJumped((WallSide)ev.WallJumpSide, ev.WallJumpNeutral, ev.WallJumpPosition, tick));
            if (ev.Has(MovementEventFlags.Dashed))
                sink.Raise(new PlayerDashed(ev.DashDirection, ev.DashChargesLeft, ev.DashPosition, tick));
            if (ev.Has(MovementEventFlags.GrappleFired))
                sink.Raise(new PlayerGrappleFired(ev.GrappleTarget, tick));
            if (ev.Has(MovementEventFlags.GrappleAttached))
                sink.Raise(new PlayerGrappleAttached(ev.GrappleAnchor, tick));
            if (ev.Has(MovementEventFlags.WallSlideStarted))
                sink.Raise(new PlayerWallSlideChanged(true, (WallSide)ev.WallSlideStartSide, tick));
            if (ev.Has(MovementEventFlags.DashDenied))
                sink.Raise(new PlayerActionDenied(DeniedAction.Dash, ev.DashDenyReason, tick));
            if (ev.Has(MovementEventFlags.AirJumpDenied))
                sink.Raise(new PlayerActionDenied(DeniedAction.AirJump, ev.AirJumpDenyReason, tick));
            if (ev.Has(MovementEventFlags.GrappleDenied))
                sink.Raise(new PlayerActionDenied(DeniedAction.Grapple, ev.GrappleDenyReason, tick));
        }
    }
}
