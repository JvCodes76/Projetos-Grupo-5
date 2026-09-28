using System.Collections.Generic;
using NUnit.Framework;
using Roguelike.Upgrades;
using UnityEngine;

namespace Roguelike.Tests
{
    /// <summary>
    /// Testes de <see cref="UpgradeOfferGenerator"/> (tarefa 2.2): determinismo, elegibilidade e fallback
    /// de raridade (ADR-06/ADR-07/ADR-08).
    /// </summary>
    public class UpgradeOfferGeneratorTests
    {
        private RarityDefinition common;
        private RarityDefinition rare;
        private RarityDefinition epic;
        private UpgradeOfferGenerator generator;
        private TestFactory.FakeUpgradeInventory inventory;

        [SetUp]
        public void SetUp()
        {
            common = TestFactory.CreateRarity("common", 0, "Comum");
            rare = TestFactory.CreateRarity("rare", 1, "Raro");
            epic = TestFactory.CreateRarity("epic", 2, "Épico");
            generator = new UpgradeOfferGenerator();
            inventory = TestFactory.CreateInventory();
        }

        [TearDown]
        public void TearDown()
        {
            TestFactory.DestroyAll();
        }

        // Tabela com peso constante só numa raridade: o RarityRoller sempre a sorteia, não importa o rng,
        // o que deixa os testes de elegibilidade e fallback livres do sorteio de raridade.
        private RarityTable CreateDominantTable(RarityDefinition dominant, params RarityDefinition[] others)
        {
            var entries = new List<(RarityDefinition, AnimationCurve)> { (dominant, AnimationCurve.Constant(0f, 1f, 1f)) };
            foreach (var other in others)
            {
                entries.Add((other, AnimationCurve.Constant(0f, 1f, 0f)));
            }

            return TestFactory.CreateRarityTable(entries.ToArray());
        }

        private UpgradeOfferRequest CreateRequest(
            RarityTable table,
            IReadOnlyList<UpgradeDefinition> pool,
            int offerCount = 1,
            int runSeed = 1,
            int levelIndex = 0,
            float performance = 0.5f)
        {
            return new UpgradeOfferRequest(runSeed, levelIndex, performance, offerCount, pool, table, inventory);
        }

        [Test]
        public void Generate_ReturnsExactlyOfferCountItems()
        {
            var table = CreateDominantTable(common, rare, epic);
            var pool = new[] { TestFactory.CreateUpgrade("a", common, maxStacks: 5) };
            var request = CreateRequest(table, pool, offerCount: 3);

            var offers = generator.Generate(request);

            Assert.AreEqual(3, offers.Count);
        }

        [Test]
        public void Generate_SameRequest_IsDeterministic()
        {
            var table = TestFactory.CreateDefaultRarityTable(out var c, out var r, out var e, out var l);
            var pool = new[]
            {
                TestFactory.CreateUpgrade("a", c, maxStacks: 3),
                TestFactory.CreateUpgrade("b", c, maxStacks: 3),
                TestFactory.CreateUpgrade("d", r, maxStacks: 2),
                TestFactory.CreateUpgrade("f", e, maxStacks: 1),
                TestFactory.CreateUpgrade("g", l, maxStacks: 1),
            };
            var request = CreateRequest(table, pool, offerCount: 3, runSeed: 777, levelIndex: 2, performance: 0.5f);

            var first = generator.Generate(request);
            var second = generator.Generate(request);

            Assert.AreEqual(first.Count, second.Count);
            for (int i = 0; i < first.Count; i++)
            {
                Assert.AreSame(first[i].Upgrade, second[i].Upgrade);
                Assert.AreSame(first[i].RolledRarity, second[i].RolledRarity);
            }
        }

        [Test]
        public void Generate_DifferentLevelIndex_ChangesTheSequence()
        {
            var table = TestFactory.CreateDefaultRarityTable(out var c, out var r, out var e, out var l);
            var pool = new[]
            {
                TestFactory.CreateUpgrade("a", c, maxStacks: 3),
                TestFactory.CreateUpgrade("b", c, maxStacks: 3),
                TestFactory.CreateUpgrade("d", r, maxStacks: 2),
                TestFactory.CreateUpgrade("f", e, maxStacks: 1),
                TestFactory.CreateUpgrade("g", l, maxStacks: 1),
            };

            var firstSlotUpgradeIds = new HashSet<string>();
            for (int levelIndex = 0; levelIndex < 10; levelIndex++)
            {
                var request = CreateRequest(table, pool, offerCount: 1, runSeed: 777, levelIndex: levelIndex, performance: 0.5f);
                var offers = generator.Generate(request);
                firstSlotUpgradeIds.Add(offers[0].IsEmpty ? "<empty>" : offers[0].Upgrade.Id);
            }

            Assert.Greater(firstSlotUpgradeIds.Count, 1, "Fases diferentes deveriam produzir ofertas diferentes em pelo menos um caso.");
        }

