using System.Collections.Generic;

namespace Roguelike.Upgrades
{
    /// <summary>
    /// Gera as ofertas de upgrade entre uma fase e a próxima (§3.5). Chamado pelo RunFlow; o RunManager emite o
    /// resultado em UpgradeOffersGenerated. É interface para o RunFlow poder ser testado com um gerador falso.
    /// Contrato:
    /// - Determinístico: mesmos argumentos (inclusive a ordem do Pool e o estado do Inventory) ⇒ mesmas ofertas.
    /// - Retorna exatamente request.OfferCount itens; slot sem candidato = UpgradeOffer.Empty(raridadeSorteada).
    /// - Nunca repete o mesmo upgrade na mesma rodada; nunca oferece upgrade inelegível
    ///   (stacks ≥ MaxStacks ou pré-requisito com 0 stacks).
    /// - Não altera o Inventory nem nenhum asset.
    /// Implementação padrão: <see cref="UpgradeOfferGenerator"/> (tarefa 2.2).
    /// </summary>
    public interface IUpgradeOfferGenerator
    {
        IReadOnlyList<UpgradeOffer> Generate(in UpgradeOfferRequest request);
    }
}
