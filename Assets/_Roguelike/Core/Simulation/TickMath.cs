using System;

namespace Roguelike.Simulation
{
    /// <summary>
    /// Conversões de tempo para ticks da simulação do jogador (SPEC §Convenções). Todo o código de gameplay converte
    /// segundos em ticks só por aqui, para que o arredondamento seja o mesmo em todo lugar.
    /// </summary>
    public static class TickMath
    {
        /// <summary>Frequência padrão da simulação (Hz).</summary>
        public const int DefaultTickRate = 60;

        /// <summary>Valor de saturação dos contadores "ticks desde X" (nunca estoura int em runs longas).</summary>
        public const int Never = 1_000_000;

        /// <summary>
        /// Duração positiva em ticks: max(1, round(s × taxa)), com arredondamento para longe do zero.
        /// Duração ≤ 0 (ou NaN) vira 0 ticks (ex.: uma tolerância desligada no perfil).
        /// Tabela a 60 Hz: 0,05 s = 3 · 0,06 = 4 · 0,1 = 6 · 0,15 = 9 · 0,16 = 10 · 0,2 = 12 · 0,3 = 18 · 0,5 = 30 · 3 = 180.
        /// </summary>
        public static int ToTicks(float seconds, int tickRate = DefaultTickRate)
        {
            if (!(seconds > 0f)) return 0;
            double ticks = Math.Round(seconds * (double)tickRate, MidpointRounding.AwayFromZero);
            return (int)Math.Max(1d, Math.Min(ticks, Never));
        }

        /// <summary>Teto de <paramref name="seconds"/> × taxa, no mínimo 1 (viagem da ponta do gancho).</summary>
        public static int CeilToTicks(float seconds, int tickRate = DefaultTickRate)
        {
            if (!(seconds > 0f)) return 1;
            // Folga contra erro de float: 0,25 s × 60 = 15,0000001 não pode virar 16.
            double ticks = Math.Ceiling(seconds * (double)tickRate - 1e-6);
            return (int)Math.Max(1d, Math.Min(ticks, Never));
        }

        /// <summary>Incrementa um contador "ticks desde X" saturando em <see cref="Never"/>.</summary>
        public static int SaturatingIncrement(int value)
        {
            return value >= Never ? Never : value + 1;
        }

        /// <summary>Segundos equivalentes a <paramref name="ticks"/>.</summary>
        public static float ToSeconds(long ticks, int tickRate = DefaultTickRate)
        {
            return (float)(ticks / (double)tickRate);
        }
    }
}