        [Test]
        public void Generate_NoDuplicateUpgradesInTheSameRound()
        {
            var table = CreateDominantTable(common, rare, epic);
            var pool = new[]
            {
                TestFactory.CreateUpgrade("a", common, maxStacks: 3),
                TestFactory.CreateUpgrade("b", common, maxStacks: 3),
                TestFactory.CreateUpgrade("c", common, maxStacks: 3),
            };
            var request = CreateRequest(table, pool, offerCount: 3);

            var offers = generator.Generate(request);

            var seen = new HashSet<UpgradeDefinition>();
            foreach (var offer in offers)
            {
                Assert.IsFalse(offer.IsEmpty);
                Assert.IsTrue(seen.Add(offer.Upgrade), "Upgrade repetido na mesma rodada de ofertas.");
            }
        }

        [Test]
        public void Generate_RespectsMaxStacks_ExcludesUpgradeAtCap()
        {
            var table = CreateDominantTable(common, rare, epic);
            var upgrade = TestFactory.CreateUpgrade("a", common, maxStacks: 1);
            inventory.SetStacks(upgrade, 1);
            var request = CreateRequest(table, new[] { upgrade }, offerCount: 1);

            var offers = generator.Generate(request);

            Assert.IsTrue(offers[0].IsEmpty);
            Assert.AreSame(common, offers[0].RolledRarity);
        }

        [Test]
        public void Generate_BelowMaxStacks_UpgradeIsEligible()
        {
            var table = CreateDominantTable(common, rare, epic);
            var upgrade = TestFactory.CreateUpgrade("a", common, maxStacks: 1);
            inventory.SetStacks(upgrade, 0);
            var request = CreateRequest(table, new[] { upgrade }, offerCount: 1);

            var offers = generator.Generate(request);

            Assert.IsFalse(offers[0].IsEmpty);
            Assert.AreSame(upgrade, offers[0].Upgrade);
        }

        [Test]
        public void Generate_PrerequisiteNotMet_ExcludesDependentUpgrade()
        {
            var table = CreateDominantTable(common, rare, epic);
            var prereq = TestFactory.CreateUpgrade("prereq", common, maxStacks: 3);
            var dependent = TestFactory.CreateUpgrade("dependent", common, maxStacks: 1, prerequisites: new[] { prereq });
            var request = CreateRequest(table, new[] { prereq, dependent }, offerCount: 1);

            var offers = generator.Generate(request);

            Assert.IsFalse(offers[0].IsEmpty);
            Assert.AreSame(prereq, offers[0].Upgrade);
        }

        [Test]
        public void Generate_PrerequisiteMet_DependentUpgradeBecomesEligible()
        {
            var table = CreateDominantTable(common, rare, epic);
            var prereq = TestFactory.CreateUpgrade("prereq", common, maxStacks: 3);
            var dependent = TestFactory.CreateUpgrade("dependent", common, maxStacks: 1, prerequisites: new[] { prereq });
            inventory.SetStacks(prereq, 1);
            var request = CreateRequest(table, new[] { dependent }, offerCount: 1);

            var offers = generator.Generate(request);

            Assert.IsFalse(offers[0].IsEmpty);
            Assert.AreSame(dependent, offers[0].Upgrade);
        }

        [Test]
        public void Generate_NoCandidateAtRolledRarity_FallsBackToClosestLowerTierFirst()
        {
            // Sorteia sempre epic; o pool só tem upgrades common (rare fica sem candidato e é pulado).
            var table = CreateDominantTable(epic, common, rare);
            var commonUpgrade = TestFactory.CreateUpgrade("a", common, maxStacks: 3);
            var request = CreateRequest(table, new[] { commonUpgrade }, offerCount: 1);

            var offers = generator.Generate(request);

            Assert.IsFalse(offers[0].IsEmpty);
            Assert.AreSame(commonUpgrade, offers[0].Upgrade);
            Assert.AreSame(epic, offers[0].RolledRarity);
            Assert.AreSame(common, offers[0].Rarity);
        }

        [Test]
        public void Generate_Fallback_PrefersClosestLowerTierOverLowestTier()
        {
            // Sorteia sempre epic, sem candidato epic; há common e rare no pool: o mais próximo abaixo (rare) vence.
            var table = CreateDominantTable(epic, common, rare);
            var commonUpgrade = TestFactory.CreateUpgrade("a", common, maxStacks: 3);
            var rareUpgrade = TestFactory.CreateUpgrade("b", rare, maxStacks: 3);
            var request = CreateRequest(table, new[] { commonUpgrade, rareUpgrade }, offerCount: 1);

            var offers = generator.Generate(request);

            Assert.AreSame(rareUpgrade, offers[0].Upgrade);
            Assert.AreSame(epic, offers[0].RolledRarity);
        }

