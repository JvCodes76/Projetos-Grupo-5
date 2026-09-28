using System;
using NUnit.Framework;
using Roguelike.Stats;

namespace Roguelike.Tests
{
    /// <summary>
    /// Testes de <see cref="JumpPhysics"/> (tarefa 2.1), incluindo o valor de referência com os
    /// parâmetros do Cyborg.prefab (jumpHeight 2,6 efetivo, timeToApex 0,37, gravidade do mundo 9,81, gravityScale 1).
    /// </summary>
    public class JumpPhysicsTests
    {
        private const float Tolerance = 1e-4f;

        [Test]
        public void JumpSpeed_MatchesLegacyFormula_JumpHeightOverTimeToApex()
        {
            const float jumpHeight = 2.6f;
            const float timeToApex = 0.37f;

            float expectedLegacy = jumpHeight / timeToApex;
            float actual = JumpPhysics.JumpSpeed(jumpHeight, timeToApex);

            Assert.AreEqual(expectedLegacy, actual, Tolerance);
            Assert.AreEqual(7.027027f, actual, Tolerance);
        }

        [Test]
        public void Gravity_MatchesLegacyFormula_TwoJumpHeightOverTimeToApexSquared()
        {
            const float jumpHeight = 2.6f;
            const float timeToApex = 0.37f;

            float expectedLegacy = (2f * jumpHeight) / (timeToApex * timeToApex);
            float actual = JumpPhysics.Gravity(jumpHeight, timeToApex);

            Assert.AreEqual(expectedLegacy, actual, Tolerance);
            Assert.AreEqual(37.98393f, actual, Tolerance);
        }

        [Test]
        public void GravityMultiplier_MatchesLegacyFormula_GravityOverWorldGravityOverGravityScale()
        {
            const float jumpHeight = 2.6f;
            const float timeToApex = 0.37f;
            const float worldGravityMagnitude = 9.81f;
            const float defaultGravityScale = 1f;

            float expectedLegacy = ((2f * jumpHeight) / (timeToApex * timeToApex)) / worldGravityMagnitude / defaultGravityScale;
            float actual = JumpPhysics.GravityMultiplier(jumpHeight, timeToApex, worldGravityMagnitude, defaultGravityScale);

            Assert.AreEqual(expectedLegacy, actual, Tolerance);
            Assert.AreEqual(3.871961f, actual, Tolerance);
        }

        [Test]
        public void JumpSpeed_TimeToApexZeroOrNegative_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => JumpPhysics.JumpSpeed(2.6f, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => JumpPhysics.JumpSpeed(2.6f, -1f));
        }

        [Test]
        public void Gravity_TimeToApexZeroOrNegative_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => JumpPhysics.Gravity(2.6f, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => JumpPhysics.Gravity(2.6f, -1f));
        }

        [Test]
        public void GravityMultiplier_TimeToApexZeroOrNegative_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => JumpPhysics.GravityMultiplier(2.6f, 0f, 9.81f, 1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => JumpPhysics.GravityMultiplier(2.6f, -1f, 9.81f, 1f));
        }

        [Test]
        public void GravityMultiplier_WorldGravityMagnitudeZeroOrNegative_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => JumpPhysics.GravityMultiplier(2.6f, 0.37f, 0f, 1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => JumpPhysics.GravityMultiplier(2.6f, 0.37f, -1f, 1f));
        }

        [Test]
        public void GravityMultiplier_DefaultGravityScaleZeroOrNegative_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => JumpPhysics.GravityMultiplier(2.6f, 0.37f, 9.81f, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => JumpPhysics.GravityMultiplier(2.6f, 0.37f, 9.81f, -1f));
        }
    }
}
