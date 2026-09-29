using NUnit.Framework;
using Roguelike.Movement;
using Roguelike.Upgrades;
using UnityEngine;

namespace Roguelike.Tests.Movement
{
    /// <summary>Determinismo e replay (RNF-02): mesma entrada por tick ⇒ mesmo hash.</summary>
    [Category("Movement")]
    public class DeterminismTests
    {
        private static void BuildWorld(MotorHarness h)
        {
            h.Floor(0f, -60f, 60f);
            h.Box(-60f, 0f, -58f, 40f);
            h.Box(58f, 0f, 60f, 40f);
            h.Box(5f, 3f, 12f, 3.5f);
            h.Box(15f, 0f, 18f, 6f);
            h.Box(25f, 2f, 27f, 12f);
            h.Box(-20f, 8f, -10f, 9f);
            h.World.AddGrapplePoint(new Vector2(0f, 12f));
            h.World.AddGrapplePoint(new Vector2(30f, 15f));
        }

        private static InputRecording RandomRecording(int ticks, int seed)
        {
            var rng = new System.Random(seed); // sorteio só no teste
            var rec = new InputRecording();
            var held = ButtonBits.None;
            sbyte x = 0, y = 0;
            for (int i = 0; i < ticks; i++)
            {
                if (rng.NextDouble() < 0.1) x = (sbyte)(rng.Next(3) - 1);
                if (rng.NextDouble() < 0.05) y = (sbyte)(rng.Next(3) - 1);
                var pressed = ButtonBits.None;
                foreach (var bit in new[] { ButtonBits.Jump, ButtonBits.Dash, ButtonBits.Grapple })
                {
                    if ((held & bit) == 0 && rng.NextDouble() < 0.06)
                    {
                        pressed |= bit;
                        held |= bit;
                    }
                    else if ((held & bit) != 0 && rng.NextDouble() < 0.15)
                    {
                        held &= ~bit;
                    }
                }

                rec.Append(new TickInput(x, y, pressed, held));
            }

            return rec;
        }

        private static ulong Play(InputRecording rec)
        {
            using var h = new MotorHarness();
            BuildWorld(h);
            h.Abilities(AbilityFlags.WallJump | AbilityFlags.GrapplingHook, airJumps: 1, dashes: 1);
            h.Spawn(rec.StartFeet);
            rec.Rewind();
            for (int i = 0; i < rec.Count; i++) h.Step(rec.NextTick());
            return h.Motor.ComputeHash();
        }

        [Test]
        public void TenThousandTicks_PlayedTwice_SameHash()
        {
            var rec = RandomRecording(10_000, 99);
            rec.StartFeet = new Vector2(0f, 1f);
            Assert.AreEqual(Play(rec), Play(rec));
        }

        [Test]
        public void RecordSerializeReplay_SameHash()
        {
            var rec = RandomRecording(3_000, 7);
            rec.StartFeet = new Vector2(2f, 1f);
            ulong original = Play(rec);
            rec.FinalHash = original;

            var copy = InputRecording.Deserialize(rec.Serialize());
            Assert.AreEqual(rec.Count, copy.Count);
            Assert.AreEqual(original, copy.FinalHash);
            Assert.AreEqual(original, Play(copy));
        }

        [Test]
        public void DifferentInput_DifferentHash()
        {
            var a = RandomRecording(600, 1);
            var b = RandomRecording(600, 2);
            a.StartFeet = b.StartFeet = new Vector2(0f, 1f);
            Assert.AreNotEqual(Play(a), Play(b));
        }
    }
}
