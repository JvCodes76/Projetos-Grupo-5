using Roguelike.Run;
using UnityEngine;

namespace Roguelike.Upgrades
{
    /// <summary>
    /// Ponto de extensão para efeitos especiais de upgrade que não cabem em StatModifier nem em AbilityFlags
    /// (ex.: futura "vida extra" Lendária). Nenhum efeito existe no MVP.
    /// Chamado por: RunState.AcquireUpgrade, uma vez por stack, DEPOIS de aplicar modificadores e flags.
    /// Invariantes: a subclasse é um asset compartilhado entre runs, então NÃO guarda estado da run em campos;
    /// todo estado vai para o RunState recebido. Não emite eventos do bus (é lógica de domínio).
    /// Subclasses concretas: um arquivo por classe, com o mesmo nome, e [CreateAssetMenu] em "Roguelike/Upgrade Effects/...".
    /// </summary>
    public abstract class UpgradeEffect : ScriptableObject
    {
        /// <summary>Aplica o efeito de um stack recém-adquirido ao estado da run.</summary>
        public abstract void OnAcquired(RunState run);
    }
}
