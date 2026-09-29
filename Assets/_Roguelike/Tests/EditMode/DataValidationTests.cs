using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using Roguelike.Levels;
using Roguelike.Stats;
using Roguelike.Upgrades;

namespace Roguelike.Tests
{
    /// <summary>
    /// Valida os assets reais de Assets/_Roguelike/Data (tarefa 3.3; invariantes do ARQUITETURA.md §5).
    /// Os valores numéricos dos upgrades são de balanceamento (Etapa 4) e não são fixados aqui; a estrutura do design
    /// da §3.5 (raridade, stat, operação, stacks e flags de cada upgrade) é.
    /// </summary>
    [TestFixture]
    public class DataValidationTests
    {
        private const string DataFolder = "Assets/_Roguelike/Data";
        private const string RunConfigPath = DataFolder + "/RunConfig.asset";

        private static readonly float[] SampledPerformances = { 0f, 0.5f, 1f };

        private RunConfig runConfig;

        [SetUp]
        public void Setup()
        {
            runConfig = AssetDatabase.LoadAssetAtPath<RunConfig>(RunConfigPath);
        }

        [TearDown]
        public void TearDown()
        {
            TestFactory.DestroyAll();
        }

        [Test]
        public void TestRunConfigExists()
        {
            Assert.IsNotNull(runConfig, "RunConfig.asset deve existir em Assets/_Roguelike/Data/");

            var allConfigs = AssetDatabase.FindAssets("t:RunConfig", new[] { DataFolder });
            Assert.AreEqual(1, allConfigs.Length, "Deve haver exatamente um RunConfig em Assets/_Roguelike/Data");
        }

        [Test]
        public void TestLevelsValid()
        {
            Assert.IsNotNull(runConfig, "RunConfig não está carregado");
            Assert.IsNotEmpty(runConfig.Levels, "Levels não pode estar vazio");
            Assert.IsFalse(runConfig.Levels.Any(l => l == null), "Levels não pode conter nulos");

            var scenes = EditorBuildSettings.scenes;
            foreach (var level in runConfig.Levels)
            {
                var found = false;
                var enabled = false;
                foreach (var scene in scenes)
                {
                    var sceneName = System.IO.Path.GetFileNameWithoutExtension(scene.path);
                    if (sceneName == level.SceneName)
                    {
                        found = true;
                        enabled = scene.enabled;
                        break;
                    }
                }

                Assert.IsTrue(found, $"Cena '{level.SceneName}' não encontrada no Build Settings (level: {level.DisplayName})");
                Assert.IsTrue(enabled, $"Cena '{level.SceneName}' não está habilitada no Build Settings (level: {level.DisplayName})");

                Assert.Greater(level.TargetTime, 0f, $"TargetTime deve ser > 0 (level: {level.DisplayName})");
                Assert.Less(level.TargetTime, level.TimeLimit, $"TargetTime ({level.TargetTime}) deve ser < TimeLimit ({level.TimeLimit}) (level: {level.DisplayName})");
            }
        }

        [Test]
        public void TestRunConfigFieldsNotNull()
        {
            Assert.IsNotNull(runConfig, "RunConfig não está carregado");
            Assert.IsNotNull(runConfig.RarityTable, "RarityTable não pode ser nulo");
            Assert.IsNotNull(runConfig.BaseStats, "BaseStats não pode ser nulo");
            Assert.GreaterOrEqual(runConfig.OfferCount, 1, "OfferCount deve ser >= 1");
        }

