using UnityEngine;

namespace Roguelike.Movement
{
    /// <summary>
    /// Log do movimento no formato "[Movement] - …" (PLANO §7). <see cref="Info"/> só escreve com <see cref="Enabled"/>
    /// (ligado pelo overlay de debug); nenhum log por tick em build normal. Quem chama deve checar
    /// <see cref="Enabled"/> antes de montar a string, para não alocar (RNF-09). Avisos sempre saem.
    /// </summary>
    public static class MovementLog
    {
        public const string Prefix = "[Movement] - ";

        /// <summary>Liga os logs informativos (overlay F1).</summary>
        public static bool Enabled { get; set; }

        public static void Info(string message)
        {
            if (!Enabled) return;
            Debug.Log(Prefix + message);
        }

        public static void Warn(string message)
        {
            Debug.LogWarning(Prefix + message);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Enabled = false;
        }
    }
}
