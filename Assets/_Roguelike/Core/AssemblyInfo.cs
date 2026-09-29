using System.Runtime.CompilerServices;

// Os testes precisam de membros internal (ex.: EventBus<T>.Clear(), PlayerMotor.DebugSetBody).
[assembly: InternalsVisibleTo("Roguelike.Tests.EditMode")]
[assembly: InternalsVisibleTo("Roguelike.Tests.Levels")]
[assembly: InternalsVisibleTo("Roguelike.Tests.PlayMode")]

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