        [Test]
        public void TestUpgradePoolValid()
        {
            Assert.IsNotNull(runConfig, "RunConfig não está carregado");
            Assert.IsFalse(runConfig.UpgradePool.Any(u => u == null), "UpgradePool não pode conter nulos");

            var poolSet = new HashSet<UpgradeDefinition>(runConfig.UpgradePool);
            Assert.AreEqual(runConfig.UpgradePool.Count, poolSet.Count, "UpgradePool não pode conter upgrades repetidos");

            foreach (var guid in AssetDatabase.FindAssets("t:UpgradeDefinition", new[] { DataFolder }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var upgrade = AssetDatabase.LoadAssetAtPath<UpgradeDefinition>(path);
                Assert.IsTrue(poolSet.Contains(upgrade), $"Upgrade órfão (fora do UpgradePool do RunConfig): {path}");
            }
        }

        [Test]
        public void TestUpgradeDefinitionsValid()
        {
            Assert.IsNotNull(runConfig, "RunConfig não está carregado");

            foreach (var upgrade in runConfig.UpgradePool)
            {
                Assert.IsNotEmpty(upgrade.Id, $"Upgrade {upgrade.name} tem Id vazio");
                Assert.IsNotNull(upgrade.Rarity, $"Upgrade {upgrade.Id} tem Rarity nula");
                Assert.GreaterOrEqual(upgrade.MaxStacks, 1, $"Upgrade {upgrade.Id} tem MaxStacks < 1");

                foreach (var modifier in upgrade.Modifiers)
                {
                    if (modifier.Operation == ModifierOperation.Multiply)
                    {
                        Assert.Greater(modifier.Value, 0f,
                            $"Upgrade {upgrade.Id}: Multiply deve ter valor > 0 (value: {modifier.Value})");
                    }

                    if (modifier.Stat == StatType.MaxAirJumps)
                    {
                        Assert.AreEqual(ModifierOperation.Add, modifier.Operation,
                            $"Upgrade {upgrade.Id}: MaxAirJumps deve usar Add, não Multiply");
                    }
                }

                foreach (var prereq in upgrade.Prerequisites)
                {
                    Assert.IsNotNull(prereq, $"Upgrade {upgrade.Id}: pré-requisito nulo");
                    Assert.Contains(prereq, runConfig.UpgradePool.ToList(),
                        $"Upgrade {upgrade.Id}: Pré-requisito {prereq.Id} não está no pool");
                }
            }

            var cycle = FindPrerequisiteCycle(runConfig.UpgradePool);
            Assert.IsNull(cycle, $"Ciclo de pré-requisitos passando pelo upgrade '{(cycle != null ? cycle.Id : string.Empty)}'");

            var ids = new HashSet<string>(runConfig.UpgradePool.Select(u => u.Id));
            Assert.AreEqual(ids.Count, runConfig.UpgradePool.Count, "Há ids duplicados de upgrades");
        }

        /// <summary>
        /// Estrutura do design da §3.5 (decisões do João de 28/09/2026 e D8): raridade, stat, operação e stacks de cada
        /// upgrade. Pega um asset com o stat ou a operação trocados. Os valores (8 %, 20 %) ficam livres para a 4.4,
        /// mas todo "aumento" precisa aumentar de fato.
        /// </summary>
        [TestCase("jump_small", "common", StatType.JumpHeight, ModifierOperation.Multiply, 3)]
        [TestCase("speed_small", "common", StatType.MaxSpeed, ModifierOperation.Multiply, 3)]
        [TestCase("jump_big", "rare", StatType.JumpHeight, ModifierOperation.Multiply, 2)]
        [TestCase("speed_big", "rare", StatType.MaxSpeed, ModifierOperation.Multiply, 2)]
        [TestCase("double_jump", "epic", StatType.MaxAirJumps, ModifierOperation.Add, 1)]
        public void TestStatUpgradeMatchesDesign(string id, string rarityId, StatType stat, ModifierOperation operation, int maxStacks)
        {
            var upgrade = FindInPool(id);

            Assert.AreEqual(rarityId, upgrade.Rarity.Id, $"Upgrade {id}: raridade");
            Assert.AreEqual(maxStacks, upgrade.MaxStacks, $"Upgrade {id}: MaxStacks");
            Assert.AreEqual(AbilityFlags.None, upgrade.Unlocks, $"Upgrade {id}: não deve liberar habilidade");
            Assert.AreEqual(1, upgrade.Modifiers.Count, $"Upgrade {id}: deve ter exatamente 1 modificador");

            var modifier = upgrade.Modifiers[0];
            Assert.AreEqual(stat, modifier.Stat, $"Upgrade {id}: stat do modificador");
            Assert.AreEqual(operation, modifier.Operation, $"Upgrade {id}: operação do modificador");

            if (operation == ModifierOperation.Multiply)
            {
                Assert.Greater(modifier.Value, 1f, $"Upgrade {id}: um aumento precisa ter fator > 1");
            }
            else
            {
                Assert.GreaterOrEqual(modifier.Value, 1f, $"Upgrade {id}: precisa somar ao menos 1");
            }
        }

        [TestCase("wall_jump", "epic", AbilityFlags.WallJump)]
        [TestCase("grappling_hook", "epic", AbilityFlags.GrapplingHook)]
        public void TestAbilityUpgradeMatchesDesign(string id, string rarityId, AbilityFlags unlocks)
        {
            var upgrade = FindInPool(id);

            Assert.AreEqual(rarityId, upgrade.Rarity.Id, $"Upgrade {id}: raridade");
            Assert.AreEqual(1, upgrade.MaxStacks, $"Upgrade {id}: MaxStacks");
            Assert.AreEqual(unlocks, upgrade.Unlocks, $"Upgrade {id}: habilidade liberada");
            Assert.AreEqual(0, upgrade.Modifiers.Count, $"Upgrade {id}: não deve ter modificadores");
        }

        [Test]
        public void TestBaseKitHasNoAbilities()
        {
            Assert.IsNotNull(runConfig, "RunConfig não está carregado");
            PlayerBaseStats baseStats = runConfig.BaseStats;

            // Design novo (§1): o kit inicial não tem pulo duplo, gancho nem wall grab.
            Assert.AreEqual(0f, baseStats.Get(StatType.MaxAirJumps), "Kit base: MaxAirJumps deve ser 0");
            Assert.AreEqual(AbilityFlags.None, baseStats.BaseAbilities, "Kit base: nenhuma habilidade liberada");
            Assert.AreEqual(0f, baseStats.Get(StatType.TimeLimitBonus), "Kit base: TimeLimitBonus deve ser 0");
        }

        [Test]
        public void TestRarityDefinitionsValid()
        {
            Assert.IsNotNull(runConfig, "RunConfig não está carregado");

            var rarityObjects = new List<RarityDefinition>();
            foreach (var guid in AssetDatabase.FindAssets("t:RarityDefinition", new[] { DataFolder }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                rarityObjects.Add(AssetDatabase.LoadAssetAtPath<RarityDefinition>(path));
            }

            var ids = new HashSet<string>(rarityObjects.Select(r => r.Id));
            Assert.AreEqual(ids.Count, rarityObjects.Count, "Há ids duplicados de raridades");

            var tiers = new HashSet<int>(rarityObjects.Select(r => r.Tier));
            Assert.AreEqual(tiers.Count, rarityObjects.Count, "Há tiers duplicados de raridades");

            Assert.IsNotNull(runConfig.RarityTable, "RarityTable é nula");
            var entries = runConfig.RarityTable.Entries;
            Assert.IsFalse(entries.Any(e => e.Rarity == null), "RarityTable tem entrada com raridade nula");

            var tableRarities = new HashSet<RarityDefinition>(entries.Select(e => e.Rarity));
            Assert.AreEqual(entries.Count, tableRarities.Count, "RarityTable tem raridades duplicadas");

            foreach (var poolRarity in runConfig.UpgradePool.Select(u => u.Rarity).Distinct())
            {
                Assert.IsTrue(tableRarities.Contains(poolRarity),
                    $"Raridade '{poolRarity.Id}' usada no pool mas não está na RarityTable");
            }

            foreach (var p in SampledPerformances)
            {
                var total = 0f;
                foreach (var entry in entries)
                {
                    Assert.IsNotNull(entry.WeightByPerformance, $"Raridade {entry.Rarity.Id}: curva de peso nula");

                    var weight = entry.WeightByPerformance.Evaluate(p);
                    Assert.GreaterOrEqual(weight, 0f, $"Raridade {entry.Rarity.Id}: peso negativo em p={p}");
                    total += weight;
                }

                Assert.Greater(total, 0f, $"Soma dos pesos em p={p} deve ser > 0");
            }
        }

        [Test]
        public void TestRaritiesHaveCandidates()
        {
            Assert.IsNotNull(runConfig, "RunConfig não está carregado");
            var maxTier = runConfig.RarityTable.Entries.Max(e => e.Rarity.Tier);

            foreach (var entry in runConfig.RarityTable.Entries)
            {
                var hasCandidates = runConfig.UpgradePool.Any(u => u.Rarity == entry.Rarity);

                if (entry.Rarity.Tier == maxTier)
                {
                    // Etapa 5 (tarefa 5.5): remover esta exceção. Até lá a Lendária fica sem candidatos (D9) e o sorteio
                    // cai para a raridade abaixo (ADR-07). A falha aqui avisa que a exceção ficou velha.
                    Assert.IsFalse(hasCandidates,
                        $"Raridade '{entry.Rarity.Id}': é a de maior tier e não deve ter candidatos no MVP");
                }
                else
                {
                    Assert.IsTrue(hasCandidates,
                        $"Raridade '{entry.Rarity.Id}' (tier {entry.Rarity.Tier}) não tem nenhum upgrade no pool");
                }
            }
        }

        [Test]
        public void TestBaseStatsValid()
        {
            Assert.IsNotNull(runConfig, "RunConfig não está carregado");
            Assert.IsNotNull(runConfig.BaseStats, "BaseStats é nulo");

            foreach (var statType in StatTypes.All)
            {
                var value = runConfig.BaseStats.Get(statType);
                Assert.GreaterOrEqual(value, 0f, $"BaseStats: {statType} deve ser >= 0 (value: {value})");
            }
        }

        // ───────────── Detector de ciclo (testado com dados sintéticos: o pool real não tem pré-requisitos) ─────────────

        [Test]
        public void FindPrerequisiteCycle_DetectsTwoNodeCycle()
        {
            var rarity = TestFactory.CreateRarity("common", 0);
            var a = TestFactory.CreateUpgrade("a", rarity);
            var b = TestFactory.CreateUpgrade("b", rarity, prerequisites: new[] { a });
            a.Configure("a", rarity, prerequisites: new[] { b });

            Assert.IsNotNull(FindPrerequisiteCycle(new[] { a, b }));
        }

        [Test]
        public void FindPrerequisiteCycle_DetectsSelfCycle()
        {
            var rarity = TestFactory.CreateRarity("common", 0);
            var a = TestFactory.CreateUpgrade("a", rarity);
            a.Configure("a", rarity, prerequisites: new[] { a });

            Assert.AreSame(a, FindPrerequisiteCycle(new[] { a }));
        }

        [Test]
        public void FindPrerequisiteCycle_AcceptsChainAndDiamond()
        {
            var rarity = TestFactory.CreateRarity("common", 0);
            var root = TestFactory.CreateUpgrade("root", rarity);
            var left = TestFactory.CreateUpgrade("left", rarity, prerequisites: new[] { root });
            var right = TestFactory.CreateUpgrade("right", rarity, prerequisites: new[] { root });
            var top = TestFactory.CreateUpgrade("top", rarity, prerequisites: new[] { left, right });

            Assert.IsNull(FindPrerequisiteCycle(new[] { top, left, right, root }));
        }

        /// <summary>
        /// DFS com três cores: devolve um upgrade que está num ciclo de pré-requisitos, ou null se não houver ciclo.
        /// Um nó ainda na pilha (cinza) alcançado de novo fecha um ciclo; um nó já concluído (preto) é seguro.
        /// </summary>
        private static UpgradeDefinition FindPrerequisiteCycle(IEnumerable<UpgradeDefinition> upgrades)
        {
            var inStack = new HashSet<UpgradeDefinition>();
            var done = new HashSet<UpgradeDefinition>();

            foreach (var upgrade in upgrades)
            {
                var cycle = Visit(upgrade, inStack, done);
                if (cycle != null) return cycle;
            }

            return null;
        }

        private static UpgradeDefinition Visit(
            UpgradeDefinition upgrade, HashSet<UpgradeDefinition> inStack, HashSet<UpgradeDefinition> done)
        {
            if (upgrade == null || done.Contains(upgrade)) return null;
            if (inStack.Contains(upgrade)) return upgrade;

            inStack.Add(upgrade);
            foreach (var prereq in upgrade.Prerequisites)
            {
                var cycle = Visit(prereq, inStack, done);
                if (cycle != null) return cycle;
            }
            inStack.Remove(upgrade);
            done.Add(upgrade);

            return null;
        }

        private UpgradeDefinition FindInPool(string id)
        {
            Assert.IsNotNull(runConfig, "RunConfig não está carregado");
            var upgrade = runConfig.UpgradePool.FirstOrDefault(u => u != null && u.Id == id);
            Assert.IsNotNull(upgrade, $"Upgrade '{id}' não está no UpgradePool");
            return upgrade;
        }
    }
}
