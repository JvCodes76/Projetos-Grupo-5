using NUnit.Framework;
using Roguelike.Movement;
using UnityEngine;

namespace Roguelike.Tests.Movement
{
    /// <summary>Lógica pura da apresentação (SPEC §10, RF-51, RF-54, RF-55, RF-57, RF-59).</summary>
    [Category("Movement")]
    public class FeedbackLogicTests
    {
        private FeedbackTuning tuning;

        [SetUp]
        public void SetUp() => tuning = ScriptableObject.CreateInstance<FeedbackTuning>();

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(tuning);

        // --- Squash (RF-55) ---

        [Test]
        public void Squash_Jump_Is0Point6By1Point4()
        {
            var squash = SquashModel.Identity;
            squash.OnJump(tuning);
            Assert.AreEqual(new Vector2(0.6f, 1.4f), squash.Scale);
        }

        [TestCase(24f, 1.6f, 0.4f)]
        [TestCase(12f, 1.3f, 0.7f)]
        [TestCase(48f, 1.6f, 0.4f)]
        [TestCase(0f, 1f, 1f)]
        public void Squash_Landing_ProportionalToImpact(float impact, float x, float y)
        {
            var squash = SquashModel.Identity;
            squash.OnLanded(impact, tuning);
            Assert.AreEqual(x, squash.Scale.x, 1e-4f);
            Assert.AreEqual(y, squash.Scale.y, 1e-4f);
        }

        [Test]
        public void Squash_Respawn_Is1Point5By0Point5()
        {
            var squash = SquashModel.Identity;
            squash.OnRespawn(tuning);
            Assert.AreEqual(new Vector2(1.5f, 0.5f), squash.Scale);
        }

        [Test]
        public void Squash_ReturnsLinearlyAt1Point75PerSecond()
        {
            var squash = SquashModel.Identity;
            squash.OnJump(tuning);
            squash.Update(0.1f, tuning);
            Assert.AreEqual(0.775f, squash.Scale.x, 1e-4f);
            Assert.AreEqual(1.225f, squash.Scale.y, 1e-4f);
            squash.Update(1f, tuning);
            Assert.AreEqual(Vector2.one, squash.Scale);
        }

        // --- Animação (RF-54, DS-15) ---

        private static PlayerSnapshot Snap(MotorStateId state, bool grounded, Vector2 v, sbyte wallSide = 0)
        {
            var s = new MotorState { State = state, Grounded = grounded, Velocity = v, WallSlideSide = wallSide, Facing = 1 };
            var st = new MovementStats { MaxSpeed = 10f };
            return new PlayerSnapshot(in s, in st, new Vector2(0.51f, 1.26f), 17f, 0);
        }

        [Test]
        public void Anim_LandingWhileRunning_IsRunSameFrame()
        {
            Assert.AreEqual(AnimState.Run, AnimStateSelector.Select(Snap(MotorStateId.Normal, true, new Vector2(10f, 0f)), 0, 99));
        }

        [Test]
        public void Anim_LandingStopped_IsLandFor6Ticks_ThenIdle()
        {
            var still = Snap(MotorStateId.Normal, true, Vector2.zero);
            Assert.AreEqual(AnimState.Land, AnimStateSelector.Select(still, 0, 99));
            Assert.AreEqual(AnimState.Land, AnimStateSelector.Select(still, 5, 99));
            Assert.AreEqual(AnimState.Idle, AnimStateSelector.Select(still, 6, 99));
        }

        [TestCase(8f, AnimState.Rise)]
        [TestCase(2f, AnimState.Apex)]
        [TestCase(-2f, AnimState.Apex)]
        [TestCase(-8f, AnimState.Fall)]
        public void Anim_AirStatesByVerticalSpeed(float vy, AnimState expected)
        {
            Assert.AreEqual(expected, AnimStateSelector.Select(Snap(MotorStateId.Normal, false, new Vector2(0f, vy)), 99, 99));
        }

        [Test]
        public void Anim_StatePriorities()
        {
            Assert.AreEqual(AnimState.WallSlide, AnimStateSelector.Select(Snap(MotorStateId.Normal, false, new Vector2(0f, -2.5f), 1), 99, 99));
            Assert.AreEqual(AnimState.Dash, AnimStateSelector.Select(Snap(MotorStateId.Dash, false, new Vector2(27f, 0f)), 99, 99));
            Assert.AreEqual(AnimState.GrapplePull, AnimStateSelector.Select(Snap(MotorStateId.Grapple, false, new Vector2(0f, 20f)), 99, 99));
            Assert.AreEqual(AnimState.Dead, AnimStateSelector.Select(Snap(MotorStateId.Dead, true, Vector2.zero), 99, 99));
            Assert.AreEqual(AnimState.AirJump, AnimStateSelector.Select(Snap(MotorStateId.Normal, false, new Vector2(0f, 10f)), 99, 3));
        }

