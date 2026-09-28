using System;
using NUnit.Framework;
using Roguelike.Run;

namespace Roguelike.Tests
{
    /// <summary>Testes de <see cref="PerformanceEvaluator"/> (tarefa 2.2): cálculo de p e da nota (§3.5).</summary>
    public class PerformanceEvaluatorTests
    {
        private const float Tolerance = 1e-5f;

        [Test]
        public void Evaluate_AtTarget_ReturnsOne()
        {
            Assert.AreEqual(1f, PerformanceEvaluator.Evaluate(30f, 60f, 30f), Tolerance);
        }

        [Test]
        public void Evaluate_BelowTarget_ReturnsOne()
        {
            Assert.AreEqual(1f, PerformanceEvaluator.Evaluate(10f, 60f, 30f), Tolerance);
        }

        [Test]
        public void Evaluate_AtLimit_ReturnsZero()
        {
            Assert.AreEqual(0f, PerformanceEvaluator.Evaluate(60f, 60f, 30f), Tolerance);
        }

        [Test]
        public void Evaluate_AboveLimit_ReturnsZero()
        {
            Assert.AreEqual(0f, PerformanceEvaluator.Evaluate(90f, 60f, 30f), Tolerance);
        }

        [Test]
        public void Evaluate_IntermediateTime_ReturnsExpectedValue()
        {
            // limite 60, alvo 30, tempo 45 -> (60-45)/(60-30) = 0.5
            Assert.AreEqual(0.5f, PerformanceEvaluator.Evaluate(45f, 60f, 30f), Tolerance);
        }

        [Test]
        public void Evaluate_LimitEqualsTarget_TimeAtOrBelowLimit_ReturnsOne()
        {
            Assert.AreEqual(1f, PerformanceEvaluator.Evaluate(30f, 30f, 30f), Tolerance);
        }

        [Test]
        public void Evaluate_LimitEqualsTarget_TimeAboveLimit_ReturnsZero()
        {
            Assert.AreEqual(0f, PerformanceEvaluator.Evaluate(31f, 30f, 30f), Tolerance);
        }

        [Test]
        public void Evaluate_LimitBelowTarget_TimeAtOrBelowLimit_ReturnsOne()
        {
            Assert.AreEqual(1f, PerformanceEvaluator.Evaluate(10f, 20f, 30f), Tolerance);
        }

        [Test]
        public void Evaluate_LimitBelowTarget_TimeAboveLimit_ReturnsZero()
        {
            Assert.AreEqual(0f, PerformanceEvaluator.Evaluate(25f, 20f, 30f), Tolerance);
        }

        [Test]
        public void Evaluate_NegativeElapsed_ThrowsArgumentOutOfRangeException()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => PerformanceEvaluator.Evaluate(-1f, 60f, 30f));
        }

        [TestCase(1f, PerformanceGrade.S)]
        [TestCase(0.9f, PerformanceGrade.S)]
        [TestCase(0.66f, PerformanceGrade.A)]
        [TestCase(0.33f, PerformanceGrade.B)]
        [TestCase(0f, PerformanceGrade.C)]
        public void GetGrade_DefaultThresholds_ExactBoundariesAreInclusive(float performance, PerformanceGrade expected)
        {
            Assert.AreEqual(expected, PerformanceEvaluator.GetGrade(performance));
        }

        [Test]
        public void GetGrade_JustBelowS_ReturnsA()
        {
            Assert.AreEqual(PerformanceGrade.A, PerformanceEvaluator.GetGrade(0.9f - 0.001f));
        }

        [Test]
        public void GetGrade_JustBelowA_ReturnsB()
        {
            Assert.AreEqual(PerformanceGrade.B, PerformanceEvaluator.GetGrade(0.66f - 0.001f));
        }

        [Test]
        public void GetGrade_JustBelowB_ReturnsC()
        {
            Assert.AreEqual(PerformanceGrade.C, PerformanceEvaluator.GetGrade(0.33f - 0.001f));
        }

        [Test]
        public void GetGrade_CustomThresholds_UsesGivenValuesInclusively()
        {
            var thresholds = new GradeThresholds(0.8f, 0.5f, 0.2f);

            Assert.AreEqual(PerformanceGrade.S, PerformanceEvaluator.GetGrade(0.8f, thresholds));
            Assert.AreEqual(PerformanceGrade.A, PerformanceEvaluator.GetGrade(0.5f, thresholds));
            Assert.AreEqual(PerformanceGrade.B, PerformanceEvaluator.GetGrade(0.2f, thresholds));
            Assert.AreEqual(PerformanceGrade.C, PerformanceEvaluator.GetGrade(0.19f, thresholds));
        }
    }
}
