using System;
using Roguelike.Simulation;

namespace Roguelike.Movement
{
    /// <summary>
    /// Converte amostras por frame em <see cref="TickInput"/> por tick sem perder apertos (SPEC §4.3, RF-62, RF-63).
    /// Garantias:
    /// - todo frame com aperto gera exatamente um tick com Pressed (até 2 apertos pendentes por botão; o excedente,
    ///   fisicamente improvável entre dois ticks, é descartado);
    /// - apertar e soltar antes do tick → tick com Pressed = true e Held = false (pulo mínimo);
    /// - vários ticks no mesmo frame: só o primeiro recebe o Pressed; todos recebem o Held;
    /// - depois de <see cref="Rearm"/>, um botão que já estava segurado é ignorado até ser solto.
    /// </summary>
    public sealed class InputSampler : IInputSource
    {
        private const int ButtonCount = 4;
        private const int MaxPendingPresses = 2;

        private readonly byte[] pending = new byte[ButtonCount];
        private ButtonBits held;
        private ButtonBits needsRelease;
        private sbyte moveX;
        private sbyte moveY;

        public InputSampler(float deadzone = 0.3f)
        {
            Deadzone = deadzone;
        }

        /// <summary>Zona morta dos eixos (perfil: StickDeadzone).</summary>
        public float Deadzone { get; set; }

        /// <summary>Apertos descartados por excesso na fila (diagnóstico).</summary>
        public int DroppedPresses { get; private set; }

        public void PushFrame(in FrameSample s)
        {
            for (int b = 0; b < ButtonCount; b++)
            {
                var bit = (ButtonBits)(1 << b);
                if ((s.ReleasedThisFrame & bit) != 0 || (s.HeldNow & bit) == 0)
                {
                    needsRelease &= ~bit;
                }

                if ((s.PressedThisFrame & bit) != 0 && (needsRelease & bit) == 0)
                {
                    if (pending[b] < MaxPendingPresses) pending[b]++;
                    else DroppedPresses++;
                }
            }

            held = s.HeldNow & ~needsRelease;
            moveX = MathUtil.Quantize(s.RawX, Deadzone);
            moveY = MathUtil.Quantize(s.RawY, Deadzone);
        }

        public TickInput NextTick()
        {
            var t = new TickInput { MoveX = moveX, MoveY = moveY };
            for (int b = 0; b < ButtonCount; b++)
            {
                var bit = (ButtonBits)(1 << b);
                if (pending[b] > 0)
                {
                    t.Pressed |= bit;
                    pending[b]--;
                    // Se ainda há outro aperto na fila, este já foi solto: tick com Held = false (pulo mínimo).
                    if (pending[b] == 0 && (held & bit) != 0) t.Held |= bit;
                }
                else if ((held & bit) != 0)
                {
                    t.Held |= bit;
                }
            }

            return t;
        }

        /// <summary>
        /// Descarta apertos pendentes e passa a ignorar os botões fisicamente segurados até serem soltos
        /// (troca de cena, saída de pausa, confirmação de upgrade; RF-63).
        /// </summary>
        public void Rearm(ButtonBits physicallyHeld)
        {
            Array.Clear(pending, 0, ButtonCount);
            needsRelease = physicallyHeld;
            held = ButtonBits.None;
        }
    }
}