        [Test]
        public void Anim_RunSpeedMultiplier_Clamped()
        {
            Assert.AreEqual(0.5f, AnimStateSelector.RunSpeedMultiplier(1f, 10f), 1e-5f);
            Assert.AreEqual(1f, AnimStateSelector.RunSpeedMultiplier(10f, 10f), 1e-5f);
            Assert.AreEqual(1.5f, AnimStateSelector.RunSpeedMultiplier(40f, 10f), 1e-5f);
        }

        // --- Áudio (RF-57) ---

        [Test]
        public void EveryMovementEvent_HasAnAudioCue()
        {
            foreach (var type in FeedbackCueMap.EventsWithCue)
            {
                Assert.AreNotEqual(FeedbackCue.None, FeedbackCueMap.CueFor(type), type.Name);
            }
        }

        [Test]
        public void Denied_OnlyNoChargeAndCooldownSound()
        {
            Assert.AreEqual(FeedbackCue.Denied, FeedbackCueMap.ForDenied(DenyReason.NoCharge));
            Assert.AreEqual(FeedbackCue.Denied, FeedbackCueMap.ForDenied(DenyReason.Cooldown));
            Assert.AreEqual(FeedbackCue.None, FeedbackCueMap.ForDenied(DenyReason.Locked));
            Assert.AreEqual(FeedbackCue.None, FeedbackCueMap.ForDenied(DenyReason.NoTarget));
        }

        [Test]
        public void HardLanding_From50PercentOfMaxFall()
        {
            Assert.AreEqual(FeedbackCue.LandHard, FeedbackCueMap.ForLanding(8.5f, tuning.HardLandingSpeed));
            Assert.AreEqual(FeedbackCue.Land, FeedbackCueMap.ForLanding(8f, tuning.HardLandingSpeed));
        }

        // --- Shake (RF-51) ---

        [TestCase(0f, 0f)]
        [TestCase(0.5f, 1f / 32f)]
        [TestCase(1f, 2f / 32f)]
        public void Shake_RespectsScale(float scale, float expectedAmplitude)
        {
            var shake = new ShakeModel();
            shake.Start(Vector2.right, 0.2f, 2, scale, 30f);
            Vector2 first = shake.Evaluate(0.001f);
            Assert.AreEqual(expectedAmplitude, Mathf.Abs(first.x), 1e-5f);
            Assert.AreEqual(0f, first.y, 1e-6f);
        }

        [Test]
        public void Shake_IsPixelSnapped_AndEnds()
        {
            var shake = new ShakeModel();
            shake.Start(new Vector2(1f, 1f), 0.3f, 2, 1f, 30f);
            for (float t = 0f; t < 0.3f; t += 0.01f)
            {
                Vector2 o = shake.Evaluate(t);
                Assert.AreEqual(Mathf.Round(o.x * 32f), o.x * 32f, 1e-4f);
            }

            Assert.AreEqual(Vector2.zero, shake.Evaluate(0.31f));
        }

        // --- Rumble (RF-59) ---

        [Test]
        public void Rumble_StopsInAllFiveConditions()
        {
            var policy = new RumblePolicy();
            System.Action[] stops = { policy.OnPaused, policy.OnPlayerDied, policy.OnSceneChanged, policy.OnFocusLost, policy.OnDisabled };
            foreach (var stop in stops)
            {
                policy.Play(tuning.Strong, 1f);
                Assert.IsTrue(policy.IsActive);
                stop();
                Assert.IsFalse(policy.IsActive);
                Assert.AreEqual(0f, policy.LowFrequency);
            }
        }

        [Test]
        public void Rumble_EndsAfterDuration_AndScaleZeroDisables()
        {
            var policy = new RumblePolicy();
            policy.Play(tuning.Light, 1f);
            Assert.IsTrue(policy.Tick(0.05f));
            Assert.IsFalse(policy.Tick(0.05f));
            policy.Play(tuning.Strong, 0f);
            Assert.IsFalse(policy.IsActive);
        }

        [Test]
        public void Rumble_JumpDoesNotVibrate_DashIsStrong()
        {
            Assert.AreEqual(RumbleLevel.None, RumblePolicy.LevelFor(FeedbackCue.Jump));
            Assert.AreEqual(RumbleLevel.Strong, RumblePolicy.LevelFor(FeedbackCue.Dash));
            Assert.AreEqual(RumbleLevel.Medium, RumblePolicy.LevelFor(FeedbackCue.GrappleAttach));
        }
    }
}
