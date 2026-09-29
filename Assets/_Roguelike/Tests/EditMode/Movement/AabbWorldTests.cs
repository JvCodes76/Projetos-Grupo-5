using NUnit.Framework;
using Roguelike.Movement;
using UnityEngine;

namespace Roguelike.Tests.Movement
{
    /// <summary>Mundo AABB dos testes (SPEC §2.4): semântica das consultas.</summary>
    [Category("Movement")]
    public class AabbWorldTests
    {
        private AabbCollisionWorld world;

        [SetUp]
        public void SetUp()
        {
            world = new AabbCollisionWorld();
            world.AddBox(new Vector2(0f, 0f), new Vector2(10f, 1f), AabbKind.Ground);
            world.AddBox(new Vector2(20f, 0f), new Vector2(21f, 10f), AabbKind.Boundary);
            world.AddBox(new Vector2(30f, 0f), new Vector2(31f, 2f), AabbKind.Enemy);
            world.AddBox(new Vector2(40f, 0f), new Vector2(42f, 2f), AabbKind.KillZone);
        }

        [Test]
        public void CastBox_DownOntoGround_ReturnsGap()
        {
            Assert.IsTrue(world.CastBox(new Vector2(5f, 2f), new Vector2(1f, 1f), Vector2.down, 5f, QueryLayer.Solids, out var hit));
            Assert.AreEqual(0.5f, hit.Distance, 1e-5f);
            Assert.AreEqual(Vector2.up, hit.Normal);
        }

        [Test]
        public void CastBox_SlidingAlongSurface_DoesNotHit()
        {
            // Caixa apoiada exatamente sobre o chão (faces se tocam) andando para o lado: sem contato.
            Assert.IsFalse(world.CastBox(new Vector2(5f, 1.5f), new Vector2(1f, 1f), Vector2.right, 3f, QueryLayer.Solids, out _));
        }

        [Test]
        public void CastBox_OutOfRange_Misses()
        {
            Assert.IsFalse(world.CastBox(new Vector2(5f, 10f), new Vector2(1f, 1f), Vector2.down, 2f, QueryLayer.Solids, out _));
        }

        [Test]
        public void Layers_FilterKinds()
        {
            Vector2 size = new Vector2(1f, 1f);
            Assert.IsTrue(world.CastBox(new Vector2(18f, 5f), size, Vector2.right, 5f, QueryLayer.Solids, out _));
            Assert.IsFalse(world.CastBox(new Vector2(18f, 5f), size, Vector2.right, 5f, QueryLayer.WallJumpable, out _));
            Assert.IsTrue(world.OverlapBox(new Vector2(41f, 1f), size, QueryLayer.KillZones));
            Assert.IsFalse(world.OverlapBox(new Vector2(41f, 1f), size, QueryLayer.Solids));
        }

        [Test]
        public void DS11_EnemyOverlappingAtStart_IsIgnoredByCast()
        {
            Assert.IsFalse(world.CastBox(new Vector2(30.5f, 1f), new Vector2(1f, 1f), Vector2.right, 3f, QueryLayer.Solids, out _));
            Assert.IsTrue(world.CastBox(new Vector2(28f, 1f), new Vector2(1f, 1f), Vector2.right, 3f, QueryLayer.Solids, out var hit));
            Assert.AreEqual(1.5f, hit.Distance, 1e-5f);
        }

        [Test]
        public void CastBox_StartingInsideGround_HitsAtZero()
        {
            Assert.IsTrue(world.CastBox(new Vector2(5f, 0.5f), new Vector2(1f, 1f), Vector2.right, 1f, QueryLayer.Solids, out var hit));
            Assert.AreEqual(0f, hit.Distance);
        }

        [Test]
        public void Raycast_HitsTopFace()
        {
            Assert.IsTrue(world.Raycast(new Vector2(3f, 5f), Vector2.down, 10f, QueryLayer.Solids, out var hit));
            Assert.AreEqual(4f, hit.Distance, 1e-5f);
            Assert.AreEqual(Vector2.up, hit.Normal);
            Assert.IsFalse(world.Raycast(new Vector2(3f, 5f), Vector2.up, 10f, QueryLayer.Solids, out _));
        }

        [Test]
        public void Raycast_Diagonal()
        {
            Vector2 dir = new Vector2(1f, -1f).normalized;
            Assert.IsTrue(world.Raycast(new Vector2(2f, 3f), dir, 10f, QueryLayer.Solids, out var hit));
            Assert.AreEqual(2f * Mathf.Sqrt(2f), hit.Distance, 1e-4f);
        }

        [Test]
        public void GrappleTargets_WithinRadius()
        {
            world.AddGrapplePoint(new Vector2(0f, 9f));
            world.AddGrapplePoint(new Vector2(0f, 20f));
            var results = new Vector2[4];
            Assert.AreEqual(1, world.FindGrappleTargets(Vector2.zero, 9f, results));
            Assert.AreEqual(new Vector2(0f, 9f), results[0]);
        }

        [Test]
        public void Broadphase_ManyBoxes_SameAnswerAsSingle()
        {
            var big = new AabbCollisionWorld();
            for (int x = -100; x < 100; x++) big.AddBox(new Vector2(x, -1f), new Vector2(x + 1, 0f), AabbKind.Ground);
            Assert.IsTrue(big.CastBox(new Vector2(37.3f, 5f), new Vector2(0.5f, 1f), Vector2.down, 10f, QueryLayer.Solids, out var hit));
            Assert.AreEqual(4.5f, hit.Distance, 1e-5f);
            Assert.IsFalse(big.CastBox(new Vector2(37.3f, 0.5f + 1e-4f), new Vector2(0.5f, 1f), Vector2.right, 20f, QueryLayer.Solids, out _),
                "emendas entre tiles não geram contato de quem anda sobre elas");
        }
    }
}
