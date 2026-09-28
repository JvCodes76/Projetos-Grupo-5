using System;
using System.Collections.Generic;
using NUnit.Framework;
using Roguelike.Upgrades;
using UnityEngine;

namespace Roguelike.Tests
{
    /// <summary>Testes de <see cref="RarityRoller"/> (tarefa 2.2): probabilidades e sorteio determinístico.</summary>
    public class RarityRollerTests
    {
        private RarityDefinition common;
        private RarityDefinition rare;
        private RarityDefinition epic;
        private RarityDefinition legendary;
        private RarityTable table;

        [SetUp]
        public void SetUp()
        {
            table = TestFactory.CreateDefaultRarityTable(out common, out rare, out epic, out legendary);
        }

        [TearDown]
        public void TearDown()
        {
            TestFactory.DestroyAll();
        }

        private static float[] ToArray(IReadOnlyList<float> values)
        {
            var array = new float[values.Count];
            for (int i = 0; i < values.Count; i++)
            {
                array[i] = values[i];
            }

            return array;
        }

        // Random que devolve sempre o mesmo NextDouble, para testar as bordas do sorteio.
        private sealed class FixedRandom : System.Random
        {
            private readonly double value;

            public FixedRandom(double value)
            {
                this.value = value;
            }

            public override double NextDouble() => value;

            protected override double Sample() => value;
        }

        [Test]
        public void Roll_RollJustBelowOne_NeverReturnsZeroWeightRarity()
        {
            // Com p = 0 o Lendário tem peso 0 e é a última entrada: o topo do intervalo tem que cair no Épico.
            var rarity = RarityRoller.Roll(table, 0f, new FixedRandom(0.9999999999d));

            Assert.AreSame(epic, rarity);
        }

        [Test]
        public void Roll_RollZero_ReturnsFirstRarityWithWeight()
        {
            var rarity = RarityRoller.Roll(table, 0f, new FixedRandom(0d));

            Assert.AreSame(common, rarity);
        }

        [Test]
        public void GetProbabilities_AtZeroPerformance_MatchesNormalizedWeights()
        {
            var probabilities = RarityRoller.GetProbabilities(table, 0f);

            Assert.AreEqual(0.70f, probabilities[0], 1e-4f);
            Assert.AreEqual(0.25f, probabilities[1], 1e-4f);
            Assert.AreEqual(0.05f, probabilities[2], 1e-4f);
            Assert.AreEqual(0.00f, probabilities[3], 1e-4f);
        }

        [Test]
        public void GetProbabilities_AtFullPerformance_MatchesNormalizedWeights()
        {
            var probabilities = RarityRoller.GetProbabilities(table, 1f);

            Assert.AreEqual(0.25f, probabilities[0], 1e-4f);
            Assert.AreEqual(0.40f, probabilities[1], 1e-4f);
            Assert.AreEqual(0.25f, probabilities[2], 1e-4f);
            Assert.AreEqual(0.10f, probabilities[3], 1e-4f);
        }

        [Test]
        public void GetProbabilities_PerformanceAboveOne_IsClampedToOne()
        {
            var atOne = ToArray(RarityRoller.GetProbabilities(table, 1f));
            var aboveOne = ToArray(RarityRoller.GetProbabilities(table, 5f));

            CollectionAssert.AreEqual(atOne, aboveOne);
        }

        [Test]
        public void GetProbabilities_PerformanceBelowZero_IsClampedToZero()
        {
            var atZero = ToArray(RarityRoller.GetProbabilities(table, 0f));
            var belowZero = ToArray(RarityRoller.GetProbabilities(table, -3f));

            CollectionAssert.AreEqual(atZero, belowZero);
        }

        [Test]
        public void GetProbabilities_NegativeWeight_IsTreatedAsZero()
        {
            var negative = TestFactory.CreateRarity("negative", 5, "Negativo");
            var customTable = TestFactory.CreateRarityTable(
                (common, AnimationCurve.Constant(0f, 1f, 10f)),
                (negative, AnimationCurve.Constant(0f, 1f, -5f)));

            var probabilities = RarityRoller.GetProbabilities(customTable, 0.5f);

            Assert.AreEqual(1f, probabilities[0], 1e-5f);
            Assert.AreEqual(0f, probabilities[1], 1e-5f);
        }

