using System.Collections.Generic;
using Roguelike.Levels;
using Roguelike.Stats;
using Roguelike.Upgrades;
using UnityEngine;

namespace Roguelike.Tests
{
    /// <summary>
    /// Fábrica de ScriptableObjects e dublês para os testes EditMode das tarefas 2.2 e 2.3 (upgrades e raridade).
    /// Todo SO criado por este helper é registrado e destruído em <see cref="DestroyAll"/>, que cada teste deve
    /// chamar no próprio [TearDown] para não vazar assets entre testes (a Unity não coleta ScriptableObjects
    /// criados via CreateInstance sozinha).
    /// </summary>
    internal static class TestFactory
    {
        private static readonly List<Object> createdObjects = new List<Object>();

        /// <summary>Cria uma RarityDefinition avulsa via Configure (ADR-17).</summary>
        internal static RarityDefinition CreateRarity(string id, int tier, string displayName = null)
        {
            var rarity = ScriptableObject.CreateInstance<RarityDefinition>();
            rarity.Configure(id, displayName ?? id, tier, Color.white);
            createdObjects.Add(rarity);
            return rarity;
        }

        /// <summary>
        /// Monta a RarityTable com as curvas placeholder da §3.5 (Comum 70→25, Raro 25→40, Épico 5→25,
        /// Lendário 0→10, lineares em p) e devolve as quatro raridades criadas.
        /// </summary>
        internal static RarityTable CreateDefaultRarityTable(
            out RarityDefinition common,
            out RarityDefinition rare,
            out RarityDefinition epic,
            out RarityDefinition legendary)
        {
            common = CreateRarity("common", 0, "Comum");
            rare = CreateRarity("rare", 1, "Raro");
            epic = CreateRarity("epic", 2, "Épico");
            legendary = CreateRarity("legendary", 3, "Lendário");

            return CreateRarityTable(
                (common, AnimationCurve.Linear(0f, 70f, 1f, 25f)),
                (rare, AnimationCurve.Linear(0f, 25f, 1f, 40f)),
                (epic, AnimationCurve.Linear(0f, 5f, 1f, 25f)),
                (legendary, AnimationCurve.Linear(0f, 0f, 1f, 10f)));
        }

        /// <summary>Monta uma RarityTable com as entradas (raridade, curva de peso) dadas, na ordem recebida.</summary>
        internal static RarityTable CreateRarityTable(params (RarityDefinition rarity, AnimationCurve curve)[] entries)
        {
            var table = ScriptableObject.CreateInstance<RarityTable>();
            var list = new List<RarityWeight>(entries.Length);
            foreach (var entry in entries)
            {
                list.Add(new RarityWeight(entry.rarity, entry.curve));
            }

            table.Configure(list);
            createdObjects.Add(table);
            return table;
        }

        /// <summary>Cria um UpgradeDefinition avulso via Configure (ADR-17).</summary>
        internal static UpgradeDefinition CreateUpgrade(
            string id,
            RarityDefinition rarity,
            int maxStacks = 1,
            IEnumerable<UpgradeDefinition> prerequisites = null)
        {
            var upgrade = ScriptableObject.CreateInstance<UpgradeDefinition>();
            upgrade.Configure(id, rarity, maxStacks, prerequisites: prerequisites);
            createdObjects.Add(upgrade);
            return upgrade;
        }

        /// <summary>Inventário falso em memória (dicionário) para testes do gerador de ofertas.</summary>
        internal static FakeUpgradeInventory CreateInventory()
        {
            return new FakeUpgradeInventory();
        }

        /// <summary>
        /// Cria um UpgradeDefinition com modificadores, flags, pré-requisitos e efeitos (tarefa 2.3).
        /// Nome diferente de <see cref="CreateUpgrade"/> para não gerar ambiguidade de sobrecarga.
        /// </summary>
        internal static UpgradeDefinition CreateConfiguredUpgrade(
            string id,
            RarityDefinition rarity,
            int maxStacks = 1,
            IEnumerable<StatModifier> modifiers = null,
            AbilityFlags unlocks = AbilityFlags.None,
            IEnumerable<UpgradeDefinition> prerequisites = null,
            IEnumerable<UpgradeEffect> effects = null)
        {
            var upgrade = ScriptableObject.CreateInstance<UpgradeDefinition>();
            upgrade.Configure(id, rarity, maxStacks, modifiers, unlocks, prerequisites, effects: effects);
            createdObjects.Add(upgrade);
            return upgrade;
        }

        /// <summary>Cria uma LevelDefinition via Configure (ADR-17).</summary>
        internal static LevelDefinition CreateLevel(string sceneName, float timeLimit, float targetTime)
        {
            var level = ScriptableObject.CreateInstance<LevelDefinition>();
            level.Configure(sceneName, timeLimit, targetTime);
            createdObjects.Add(level);
            return level;
        }

        /// <summary>Cria um PlayerBaseStats com os valores padrão do SO (kit base do Cyborg, ADR-18).</summary>
        internal static PlayerBaseStats CreateBaseStats()
        {
            var baseStats = ScriptableObject.CreateInstance<PlayerBaseStats>();
            createdObjects.Add(baseStats);
            return baseStats;
        }

        /// <summary>Cria um RunConfig via Configure (GradeThresholds ficam no padrão 0,9/0,66/0,33).</summary>
        internal static RunConfig CreateRunConfig(
            IEnumerable<LevelDefinition> levels,
            RarityTable rarityTable,
            IEnumerable<UpgradeDefinition> upgradePool,
            PlayerBaseStats baseStats,
            int offerCount = 3)
        {
            var config = ScriptableObject.CreateInstance<RunConfig>();
            config.Configure(levels, rarityTable, upgradePool, baseStats, offerCount);
            createdObjects.Add(config);
            return config;
        }

        /// <summary>Registra um SO criado fora do factory (ex.: UpgradeEffect de teste) para o DestroyAll.</summary>
        internal static T Track<T>(T obj) where T : Object
        {
            createdObjects.Add(obj);
            return obj;
        }

        /// <summary>Destrói todos os ScriptableObjects criados por este factory desde o último DestroyAll.</summary>
        internal static void DestroyAll()
        {
            foreach (var obj in createdObjects)
            {
                if (obj != null)
                {
                    Object.DestroyImmediate(obj);
                }
            }

            createdObjects.Clear();
        }

        /// <summary>Dublê de <see cref="IUpgradeInventory"/> baseado em dicionário; não altera nada sozinho.</summary>
        internal sealed class FakeUpgradeInventory : IUpgradeInventory
        {
            private readonly Dictionary<UpgradeDefinition, int> stacks = new Dictionary<UpgradeDefinition, int>();

            public int GetStacks(UpgradeDefinition upgrade)
            {
                return stacks.TryGetValue(upgrade, out var value) ? value : 0;
            }

            public void SetStacks(UpgradeDefinition upgrade, int value)
            {
                stacks[upgrade] = value;
            }
        }
    }
}