        [Test]
        public void Generate_Fallback_TriesAllLowerTiersBeforeAnyUpperTier()
        {
            // Sorteia sempre rare, sem candidato rare; common (abaixo) e epic (acima) estão à mesma distância:
            // a ADR-07 manda esgotar os tiers de baixo antes de subir.
            var table = CreateDominantTable(rare, common, epic);
            var commonUpgrade = TestFactory.CreateUpgrade("a", common, maxStacks: 3);
            var epicUpgrade = TestFactory.CreateUpgrade("b", epic, maxStacks: 1);
            var request = CreateRequest(table, new[] { epicUpgrade, commonUpgrade }, offerCount: 1);

            var offers = generator.Generate(request);

            Assert.AreSame(commonUpgrade, offers[0].Upgrade);
        }

        [Test]
        public void Generate_Fallback_UpwardPrefersClosestUpperTier()
        {
            // Sorteia sempre common, nada abaixo; rare e epic no pool (epic primeiro na ordem do pool): sobe para rare.
            var table = CreateDominantTable(common, rare, epic);
            var epicUpgrade = TestFactory.CreateUpgrade("a", epic, maxStacks: 1);
            var rareUpgrade = TestFactory.CreateUpgrade("b", rare, maxStacks: 1);
            var request = CreateRequest(table, new[] { epicUpgrade, rareUpgrade }, offerCount: 1);

            var offers = generator.Generate(request);

            Assert.AreSame(rareUpgrade, offers[0].Upgrade);
            Assert.AreSame(common, offers[0].RolledRarity);
        }

        [Test]
        public void Generate_NothingBelow_FallsBackUpward()
        {
            // Sorteia sempre common (tier mais baixo); nada abaixo, então precisa subir até epic.
            var table = CreateDominantTable(common, rare, epic);
            var epicUpgrade = TestFactory.CreateUpgrade("a", epic, maxStacks: 1);
            var request = CreateRequest(table, new[] { epicUpgrade }, offerCount: 1);

            var offers = generator.Generate(request);

            Assert.IsFalse(offers[0].IsEmpty);
            Assert.AreSame(epicUpgrade, offers[0].Upgrade);
            Assert.AreSame(common, offers[0].RolledRarity);
            Assert.AreSame(epic, offers[0].Rarity);
        }

        [Test]
        public void Generate_NoCandidateAtAnyTier_ReturnsEmptyOfferWithRolledRarityPreserved()
        {
            var singleRarityTable = TestFactory.CreateRarityTable((common, AnimationCurve.Constant(0f, 1f, 1f)));
            var upgrade = TestFactory.CreateUpgrade("a", common, maxStacks: 1);
            inventory.SetStacks(upgrade, 1); // No cap: nenhum candidato elegível em nenhuma raridade.
            var request = CreateRequest(singleRarityTable, new[] { upgrade }, offerCount: 1);

            var offers = generator.Generate(request);

            Assert.IsTrue(offers[0].IsEmpty);
            Assert.AreSame(common, offers[0].RolledRarity);
            Assert.IsNull(offers[0].Rarity);
        }

        [Test]
        public void Generate_EmptyPool_ReturnsOnlyEmptyOffers()
        {
            var table = CreateDominantTable(common, rare, epic);
            var request = CreateRequest(table, System.Array.Empty<UpgradeDefinition>(), offerCount: 2);

            var offers = generator.Generate(request);

            Assert.AreEqual(2, offers.Count);
            Assert.IsTrue(offers[0].IsEmpty);
            Assert.IsTrue(offers[1].IsEmpty);
            Assert.AreSame(common, offers[0].RolledRarity);
            Assert.AreSame(common, offers[1].RolledRarity);
        }

        [Test]
        public void Generate_DoesNotMutatePoolOrInventory()
        {
            var table = CreateDominantTable(common, rare, epic);
            var upgradeA = TestFactory.CreateUpgrade("a", common, maxStacks: 3);
            var upgradeB = TestFactory.CreateUpgrade("b", common, maxStacks: 3);
            var pool = new[] { upgradeA, upgradeB };
            inventory.SetStacks(upgradeA, 1);
            var request = CreateRequest(table, pool, offerCount: 1);

            generator.Generate(request);

            Assert.AreEqual(2, pool.Length);
            Assert.AreSame(upgradeA, pool[0]);
            Assert.AreSame(upgradeB, pool[1]);
            Assert.AreEqual(1, inventory.GetStacks(upgradeA));
            Assert.AreEqual(0, inventory.GetStacks(upgradeB));
        }

        [Test]
        public void CombineSeed_SameInputs_ReturnsSameValue()
        {
            Assert.AreEqual(UpgradeOfferGenerator.CombineSeed(123, 4), UpgradeOfferGenerator.CombineSeed(123, 4));
        }

        [Test]
        public void CombineSeed_DifferentLevelIndex_ReturnsDifferentValue()
        {
            Assert.AreNotEqual(UpgradeOfferGenerator.CombineSeed(123, 0), UpgradeOfferGenerator.CombineSeed(123, 1));
        }

        [Test]
        public void CombineSeed_DifferentRunSeed_ReturnsDifferentValue()
        {
            Assert.AreNotEqual(UpgradeOfferGenerator.CombineSeed(1, 5), UpgradeOfferGenerator.CombineSeed(2, 5));
        }
    }
}
