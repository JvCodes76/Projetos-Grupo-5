using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using Roguelike.Movement;
using UnityEngine;

namespace Roguelike.Tests.Levels
{
    /// <summary>
    /// D1 (a) e RF-42 nas cenas reais, já com as edições da M.16: o kit base alcança o objetivo das 3 fases (SPEC §16) e
    /// nenhuma superfície alcançável deixa o jogador sem volta ao objetivo.
    /// </summary>
    [Category("Levels")]
    public class ReachabilityTests
    {
        private static IEnumerable<string> Phases => LevelTestContext.PhaseScenes;

        [TestCaseSource(nameof(Phases))]
        public void BaseKit_ReachesGoal(string scenePath)
        {
            LevelTestContext ctx = LevelTestContext.Open(scenePath);
            Assert.IsTrue(ctx.Level.HasSpawn, "sem SpawnPoint");
            Assert.IsNotEmpty(ctx.Level.Goals, "sem objetivo");

            var model = new ReachabilityModel(ctx.Profile, ctx.Level.World, ctx.KillPlaneY);
            ReachabilityReport report = model.Run(ctx.Stats(), ctx.Level.Spawn, ctx.Level.Goals, "base");
            Assert.IsTrue(report.GoalReached, model.ToMarkdown(ctx.Name, new[] { report }, ctx.Level.Goals));
        }

        [TestCaseSource(nameof(Phases))]
        [Category("Slow")]
        public void BaseKit_HasNoSoftLocks(string scenePath)
        {
            LevelTestContext ctx = LevelTestContext.Open(scenePath);
            var model = new ReachabilityModel(ctx.Profile, ctx.Level.World, ctx.KillPlaneY);
            List<int> locked = model.FindSoftLocks(ctx.Stats(), ctx.Level.Spawn, ctx.Level.Goals, out int reachable);
            Assert.Greater(reachable, 1);

            var sb = new StringBuilder();
            foreach (int i in locked) sb.AppendLine(model.Surfaces[i].ToString());
            Assert.IsEmpty(locked, $"{ctx.Name}: {locked.Count}/{reachable} superfícies sem volta ao objetivo\n{sb}");
        }
    }
}
