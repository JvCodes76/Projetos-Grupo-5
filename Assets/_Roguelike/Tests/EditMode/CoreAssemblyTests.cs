using NUnit.Framework;

namespace Roguelike.Tests
{
    /// <summary>
    /// Smoke test da infraestrutura: Roguelike.Core compila, é carregado e expõe
    /// seus membros internal para este assembly de testes.
    /// </summary>
    public class CoreAssemblyTests
    {
        [Test]
        public void CoreAssembly_IsLoadedWithExpectedName()
        {
            var assembly = typeof(CoreAssembly).Assembly;

            Assert.AreEqual(CoreAssembly.Name, assembly.GetName().Name);
        }
    }
}
