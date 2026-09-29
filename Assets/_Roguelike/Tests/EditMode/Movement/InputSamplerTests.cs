using NUnit.Framework;
using Roguelike.Movement;
using Roguelike.Simulation;

namespace Roguelike.Tests.Movement
{
    /// <summary>Amostragem de input por frame → tick (SPEC §4.3, RF-62, RF-63).</summary>
    [Category("Movement")]
    public class InputSamplerTests
    {
        [Test]
        public void TenThousandPresses_RandomCadences_NoneLost()
        {
            var rng = new System.Random(1234); // sorteio só no teste
            var sampler = new InputSampler();
            var loop = new SimulationLoop();

            int pressesSent = 0;
            int pressesReceived = 0;
            bool physicallyHeld = false;
            double lastPressTime = -1;
            double time = 0;

            while (pressesSent < 10_000)
            {
                float frame = (float)(0.004 + rng.NextDouble() * 0.036); // frames de 4 a 40 ms
                time += frame;

                var sample = new FrameSample();
                // No máximo um aperto a cada 1/30 s (30 apertos/s já está muito além do humano).
                if (!physicallyHeld && time - lastPressTime >= 1.0 / 30.0 && rng.NextDouble() < 0.6)
                {
                    sample.PressedThisFrame = ButtonBits.Jump;
                    physicallyHeld = true;
                    pressesSent++;
                    lastPressTime = time;
                    if (rng.NextDouble() < 0.4)
                    {
                        sample.ReleasedThisFrame = ButtonBits.Jump; // apertou e soltou no mesmo frame
                        physicallyHeld = false;
                    }
                }
                else if (physicallyHeld && rng.NextDouble() < 0.5)
                {
                    sample.ReleasedThisFrame = ButtonBits.Jump;
                    physicallyHeld = false;
                }

                sample.HeldNow = physicallyHeld ? ButtonBits.Jump : ButtonBits.None;
                sampler.PushFrame(sample);

                loop.BeginFrame(frame);
                while (loop.NextStep() == StepKind.Tick)
                {
                    if (sampler.NextTick().IsPressed(ButtonBits.Jump)) pressesReceived++;
                }
            }

            // Drena o que ficou pendente.
            for (int i = 0; i < 4; i++)
            {
                if (sampler.NextTick().IsPressed(ButtonBits.Jump)) pressesReceived++;
            }

            Assert.AreEqual(0, sampler.DroppedPresses);
            Assert.AreEqual(pressesSent, pressesReceived);
        }

        [Test]
        public void PressAndReleaseInSameFrame_IsPressedNotHeld_MinimumJump()
        {
            var sampler = new InputSampler();
            sampler.PushFrame(new FrameSample { PressedThisFrame = ButtonBits.Jump, ReleasedThisFrame = ButtonBits.Jump });
            var t = sampler.NextTick();
            Assert.IsTrue(t.IsPressed(ButtonBits.Jump));
            Assert.IsFalse(t.IsHeld(ButtonBits.Jump));
        }

        [Test]
        public void TwoTicksInOneFrame_OnlyFirstGetsPressed_BothGetHeld()
        {
            var sampler = new InputSampler();
            sampler.PushFrame(new FrameSample { PressedThisFrame = ButtonBits.Jump, HeldNow = ButtonBits.Jump });
            var a = sampler.NextTick();
            var b = sampler.NextTick();
            Assert.IsTrue(a.IsPressed(ButtonBits.Jump) && a.IsHeld(ButtonBits.Jump));
            Assert.IsFalse(b.IsPressed(ButtonBits.Jump));
            Assert.IsTrue(b.IsHeld(ButtonBits.Jump));
        }

        [Test]
        public void TwoPressesBeforeOneTick_BothDelivered_FirstWithoutHeld()
        {
            var sampler = new InputSampler();
            sampler.PushFrame(new FrameSample { PressedThisFrame = ButtonBits.Jump, ReleasedThisFrame = ButtonBits.Jump });
            sampler.PushFrame(new FrameSample { PressedThisFrame = ButtonBits.Jump, HeldNow = ButtonBits.Jump });
            var a = sampler.NextTick();
            var b = sampler.NextTick();
            Assert.IsTrue(a.IsPressed(ButtonBits.Jump));
            Assert.IsFalse(a.IsHeld(ButtonBits.Jump));
            Assert.IsTrue(b.IsPressed(ButtonBits.Jump));
            Assert.IsTrue(b.IsHeld(ButtonBits.Jump));
        }

        [Test]
        public void Rearm_IgnoresButtonHeldAcrossSceneChange_UntilReleased()
        {
            var sampler = new InputSampler();
            sampler.PushFrame(new FrameSample { PressedThisFrame = ButtonBits.Jump, HeldNow = ButtonBits.Jump });
            sampler.Rearm(ButtonBits.Jump);

            sampler.PushFrame(new FrameSample { HeldNow = ButtonBits.Jump });
            var t = sampler.NextTick();
            Assert.IsFalse(t.IsPressed(ButtonBits.Jump), "o aperto de antes do Rearm foi descartado");
            Assert.IsFalse(t.IsHeld(ButtonBits.Jump), "segurado desde o Rearm não conta");

            sampler.PushFrame(new FrameSample { ReleasedThisFrame = ButtonBits.Jump });
            sampler.PushFrame(new FrameSample { PressedThisFrame = ButtonBits.Jump, HeldNow = ButtonBits.Jump });
            t = sampler.NextTick();
            Assert.IsTrue(t.IsPressed(ButtonBits.Jump) && t.IsHeld(ButtonBits.Jump), "depois de soltar, volta ao normal");
        }

        [Test]
        public void Deadzone_QuantizesBothAxes()
        {
            var sampler = new InputSampler(0.3f);
            sampler.PushFrame(new FrameSample { RawX = 0.29f, RawY = -0.31f });
            var t = sampler.NextTick();
            Assert.AreEqual(0, t.MoveX);
            Assert.AreEqual(-1, t.MoveY);
        }
    }
}
