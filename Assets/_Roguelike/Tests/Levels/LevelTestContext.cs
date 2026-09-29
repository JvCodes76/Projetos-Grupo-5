using System;
using System.Collections.Generic;
using Roguelike.Movement;
using Roguelike.MovementPhysics;
using Roguelike.Upgrades;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Roguelike.Tests.Levels
{
    /// <summary>
    /// Cena real carregada para os testes de fase (SPEC §16, categoria Levels): a mesma geometria em AABB
    /// (<see cref="LevelAabbExtractor"/>) e no Physics2D (<see cref="Physics2DCollisionWorld"/>), o perfil padrão e o plano
    /// de queda do CameraBoundary da fase.
    /// </summary>
    internal sealed class LevelTestContext
    {
        public const string ProfilePath = "Assets/_Roguelike/Data/Movement/MovementProfile_Default.asset";

        /// <summary>As 3 fases do Build Settings (as mesmas do ReachabilityWindow).</summary>
        public static readonly string[] PhaseScenes =
        {
            "Assets/Scenes/PrimeiraFase.unity",
            "Assets/Scenes/QuartaFase 1.unity",
            "Assets/Scenes/QuintaFase.unity",
        };

        public Scene Scene;
        public ExtractedLevel Level;
        public Physics2DCollisionWorld Physics;
        public MovementProfile Profile;
        public float KillPlaneY;

        public static LevelTestContext Open(string scenePath)
        {
            var ctx = new LevelTestContext();
            ctx.Scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            Physics2D.SyncTransforms();
            ctx.Level = LevelAabbExtractor.Extract(ctx.Scene);
            ctx.Physics = new Physics2DCollisionWorld();
            ctx.Profile = AssetDatabase.LoadAssetAtPath<MovementProfile>(ProfilePath);
            if (ctx.Profile == null) throw new InvalidOperationException("MovementProfile_Default não encontrado em " + ProfilePath);
            ctx.KillPlaneY = ReadKillPlane(ctx.Scene, ctx.Level.Bounds.yMin - 4f);
            return ctx;
        }

        public string Name => Scene.name;

        public MovementStats Stats(AbilityFlags abilities = AbilityFlags.None, int airJumps = 0)
        {
            return MovementStatsResolver.Resolve(Profile, new KitStatInput(Profile, abilities, airJumps, 0));
        }

        /// <summary>Plano de queda como o jogo usa (CameraBoundary.KillPlaneY), lido por reflexão (Assembly-CSharp).</summary>
        private static float ReadKillPlane(Scene scene, float fallback)
        {
            Type type = Type.GetType("CameraBoundary, Assembly-CSharp");
            if (type == null) return fallback;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                Component c = root.GetComponentInChildren(type, true);
                if (c == null) continue;
                object value = type.GetProperty("KillPlaneY")?.GetValue(c);
                return value is float f ? Math.Max(f, fallback) : fallback;
            }

            return fallback;
        }

        public IEnumerable<AabbCollisionWorld.Box> Boxes()
        {
            for (int i = 0; i < Level.World.BoxCount; i++) yield return Level.World.GetBox(i);
        }
    }
}
