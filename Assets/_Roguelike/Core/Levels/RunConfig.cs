using System.Collections.Generic;
using Roguelike.Run;
using Roguelike.Stats;
using Roguelike.Upgrades;
using UnityEngine;

namespace Roguelike.Levels
{
    /// <summary>
    /// Configuração de uma run: fases em ordem, pool de upgrades, tabela de raridade, kit base e parâmetros de UI/debug.
    /// Referenciado pelo RunManager (3.1); passado a RunFlow.StartRun e no evento RunStarted.
    /// Invariantes (DataValidationTests, 3.3): Levels não vazio e sem nulos; RarityTable e BaseStats não nulos;
    /// UpgradePool sem nulos nem repetidos; OfferCount ≥ 1.
    /// </summary>
    [CreateAssetMenu(menuName = "Roguelike/Run Config", fileName = "RunConfig")]
    public sealed class RunConfig : ScriptableObject
    {
        [Tooltip("Fases da run, na ordem em que são jogadas (D4).")]
        [SerializeField] private List<LevelDefinition> levels = new List<LevelDefinition>();

        [SerializeField] private RarityTable rarityTable;

        [Tooltip("Todos os upgrades que podem ser oferecidos. A ordem faz parte do determinismo das ofertas.")]
        [SerializeField] private List<UpgradeDefinition> upgradePool = new List<UpgradeDefinition>();

        [SerializeField] private PlayerBaseStats baseStats;

        [Tooltip("Ofertas por escolha de upgrade (§1: 3).")]
        [SerializeField, Min(1)] private int offerCount = 3;

        [Tooltip("Limiares de desempenho para as notas S/A/B (§3.5: 0,9 / 0,66 / 0,33).")]
        [SerializeField] private GradeThresholds gradeThresholds = GradeThresholds.Default;

        [Header("Debug")]
        [Tooltip("Se ligado, toda run usa Fixed Seed (reproduzir ofertas em playtest/QA).")]
        [SerializeField] private bool useFixedSeed;

        [SerializeField] private int fixedSeed;

        public IReadOnlyList<LevelDefinition> Levels => levels;
        public RarityTable RarityTable => rarityTable;
        public IReadOnlyList<UpgradeDefinition> UpgradePool => upgradePool;
        public PlayerBaseStats BaseStats => baseStats;
        public int OfferCount => offerCount;
        public GradeThresholds GradeThresholds => gradeThresholds;
        public bool UseFixedSeed => useFixedSeed;
        public int FixedSeed => fixedSeed;

        /// <summary>Preenche os campos em código (testes EditMode). Não usar em runtime.</summary>
        internal void Configure(
            IEnumerable<LevelDefinition> levels,
            RarityTable rarityTable,
            IEnumerable<UpgradeDefinition> upgradePool,
            PlayerBaseStats baseStats,
            int offerCount = 3)
        {
            this.levels = new List<LevelDefinition>(levels);
            this.rarityTable = rarityTable;
            this.upgradePool = new List<UpgradeDefinition>(upgradePool);
            this.baseStats = baseStats;
            this.offerCount = offerCount;
            gradeThresholds = GradeThresholds.Default;
        }
    }
}
