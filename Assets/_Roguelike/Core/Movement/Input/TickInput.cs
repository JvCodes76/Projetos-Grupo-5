using System;
using UnityEngine;

namespace Roguelike.Movement
{
    /// <summary>Botões do jogador (bits). Valores fixos: gravações de input guardam o byte.</summary>
    [Flags]
    public enum ButtonBits : byte
    {
        None = 0,
        Jump = 1,
        Dash = 2,
        Grapple = 4,
        Restart = 8,
        GravityFlip = 16,
    }

    /// <summary>
    /// Input de um tick (SPEC §2.3): determinístico, 6 bytes, gravável. Eixos já quantizados (−1, 0, +1; Q13) e a
    /// mira do gancho como ângulo de 16 bits (<see cref="AimCodec"/>).
    /// </summary>
    [Serializable]
    public struct TickInput : IEquatable<TickInput>
    {
        /// <summary>−1, 0, +1.</summary>
        public sbyte MoveX;

        /// <summary>−1, 0, +1 (↑ = +1).</summary>
        public sbyte MoveY;

        /// <summary>Aperto entregue neste tick.</summary>
        public ButtonBits Pressed;

        /// <summary>Estado lógico do botão neste tick.</summary>
        public ButtonBits Held;

        /// <summary>Mira do gancho (<see cref="AimCodec"/>): 0 = sem mira explícita (mouse parado/sem analógico).</summary>
        public ushort Aim;

        public TickInput(sbyte moveX, sbyte moveY, ButtonBits pressed, ButtonBits held, ushort aim = 0)
        {
            MoveX = moveX;
            MoveY = moveY;
            Pressed = pressed;
            Held = held;
            Aim = aim;
        }

        public bool IsPressed(ButtonBits bit) => (Pressed & bit) != 0;

        public bool IsHeld(ButtonBits bit) => (Held & bit) != 0;

        public bool Equals(TickInput other)
        {
            return MoveX == other.MoveX && MoveY == other.MoveY && Pressed == other.Pressed && Held == other.Held
                && Aim == other.Aim;
        }

        public override bool Equals(object obj) => obj is TickInput other && Equals(other);

        public override int GetHashCode()
        {
            int buttons = (MoveX & 0xFF) | ((MoveY & 0xFF) << 8) | ((int)Pressed << 16) | ((int)Held << 24);
            return buttons ^ (Aim * 397);
        }

        public override string ToString() => $"({MoveX},{MoveY}) P={Pressed} H={Held} A={Aim}";
    }

    /// <summary>
    /// Leitura do input num frame (SPEC §4.3): eixos brutos e bordas dos botões naquele frame.
    /// Montado pelo PlayerInputReader (adaptador) e entregue ao <see cref="InputSampler"/>.
    /// </summary>
    public struct FrameSample
    {
        public float RawX;
        public float RawY;
        public ButtonBits PressedThisFrame;
        public ButtonBits ReleasedThisFrame;
        public ButtonBits HeldNow;

        /// <summary>Direção da mira do gancho (mouse ou analógico direito; não normalizada). Zero = sem mira.</summary>
        public Vector2 Aim;
    }

    /// <summary>Fonte de input por tick: InputSampler (jogo), InputRecording (replay) ou roteiros de teste.</summary>
    public interface IInputSource
    {
        TickInput NextTick();
    }
}
