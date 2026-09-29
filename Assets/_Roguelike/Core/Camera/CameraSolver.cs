using System;
using Roguelike.Movement;
using Roguelike.Simulation;
using UnityEngine;

namespace Roguelike.Cameras
{
    /// <summary>Entrada da câmera num tick: o jogador depois do tick (SPEC §11).</summary>
    public readonly struct CameraInput
    {
        public CameraInput(Vector2 feet, Vector2 velocity, bool grounded, MotorStateId state, float maxFall,
            float bodyHeight, bool teleported)
        {
            Feet = feet;
            Velocity = velocity;
            Grounded = grounded;
            State = state;
            MaxFall = maxFall;
            BodyHeight = bodyHeight;
            Teleported = teleported;
        }

        public Vector2 Feet { get; }
        public Vector2 Velocity { get; }
        public bool Grounded { get; }
        public MotorStateId State { get; }
        public float MaxFall { get; }
        public float BodyHeight { get; }
        public bool Teleported { get; }

        public static CameraInput FromSnapshot(in PlayerSnapshot snap)
        {
            return new CameraInput(snap.Position, snap.Velocity, snap.Grounded, snap.State, snap.MaxFall,
                snap.BodySize.y, snap.Teleported);
        }

        /// <summary>Alvo legado (sem PlayerController): segue o ponto, sem look-ahead nem snapping de plataforma.</summary>
        public static CameraInput ForLegacyTarget(Vector2 point, bool teleported)
        {
            return new CameraInput(point, Vector2.zero, true, MotorStateId.Normal, 17f, 0f, teleported);
        }
    }

    /// <summary>
    /// Câmera simulada no tick e interpolada no render (DS-14): a mesma trajetória dá as mesmas posições a qualquer
    /// framerate (RF-46). Zona morta + look-ahead com rampa e sem ré em X; platform snapping + look-down em Y;
    /// restrição dura de pés e cabeça na tela; clamp pela CameraBoundary e pelo plano de queda (RF-50). Lógica pura.
    /// </summary>
    public sealed class CameraSolver
    {
        private readonly CameraProfile p;
        private float baseX;
        private float lookAhead;
        private float targetX;
        private float anchorY;
        private bool followY;
        private bool initialized;

        public CameraSolver(CameraProfile profile)
        {
            p = profile != null ? profile : throw new ArgumentNullException(nameof(profile));
        }

        /// <summary>Posição (centro) depois do último tick.</summary>
        public Vector2 Position { get; private set; }

        /// <summary>Posição antes do último tick (interpolação).</summary>
        public Vector2 PreviousPosition { get; private set; }

        /// <summary>Meia altura visível (ortho size).</summary>
        public float HalfHeight { get; private set; } = 5.625f;

        /// <summary>Meia largura visível.</summary>
        public float HalfWidth { get; private set; } = 10f;

        public bool IsFollowingY => followY;

        public float LookAhead => lookAhead;

        /// <summary>Alvo em X depois da regra "sem ré" (overlay e testes).</summary>
        public float TargetX => targetX;

        /// <summary>Define a área visível a partir do aspecto da tela; devolve o ortho size.</summary>
        public float SetAspect(float aspect)
        {
            HalfHeight = p.OrthographicSizeFor(aspect);
            HalfWidth = HalfHeight * (aspect > 0f ? aspect : 16f / 9f);
            return HalfHeight;
        }

        /// <summary>Corte: centraliza no jogador sem suavização (spawn, respawn, RF-42).</summary>
        public void Snap(in CameraInput i, in CameraBounds b)
        {
            baseX = i.Feet.x;
            targetX = i.Feet.x;
            lookAhead = 0f;
            anchorY = i.Feet.y;
            followY = false;
            var pos = new Vector2(i.Feet.x, i.Feet.y + p.FocusOffsetY);
            pos = ApplyHardConstraints(pos, i, b);
            Position = pos;
            PreviousPosition = pos;
            initialized = true;
        }

        public void Tick(in CameraInput i, in CameraBounds b, float dt)
        {
            if (!initialized || i.Teleported)
            {
                Snap(i, b);
                return;
            }

            PreviousPosition = Position;
            Vector2 pos = Position;

            // X: zona morta + look-ahead com rampa, sem "câmera de ré".
            if (i.Feet.x > baseX + p.DeadZoneX) baseX = i.Feet.x - p.DeadZoneX;
            else if (i.Feet.x < baseX - p.DeadZoneX) baseX = i.Feet.x + p.DeadZoneX;

            float vx = i.Velocity.x;
            float lookTarget = Math.Abs(vx) < p.LookAheadMinSpeed
                ? lookAhead
                : MathUtil.Clamp(vx * p.LookAheadTime, -p.LookAheadMax, p.LookAheadMax);
            lookAhead = MathUtil.Approach(lookAhead, lookTarget, p.LookAheadRate * dt);
            float tx = baseX + lookAhead;
            if ((vx >= p.LookAheadMinSpeed && tx < targetX) || (vx <= -p.LookAheadMinSpeed && tx > targetX)) tx = targetX;
            targetX = tx;

            // Y: platform snapping + look-down.
            float halfMaxFall = 0.5f * i.MaxFall;
            if (i.Grounded)
            {
                anchorY = i.Feet.y;
                followY = false;
            }
            else if (!followY && (i.Feet.y < anchorY - p.FallFollowDistance
                         || i.Feet.y > anchorY + p.RiseWindow
                         || (i.Velocity.y < -halfMaxFall && i.Feet.y < anchorY - p.FastFallFollowDistance)
                         || i.State == MotorStateId.Grapple))
            {
                followY = true;
            }

            float lookDown = followY ? -Math.Min(p.LookDownMax, Math.Max(0f, -i.Velocity.y - halfMaxFall) * p.LookDownGain) : 0f;
            float ty = (followY ? i.Feet.y : anchorY) + p.FocusOffsetY + lookDown;

            // Suavização exponencial por eixo, no tick.
            pos.x += (targetX - pos.x) * (1f - (float)Math.Pow(2.0, -dt / p.HalfLifeX));
            float halfLife = ty < pos.y ? p.HalfLifeYDown : p.HalfLifeYUp;
            pos.y += (ty - pos.y) * (1f - (float)Math.Pow(2.0, -dt / halfLife));

            Position = ApplyHardConstraints(pos, i, b);
        }

        // Pés e cabeça na tela (vence a suavização), depois limites da fase e plano de queda.
        private Vector2 ApplyHardConstraints(Vector2 pos, in CameraInput i, in CameraBounds b)
        {
            float lower = i.Feet.y + i.BodyHeight + p.HeadMargin - HalfHeight;
            float upper = i.Feet.y - p.FeetMargin + HalfHeight;
            if (lower <= upper) pos.y = MathUtil.Clamp(pos.y, lower, upper);

            if (b.HasBounds)
            {
                if (b.MinX <= b.MaxX) pos.x = MathUtil.Clamp(pos.x, b.MinX, b.MaxX);
                float minY = b.MinY;
                if (!float.IsNegativeInfinity(b.KillPlaneY)) minY = Math.Max(minY, b.KillPlaneY + HalfHeight);
                if (minY <= b.MaxY) pos.y = MathUtil.Clamp(pos.y, minY, b.MaxY);
                else pos.y = b.MaxY;
            }

            return pos;
        }
    }
}
