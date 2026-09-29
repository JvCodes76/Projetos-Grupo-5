using System;
using System.Globalization;

namespace Roguelike.Run
{
    /// <summary>
    /// Formatação de tempo para a UI da run (HUD, resultado de fase, fim de run). Lógica pura, sem UnityEngine.
    /// Consumido por: RunHudView, LevelResultView, RunEndView (tarefa 3.2).
    /// </summary>
    public static class TimeFormat
    {
        /// <summary>
        /// Formata <paramref name="seconds"/> como "mm:ss.d" (ou "h:mm:ss.d" a partir de 1 hora), com o décimo
        /// truncado (nunca arredondado). Negativo é tratado como 0; NaN vira "--:--.-". Cultura invariante.
        /// </summary>
        public static string Format(float seconds)
        {
            if (float.IsNaN(seconds))
            {
                return "--:--.-";
            }

            if (seconds < 0f)
            {
                seconds = 0f;
            }

            // Décimos de segundo em double. O float tem erro de representação (ex.: 18.4f é armazenado como
            // 18.399999...): se o valor está muito perto de um inteiro de décimos, assume que é esse inteiro
            // (corrige o erro); senão trunca para baixo normalmente (não arredonda um valor genuinamente
            // fracionário, ex.: 18.49 tem que truncar para 18.4, não virar 18.5). O epsilon cresce com a
            // magnitude porque o erro de representação do float também cresce.
            double tenthsValue = (double)seconds * 10.0;
            double nearestTenths = Math.Round(tenthsValue, MidpointRounding.AwayFromZero);
            double epsilon = Math.Max(0.0001, Math.Abs(tenthsValue) * 0.000001);
            double totalTenths = Math.Abs(tenthsValue - nearestTenths) < epsilon
                ? nearestTenths
                : Math.Floor(tenthsValue);

            if (totalTenths < 0d)
            {
                totalTenths = 0d;
            }

            long tenths = (long)totalTenths;
            long tenthsDigit = tenths % 10;
            long totalWholeSeconds = tenths / 10;

            long hours = totalWholeSeconds / 3600;
            long minutes = (totalWholeSeconds % 3600) / 60;
            long secs = totalWholeSeconds % 60;

            CultureInfo culture = CultureInfo.InvariantCulture;

            if (hours >= 1)
            {
                return string.Format(
                    culture,
                    "{0}:{1:D2}:{2:D2}.{3}",
                    hours,
                    minutes,
                    secs,
                    tenthsDigit);
            }

            return string.Format(
                culture,
                "{0:D2}:{1:D2}.{2}",
                minutes,
                secs,
                tenthsDigit);
        }
    }
}
