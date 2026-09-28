using System.Runtime.CompilerServices;

// Os testes EditMode precisam de membros internal (ex.: EventBus<T>.Clear()).
[assembly: InternalsVisibleTo("Roguelike.Tests.EditMode")]

namespace Roguelike
{
    /// <summary>
    /// Marcador do assembly Roguelike.Core (lógica pura do roguelike, sem dependência do Assembly-CSharp).
    /// </summary>
    internal static class CoreAssembly
    {
        public const string Name = "Roguelike.Core";
    }
}
