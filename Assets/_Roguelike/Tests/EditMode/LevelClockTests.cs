using System;
using NUnit.Framework;
using Roguelike.Run;

namespace Roguelike.Tests
{
    /// <summary>
    /// Testes do <see cref="LevelClock"/> (tarefa 3.1): quantização em décimos, quando o display muda, expiração
    /// única no limite, Stop, Tick parado, reinício e argumentos inválidos.
    /// </summary>
    public class LevelClockTests
    {
        private const float Tolerance = 1e-6f;

        private LevelClock clock;

        [SetUp]
        public void SetUp()
        {
            clock = new LevelClock();
        }

        // ───────────────────────────── Quantize ─────────────────────────────

        [TestCase(0f, 0f)]
        [TestCase(0.05f, 0f)]
        [TestCase(0.1f, 0.1f)]
        [TestCase(0.19f, 0.1f)]
        [TestCase(0.29f, 0.2f)]
        [TestCase(0.35f, 0.3f)]
        [TestCase(0.99f, 0.9f)]
        [TestCase(1f, 1f)]
        [TestCase(12.34f, 12.3f)]
        [TestCase(18.46f, 18.4f)]
        public void Quantize_FloorsToTenths(float seconds, float expected)
        {
            Assert.AreEqual(expected, LevelClock.Quantize(seconds), Tolerance);
        }

        [Test]
        public void Quantize_ExactTenth_ReturnsSameFloat()
        {
            Assert.AreEqual(0.3f, LevelClock.Quantize(0.3f));
            Assert.AreEqual(0.1f, LevelClock.Quantize(0.1f));
            Assert.AreEqual(0.2f, LevelClock.Quantize(0.2f));
        }

        [Test]
        public void Quantize_ValueOneUlpBelowTenth_RoundsUpToThatTenth()
        {
            Assert.AreEqual(0.3f, LevelClock.Quantize(0.29999998f));
        }

        [Test]
        public void Quantize_TenthsAccumulatedWithFloatSums_AreRecognized()
        {
            float sum = 0f;

            sum += 0.1f;
            Assert.AreEqual(0.1f, LevelClock.Quantize(sum));
            sum += 0.1f;
            Assert.AreEqual(0.2f, LevelClock.Quantize(sum));
            sum += 0.1f;
            Assert.AreEqual(0.3f, LevelClock.Quantize(sum));

            // Dez somas de 0.1f dão 1.0000001f em float, e o décimo continua sendo 1,0.
            float ten = 0f;
            for (int i = 0; i < 10; i++)
            {
                ten += 0.1f;
            }
            Assert.AreEqual(1f, LevelClock.Quantize(ten), Tolerance);
        }

        [TestCase(-0.01f)]
        [TestCase(-5f)]
        [TestCase(float.NegativeInfinity)]
        [TestCase(float.NaN)]
        public void Quantize_NegativeOrNaN_ReturnsZero(float seconds)
        {
            Assert.AreEqual(0f, LevelClock.Quantize(seconds));
        }

        // ───────────────────────────── Start ─────────────────────────────

        [Test]
        public void NewClock_IsStoppedAndZeroed()
        {
            Assert.IsFalse(clock.IsRunning);
            Assert.IsFalse(clock.HasExpired);
            Assert.AreEqual(0f, clock.TimeLimit);
            Assert.AreEqual(0f, clock.ElapsedSeconds);
            Assert.AreEqual(0f, clock.DisplayedSeconds);
        }

        [Test]
        public void Start_SetsLimitAndRunsFromZero()
        {
            clock.Start(20f);

            Assert.IsTrue(clock.IsRunning);
            Assert.IsFalse(clock.HasExpired);
            Assert.AreEqual(20f, clock.TimeLimit);
            Assert.AreEqual(0f, clock.ElapsedSeconds);
            Assert.AreEqual(0f, clock.DisplayedSeconds);
        }

