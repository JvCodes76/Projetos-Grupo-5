namespace Roguelike.Upgrades
{
    /// <summary>
    /// Leitura dos upgrades já adquiridos na run, do jeito que o gerador de ofertas precisa (stacks e pré-requisitos).
    /// Implementado por: RunState. Mantém Roguelike.Upgrades sem depender dos detalhes da run.
    /// </summary>
    public interface IUpgradeInventory
    {
        /// <summary>Quantos stacks de <paramref name="upgrade"/> já foram adquiridos (0 se nenhum).</summary>
        int GetStacks(UpgradeDefinition upgrade);
    }
}