        [Test]
        public void GetProbabilities_ZeroWeightSum_ReturnsLowestTierWithProbabilityOne()
        {
            // "rare" (tier 1) vem primeiro na tabela, mas "common" (tier 0) é a raridade de menor tier.
            var customTable = TestFactory.CreateRarityTable(
                (rare, AnimationCurve.Constant(0f, 1f, 0f)),
                (common, AnimationCurve.Constant(0f, 1f, 0f)));

            var probabilities = RarityRoller.GetProbabilities(customTable, 0.5f);

            Assert.AreEqual(0f, probabilities[0], 1e-5f);
            Assert.AreEqual(1f, probabilities[1], 1e-5f);
        }

        [Test]
        public void Roll_NullTable_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => RarityRoller.Roll(null, 0.5f, new System.Random(1)));
        }

        [Test]
        public void Roll_EmptyTable_ThrowsInvalidOperationException()
        {
            var emptyTable = TestFactory.CreateRarityTable();

            Assert.Throws<InvalidOperationException>(() => RarityRoller.Roll(emptyTable, 0.5f, new System.Random(1)));
        }

        [Test]
        public void GetProbabilities_NullTable_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => RarityRoller.GetProbabilities(null, 0.5f));
        }

        [Test]
        public void GetProbabilities_EmptyTable_ThrowsInvalidOperationException()
        {
            var emptyTable = TestFactory.CreateRarityTable();

            Assert.Throws<InvalidOperationException>(() => RarityRoller.GetProbabilities(emptyTable, 0.5f));
        }

        [Test]
        public void Roll_SameSeed_ProducesSameSequence()
        {
            var rngA = new System.Random(12345);
            var rngB = new System.Random(12345);

            for (int i = 0; i < 50; i++)
            {
                var a = RarityRoller.Roll(table, 0.5f, rngA);
                var b = RarityRoller.Roll(table, 0.5f, rngB);
                Assert.AreEqual(a, b);
            }
        }

        [Test]
        public void Roll_ConsumesExactlyOneNextDouble_PerCall()
        {
            var rngUnderTest = new System.Random(999);
            var referenceRng = new System.Random(999);

            for (int i = 0; i < 20; i++)
            {
                RarityRoller.Roll(table, 0.5f, rngUnderTest);
                referenceRng.NextDouble();
            }

            // Se Roll consumisse mais ou menos que um NextDouble por chamada, os dois geradores
            // divergiriam e os próximos valores não bateriam mais.
            Assert.AreEqual(referenceRng.Next(), rngUnderTest.Next());
        }

        [Test]
        public void Roll_ZeroWeightSum_StillConsumesExactlyOneNextDouble()
        {
            var zeroSumTable = TestFactory.CreateRarityTable(
                (common, AnimationCurve.Constant(0f, 1f, 0f)),
                (rare, AnimationCurve.Constant(0f, 1f, 0f)));

            var rngUnderTest = new System.Random(7);
            var referenceRng = new System.Random(7);

            RarityRoller.Roll(zeroSumTable, 0.5f, rngUnderTest);
            referenceRng.NextDouble();

            Assert.AreEqual(referenceRng.Next(), rngUnderTest.Next());
        }

        [Test]
        public void Roll_Distribution_WithFixedSeed_MatchesProbabilitiesWithinTwoPercentagePoints()
        {
            const int rolls = 10000;
            var counts = new Dictionary<RarityDefinition, int>
            {
                { common, 0 },
                { rare, 0 },
                { epic, 0 },
                { legendary, 0 },
            };

            var rng = new System.Random(42);
            for (int i = 0; i < rolls; i++)
            {
                var result = RarityRoller.Roll(table, 0.5f, rng);
                counts[result]++;
            }

            var expectedProbabilities = RarityRoller.GetProbabilities(table, 0.5f);
            var entries = table.Entries;
            for (int i = 0; i < entries.Count; i++)
            {
                float actual = counts[entries[i].Rarity] / (float)rolls;
                Assert.AreEqual(expectedProbabilities[i], actual, 0.02f,
                    $"Raridade {entries[i].Rarity.Id} fora da margem de 2pp (esperado {expectedProbabilities[i]:P1}, obtido {actual:P1}).");
            }
        }
    }
}