        [TestCase(0f)]
        [TestCase(-1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(float.NegativeInfinity)]
        public void Start_InvalidLimit_ThrowsAndKeepsState(float limit)
        {
            clock.Start(20f);
            clock.Tick(1.25f);

            Assert.Throws<ArgumentOutOfRangeException>(() => clock.Start(limit));

            Assert.IsTrue(clock.IsRunning);
            Assert.AreEqual(20f, clock.TimeLimit);
            Assert.AreEqual(1.25f, clock.ElapsedSeconds, Tolerance);
            Assert.AreEqual(1.2f, clock.DisplayedSeconds, Tolerance);
        }

        // ───────────────────────────── Tick ─────────────────────────────

        [Test]
        public void Tick_WithinSameTenth_DoesNotChangeDisplay()
        {
            clock.Start(10f);

            LevelClockTick tick = clock.Tick(0.05f);

            Assert.IsFalse(tick.DisplayChanged);
            Assert.AreEqual(0f, tick.DisplayedSeconds);
            Assert.IsFalse(tick.Expired);
            Assert.AreEqual(0.05f, clock.ElapsedSeconds, Tolerance);
            Assert.AreEqual(0f, clock.DisplayedSeconds);
        }

        [Test]
        public void Tick_CrossingTenth_ChangesDisplayOnlyOnce()
        {
            clock.Start(10f);

            clock.Tick(0.05f);
            LevelClockTick crossing = clock.Tick(0.06f);
            LevelClockTick sameTenth = clock.Tick(0.02f);

            Assert.IsTrue(crossing.DisplayChanged);
            Assert.AreEqual(0.1f, crossing.DisplayedSeconds, Tolerance);
            Assert.IsFalse(sameTenth.DisplayChanged);
            Assert.AreEqual(0.1f, sameTenth.DisplayedSeconds, Tolerance);
            Assert.AreEqual(0.1f, clock.DisplayedSeconds, Tolerance);
        }

        [Test]
        public void Tick_ThreeTicksOfOneTenth_ChangeDisplayEveryTick()
        {
            clock.Start(10f);

            LevelClockTick first = clock.Tick(0.1f);
            LevelClockTick second = clock.Tick(0.1f);
            LevelClockTick third = clock.Tick(0.1f);

            Assert.IsTrue(first.DisplayChanged);
            Assert.AreEqual(0.1f, first.DisplayedSeconds, Tolerance);
            Assert.IsTrue(second.DisplayChanged);
            Assert.AreEqual(0.2f, second.DisplayedSeconds, Tolerance);
            Assert.IsTrue(third.DisplayChanged);
            Assert.AreEqual(0.3f, third.DisplayedSeconds, Tolerance);
        }

        [Test]
        public void Tick_ZeroDelta_ChangesNothing()
        {
            clock.Start(10f);
            clock.Tick(0.15f);

            LevelClockTick tick = clock.Tick(0f);

            Assert.IsFalse(tick.DisplayChanged);
            Assert.IsFalse(tick.Expired);
            Assert.AreEqual(0.1f, tick.DisplayedSeconds, Tolerance);
            Assert.IsTrue(clock.IsRunning);
        }

        [Test]
        public void Tick_LargeDelta_JumpsSeveralTenthsInOneChange()
        {
            clock.Start(10f);

            LevelClockTick tick = clock.Tick(0.55f);

            Assert.IsTrue(tick.DisplayChanged);
            Assert.AreEqual(0.5f, tick.DisplayedSeconds, Tolerance);
            Assert.IsFalse(tick.Expired);

            LevelClockTick next = clock.Tick(2.5f);

            Assert.IsTrue(next.DisplayChanged);
            Assert.AreEqual(3f, next.DisplayedSeconds, Tolerance);
            Assert.AreEqual(3.05f, clock.ElapsedSeconds, Tolerance);
        }

        [Test]
        public void Tick_JustBelowLimit_DoesNotExpire()
        {
            clock.Start(1f);

            LevelClockTick tick = clock.Tick(0.99f);

            Assert.IsFalse(tick.Expired);
            Assert.IsTrue(clock.IsRunning);
            Assert.IsFalse(clock.HasExpired);
            Assert.AreEqual(0.9f, tick.DisplayedSeconds, Tolerance);
        }

        // ───────────────────────────── Expiração ─────────────────────────────

        [Test]
        public void Tick_ReachingLimitExactly_Expires()
        {
            clock.Start(1f);
            clock.Tick(0.5f);

            LevelClockTick tick = clock.Tick(0.5f);

            Assert.IsTrue(tick.Expired);
            Assert.IsTrue(tick.DisplayChanged);
            Assert.AreEqual(1f, tick.DisplayedSeconds);
            Assert.AreEqual(1f, clock.ElapsedSeconds);
            Assert.AreEqual(1f, clock.DisplayedSeconds);
            Assert.IsFalse(clock.IsRunning);
            Assert.IsTrue(clock.HasExpired);
        }

        [Test]
        public void Tick_LimitReachedByFloatSumsOfTenths_Expires()
        {
            // 3 × 0.1f não dá exatamente 0.3f: a folga de expiração evita um frame "quase expirado".
            clock.Start(0.3f);

            Assert.IsFalse(clock.Tick(0.1f).Expired);
            Assert.IsFalse(clock.Tick(0.1f).Expired);
            LevelClockTick last = clock.Tick(0.1f);

            Assert.IsTrue(last.Expired);
            Assert.AreEqual(0.3f, last.DisplayedSeconds);
            Assert.AreEqual(0.3f, clock.ElapsedSeconds);
        }

        [Test]
        public void Tick_DeltaOvershootingLimit_ClampsToLimit()
        {
            clock.Start(2f);
            clock.Tick(1.25f);

            LevelClockTick tick = clock.Tick(5f);

            Assert.IsTrue(tick.Expired);
            Assert.IsTrue(tick.DisplayChanged);
            Assert.AreEqual(2f, tick.DisplayedSeconds);
            Assert.AreEqual(2f, clock.ElapsedSeconds);
            Assert.IsTrue(clock.HasExpired);
            Assert.IsFalse(clock.IsRunning);
        }

        [Test]
        public void Tick_LimitNotMultipleOfTenth_DisplaysQuantizedLimit()
        {
            clock.Start(1.25f);
            clock.Tick(1.2f);

            LevelClockTick tick = clock.Tick(1f);

            // Display já estava em 1,2 e Quantize(1,25) = 1,2: expira sem mudar o display.
            Assert.IsTrue(tick.Expired);
            Assert.IsFalse(tick.DisplayChanged);
            Assert.AreEqual(1.2f, tick.DisplayedSeconds, Tolerance);
            Assert.AreEqual(1.25f, clock.ElapsedSeconds);
        }

        [Test]
        public void Tick_AfterExpiring_NeverExpiresAgain()
        {
            clock.Start(1f);
            Assert.IsTrue(clock.Tick(3f).Expired);

            LevelClockTick after = clock.Tick(1f);
            LevelClockTick afterZero = clock.Tick(0f);

            Assert.IsFalse(after.Expired);
            Assert.IsFalse(after.DisplayChanged);
            Assert.AreEqual(1f, after.DisplayedSeconds);
            Assert.IsFalse(afterZero.Expired);
            Assert.AreEqual(1f, clock.ElapsedSeconds);
            Assert.IsTrue(clock.HasExpired);
        }

        [Test]
        public void ManyFrames_ExpireExactlyOnce()
        {
            clock.Start(1f);
            int expiredCount = 0;
            int changes = 0;

            for (int frame = 0; frame < 200; frame++)
            {
                LevelClockTick tick = clock.Tick(1f / 60f);
                if (tick.Expired) expiredCount++;
                if (tick.DisplayChanged) changes++;
            }

            Assert.AreEqual(1, expiredCount);
            // 0,1 … 1,0: dez mudanças de décimo, a última junto com a expiração.
            Assert.AreEqual(10, changes);
            Assert.AreEqual(1f, clock.DisplayedSeconds);
        }

        // ───────────────────────────── Stop ─────────────────────────────

        [Test]
        public void Stop_StopsWithoutExpiring()
        {
            clock.Start(10f);
            clock.Tick(0.45f);

            clock.Stop();

            Assert.IsFalse(clock.IsRunning);
            Assert.IsFalse(clock.HasExpired);
            Assert.AreEqual(0.45f, clock.ElapsedSeconds, Tolerance);
            Assert.AreEqual(0.4f, clock.DisplayedSeconds, Tolerance);
        }

        [Test]
        public void Stop_IsIdempotent()
        {
            clock.Start(10f);
            clock.Tick(0.45f);

            clock.Stop();
            clock.Stop();

            Assert.IsFalse(clock.IsRunning);
            Assert.IsFalse(clock.HasExpired);
            Assert.AreEqual(0.45f, clock.ElapsedSeconds, Tolerance);
        }

        [Test]
        public void Stop_BeforeStart_IsNoOp()
        {
            Assert.DoesNotThrow(() => clock.Stop());
            Assert.IsFalse(clock.IsRunning);
            Assert.IsFalse(clock.HasExpired);
        }

        [Test]
        public void Stop_AfterExpiring_KeepsExpired()
        {
            clock.Start(1f);
            clock.Tick(2f);

            clock.Stop();

            Assert.IsTrue(clock.HasExpired);
            Assert.IsFalse(clock.IsRunning);
        }

        [Test]
        public void Tick_WhenStopped_ReturnsEmptyTickAndKeepsTime()
        {
            clock.Start(1f);
            clock.Tick(0.35f);
            clock.Stop();

            LevelClockTick tick = clock.Tick(5f);

            Assert.IsFalse(tick.DisplayChanged);
            Assert.IsFalse(tick.Expired);
            Assert.AreEqual(0.3f, tick.DisplayedSeconds, Tolerance);
            Assert.AreEqual(0.35f, clock.ElapsedSeconds, Tolerance);
            Assert.IsFalse(clock.HasExpired);
        }

        [Test]
        public void Tick_BeforeStart_ReturnsEmptyTick()
        {
            LevelClockTick tick = clock.Tick(1f);

            Assert.IsFalse(tick.DisplayChanged);
            Assert.IsFalse(tick.Expired);
            Assert.AreEqual(0f, tick.DisplayedSeconds);
            Assert.AreEqual(0f, clock.ElapsedSeconds);
        }

        // ───────────────────────────── Reinício ─────────────────────────────

        [Test]
        public void Start_AfterExpiring_RestartsFromZero()
        {
            clock.Start(1f);
            clock.Tick(2f);

            clock.Start(5f);

            Assert.IsTrue(clock.IsRunning);
            Assert.IsFalse(clock.HasExpired);
            Assert.AreEqual(5f, clock.TimeLimit);
            Assert.AreEqual(0f, clock.ElapsedSeconds);
            Assert.AreEqual(0f, clock.DisplayedSeconds);

            LevelClockTick tick = clock.Tick(0.1f);
            Assert.IsTrue(tick.DisplayChanged);
            Assert.AreEqual(0.1f, tick.DisplayedSeconds, Tolerance);
            Assert.IsFalse(tick.Expired);

            Assert.IsTrue(clock.Tick(10f).Expired);
        }

        [Test]
        public void Start_AfterStop_RestartsFromZero()
        {
            clock.Start(10f);
            clock.Tick(3.3f);
            clock.Stop();

            clock.Start(10f);

            Assert.IsTrue(clock.IsRunning);
            Assert.AreEqual(0f, clock.ElapsedSeconds);
            Assert.AreEqual(0f, clock.DisplayedSeconds);
        }

        // ─────────────────────────── Argumentos inválidos ───────────────────────────

        [TestCase(-0.01f)]
        [TestCase(float.NaN)]
        [TestCase(float.NegativeInfinity)]
        public void Tick_InvalidDelta_ThrowsAndKeepsState(float deltaTime)
        {
            clock.Start(10f);
            clock.Tick(0.25f);

            Assert.Throws<ArgumentOutOfRangeException>(() => clock.Tick(deltaTime));

            Assert.IsTrue(clock.IsRunning);
            Assert.AreEqual(0.25f, clock.ElapsedSeconds, Tolerance);
            Assert.AreEqual(0.2f, clock.DisplayedSeconds, Tolerance);
        }

        [Test]
        public void Tick_InvalidDelta_ThrowsEvenWhenStopped()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => clock.Tick(-1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => clock.Tick(float.NaN));
        }
    }
}
