using System;

namespace Roguelike.Run
{
    /// <summary>
    /// Relógio de uma fase (tarefa 3.1): conta para cima de 0 até o limite efetivo e expira uma única vez.
    /// Lógica pura; o adaptador é o LevelTimer (Assembly-CSharp), que chama <see cref="Tick"/> a cada frame e
    /// traduz o <see cref="LevelClockTick"/> em LevelTimeChanged/LevelTimeExpired.
    /// Invariantes: 0 ≤ ElapsedSeconds ≤ TimeLimit; DisplayedSeconds é múltiplo de 0,1 (ADR-13); Expired sai
    /// verdadeiro em no máximo um tick por Start; depois de expirar ou de Stop, Tick não muda nada.
    /// </summary>
    public sealed class LevelClock
    {
        // Folga (em décimos de segundo) do Quantize contra erro de float: absoluta + relativa ao valor.
        // Ex.: 0.29999998f (0,3 acumulado com erro) × 10 = 2.9999998 ⇒ ainda conta como 3 décimos.
        private const double QuantizeAbsoluteTolerance = 1e-3;
        private const double QuantizeRelativeTolerance = 1e-6;

        // Folga (s) para considerar o limite alcançado: somas de deltas em float (ex.: 3 × 0.1f) ficam
        // alguns ulps abaixo do limite em float e não podem deixar o relógio "quase expirado" por um frame.
        private const double ExpiryTolerance = 1e-5;

        // Acumulado em double: milhares de deltas pequenos somados em float perderiam precisão.
        private double elapsed;

        public bool IsRunning { get; private set; }

        public bool HasExpired { get; private set; }

        /// <summary>Limite efetivo (s) passado ao último <see cref="Start"/>; 0 antes do primeiro.</summary>
        public float TimeLimit { get; private set; }

        /// <summary>Decorrido exato (s) desde o Start; nunca passa do limite.</summary>
        public float ElapsedSeconds => (float)elapsed;

        /// <summary>Último valor quantizado (múltiplo de 0,1 s) devolvido por um tick; 0 logo depois do Start.</summary>
        public float DisplayedSeconds { get; private set; }

        /// <summary>
        /// Zera o relógio e começa a contar. O Start não gera tick: quem chama emite o valor inicial (0).
        /// Limite ≤ 0, NaN ou infinito ⇒ ArgumentOutOfRangeException (sem mudar o estado).
        /// </summary>
        public void Start(float effectiveTimeLimit)
        {
            if (!(effectiveTimeLimit > 0f) || float.IsInfinity(effectiveTimeLimit))
            {
                throw new ArgumentOutOfRangeException(nameof(effectiveTimeLimit), effectiveTimeLimit,
                    "O limite da fase deve ser finito e > 0.");
            }

            TimeLimit = effectiveTimeLimit;
            elapsed = 0d;
            DisplayedSeconds = 0f;
            HasExpired = false;
            IsRunning = true;
        }

        /// <summary>Para o relógio sem expirar (meta alcançada, morte, fim da run). Idempotente.</summary>
        public void Stop()
        {
            IsRunning = false;
        }

        /// <summary>
        /// Avança o relógio. deltaTime &lt; 0 ou NaN ⇒ ArgumentOutOfRangeException (em qualquer estado).
        /// Parado ⇒ tick vazio (nada mudou, nada expirou), com o DisplayedSeconds atual.
        /// Ao alcançar o limite: ElapsedSeconds = limite, DisplayedSeconds = Quantize(limite), Expired = true e o
        /// relógio para.
        /// </summary>
        public LevelClockTick Tick(float deltaTime)
        {
            if (!(deltaTime >= 0f))
            {
                throw new ArgumentOutOfRangeException(nameof(deltaTime), deltaTime,
                    "O deltaTime deve ser ≥ 0 e não pode ser NaN.");
            }

            if (!IsRunning)
            {
                return new LevelClockTick(false, DisplayedSeconds, false);
            }

            elapsed += deltaTime;

            bool expired = elapsed >= TimeLimit - ExpiryTolerance;
            float newDisplayed;
            if (expired)
            {
                elapsed = TimeLimit;
                newDisplayed = Quantize(TimeLimit);
                IsRunning = false;
                HasExpired = true;
            }
            else
            {
                newDisplayed = Quantize((float)elapsed);
            }

            bool displayChanged = newDisplayed != DisplayedSeconds;
            DisplayedSeconds = newDisplayed;
            return new LevelClockTick(displayChanged, newDisplayed, expired);
        }

        /// <summary>
        /// floor(segundos × 10) / 10, tolerante a erro de float (0.3f ⇒ 0.3f; 0.29999998f ⇒ 0.3f).
        /// Negativo ou NaN ⇒ 0.
        /// </summary>
        public static float Quantize(float seconds)
        {
            if (!(seconds > 0f)) return 0f;

            double tenths = seconds * 10d;
            double tolerance = QuantizeAbsoluteTolerance + tenths * QuantizeRelativeTolerance;
            return (float)(Math.Floor(tenths + tolerance) / 10d);
        }
    }

    /// <summary>Resultado de um <see cref="LevelClock.Tick"/>.</summary>
    public readonly struct LevelClockTick
    {
        public LevelClockTick(bool displayChanged, float displayedSeconds, bool expired)
        {
            DisplayChanged = displayChanged;
            DisplayedSeconds = displayedSeconds;
            Expired = expired;
        }

        /// <summary>O valor quantizado mudou neste tick (hora de emitir LevelTimeChanged).</summary>
        public bool DisplayChanged { get; }

        /// <summary>Valor quantizado depois do tick (múltiplo de 0,1 s).</summary>
        public float DisplayedSeconds { get; }

        /// <summary>O limite foi alcançado neste tick (verdadeiro uma única vez por Start).</summary>
        public bool Expired { get; }
    }
}
