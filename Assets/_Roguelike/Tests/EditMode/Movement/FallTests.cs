using System;
using NUnit.Framework;
using Roguelike.Movement;
using UnityEngine;

namespace Roguelike.Tests.Movement
{
    /// <summary>Queda e fast fall (SPEC §7.3, RF-13, RF-14, M16–M18).</summary>
    [Category("Movement")]
    public class FallTests
    {
        private const float Dt = 1f / 60f;

        [Test]
        public void M16_TerminalFallSpeed_Is17()
        {
            using var h = new MotorHarness();
            h.Spawn(new Vector2(0f, 500f));
            h.Run(90, In.None);
            Assert.AreEqual(-17f, h.Vel.y, 1e-4f);
        }

        [Test]
        public void M18_ZeroToMaxFall_TakesAbout0Point155s()
        {
            using var h = new MotorHarness();
            h.Spawn(new Vector2(0f, 500f));
            int ticks = h.RunUntil(() => h.Vel.y <= -17f + 1e-4f, In.None);
            Assert.AreEqual(0.155f, ticks * Dt, Dt + 1e-3f);
        }

        [Test]
        public void RF13_TerminalSpeed_SameAt50And60Hz()
        {
            float Terminal(int rate)
            {
                using var h = new MotorHarness(rate);
                h.Spawn(new Vector2(0f, 500f));
                h.Run(rate * 2, In.None);
                return h.Vel.y;
            }

            float t50 = Terminal(50);
            float t60 = Terminal(60);
            Assert.AreEqual(t60, t50, Math.Abs(t60) * 0.02f);
        }

        [Test]
        public void M17_FastFall_Reaches24InAbout0Point2s_AndReturns()
        {
            using var h = new MotorHarness();
            h.Spawn(new Vector2(0f, 1000f));
            h.Run(30, In.None);
            Assert.AreEqual(-17f, h.Vel.y, 1e-4f);

            int down = h.RunUntil(() => h.Vel.y <= -24f + 1e-4f, In.Move(0, -1));
            Assert.AreEqual(0.2f, down * Dt, 2f * Dt + 1e-3f);

            int back = h.RunUntil(() => h.Vel.y >= -17f - 1e-4f, In.None);
            Assert.AreEqual(0.2f, back * Dt, 2f * Dt + 1e-3f);
        }

        [Test]
        public void FastFall_OnlyRaisesCapWhenAlreadyAtMaxFall()
        {
            using var h = new MotorHarness();
            h.Spawn(new Vector2(0f, 500f));
            h.Run(3, In.Move(0, -1));
            Assert.AreEqual(17f, h.S.MaxFallCurrent, 1e-4f, "segurar para baixo antes da queda máxima não muda o teto");
        }

        [Test]
        public void RF15_Gravity_IsInvariantToJumpHeight()
        {
            using var h = new MotorHarness();
            h.Kit.Set(Roguelike.Upgrades.StatType.JumpHeight, 4.2f);
            h.Spawn(new Vector2(0f, 500f));
            h.Run(90, In.None);
            Assert.AreEqual(-17f, h.Vel.y, 1e-4f);
        }
    }
}
