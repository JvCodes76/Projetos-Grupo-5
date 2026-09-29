using System.Collections.Generic;
using NUnit.Framework;
using Roguelike.Movement;
using UnityEngine;

namespace Roguelike.Tests.Movement
{
    /// <summary>Analisador de alcançabilidade (SPEC §14.2) em geometrias sintéticas: objetivo, fronteira e soft-locks.</summary>
    [Category("Movement")]
    public class ReachabilityModelTests
    {
        private MovementProfile profile;
        private AabbCollisionWorld world;
        private readonly List<Rect> goals = new List<Rect> { new Rect(15f, 0f, 1f, 2f) };

        [SetUp]
        public void SetUp()
        {
            profile = ScriptableObject.CreateInstance<MovementProfile>();
            world = new AabbCollisionWorld();
            // Chão em y = 0 de x −20 a 20 com um poço de 4 × 6 u (x 0..4, fundo em y = −6) e paredes nas pontas.
            world.AddBox(new Vector2(-20f, -7f), new Vector2(0f, 0f), AabbKind.Ground);
            world.AddBox(new Vector2(4f, -7f), new Vector2(20f, 0f), AabbKind.Ground);
            world.AddBox(new Vector2(0f, -7f), new Vector2(4f, -6f), AabbKind.Ground);
            world.AddBox(new Vector2(-22f, -7f), new Vector2(-20f, 10f), AabbKind.Boundary);
            world.AddBox(new Vector2(20f, -7f), new Vector2(22f, 10f), AabbKind.Boundary);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(profile);
        }

        private MovementStats BaseStats => MovementStatsResolver.Resolve(profile, new KitStatInput(profile));

        [Test]
        public void BaseKit_JumpsTheGapAndReachesGoal()
        {
            var model = new ReachabilityModel(profile, world, -20f);
            ReachabilityReport report = model.Run(BaseStats, new Vector2(-10f, 0.05f), goals, "base");
            Assert.IsTrue(report.GoalReached);
        }

        [Test]
        public void WallOfFiveUnits_BlocksBaseKit()
        {
            world.AddBox(new Vector2(8f, 0f), new Vector2(20f, 5f), AabbKind.Ground);
            var model = new ReachabilityModel(profile, world, -20f);
            var high = new List<Rect> { new Rect(15f, 5f, 1f, 2f) };
            ReachabilityReport report = model.Run(BaseStats, new Vector2(-10f, 0.05f), high, "base");
            Assert.IsFalse(report.GoalReached);
            Assert.Less(report.MaxFeetY, 5f);
        }

        [Test]
        public void PitWithoutSupport_IsSoftLock()
        {
            var model = new ReachabilityModel(profile, world, -20f);
            List<int> locked = model.FindSoftLocks(BaseStats, new Vector2(-10f, 0.05f), goals, out int reachable);
            Assert.AreEqual(3, reachable, "chão esquerdo, fundo do poço e chão direito");
            Assert.AreEqual(1, locked.Count, "só o fundo do poço");
            Assert.AreEqual(-6f, model.Surfaces[locked[0]].Y, 1e-4f);
        }

        [Test]
        public void PitWithStep_HasNoSoftLock()
        {
            // Apoio de 2 u encostado na parede esquerda do poço, topo em y = −3: sobe +3 e +3.
            world.AddBox(new Vector2(0f, -4f), new Vector2(2f, -3f), AabbKind.Ground);
            var model = new ReachabilityModel(profile, world, -20f);
            Assert.IsEmpty(model.FindSoftLocks(BaseStats, new Vector2(-10f, 0.05f), goals, out int reachable));
            Assert.AreEqual(4, reachable);
        }
    }
}
