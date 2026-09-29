using System;
using System.Collections;
using NUnit.Framework;
using Roguelike.Events;
using Roguelike.Movement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Profiling;
using UnityEditor.SceneManagement;
using UnityEditorInternal;
using UnityEngine.Profiling;
#endif

namespace Roguelike.Tests.PlayMode
{
    /// <summary>
    /// Smoke test do controlador no jogo de verdade (SPEC §16, categoria PlayMode): carrega o gym, injeta input por tick
    /// via <see cref="IPlayerSimulationProbe"/> e confere velocidade máxima (RF-02), feedback no mesmo frame do pulo
    /// (RF-53), 0 B de GC por frame (RNF-09), EndGoal / bala / KillZone (RNF-08) e troca de cena sem pulo (RF-63).
    /// </summary>
    [Category("PlayMode")]
    public class MovementSmokeTests
    {
        private const string GymPath = "Assets/Scenes/Dev/MovementGym.unity";
        private const string EndGoalPrefabPath = "Assets/Prefabs/EndGoal.prefab";
        private const string BulletPrefabPath = "Assets/Prefabs/Bullet.prefab";

        // Trecho plano do gym (x 99..130, chão em y = 0), longe dos degraus e dos vãos.
        private static readonly Vector2 FlatStart = new Vector2(107f, 0.05f);

        private IPlayerSimulationProbe probe;
        private IPlayerFeedbackProbe feedback;
        private ScriptedInput script;
        private Keyboard keyboard;
        private InputSettings originalInputSettings;
        private InputSettings testInputSettings;

        /// <summary>Input por tick: função do índice do tick desde que o roteiro começou.</summary>
        private sealed class ScriptedInput : IInputSource
        {
            public Func<long, TickInput> Script;
            public long Tick;

            public TickInput NextTick()
            {
                TickInput input = Script != null ? Script(Tick) : default;
                Tick++;
                return input;
            }

            public void Run(Func<long, TickInput> script)
            {
                Script = script;
                Tick = 0;
            }
        }

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return LoadGym(true);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (keyboard != null)
            {
                InputSystem.RemoveDevice(keyboard);
                keyboard = null;
            }

            if (testInputSettings != null)
            {
                InputSystem.settings = originalInputSettings;
                UnityEngine.Object.Destroy(testInputSettings);
                testInputSettings = null;
            }

            yield return null;
        }

        private IEnumerator LoadGym(bool scripted)
        {
            probe = null;
            feedback = null;
#if UNITY_EDITOR
            AsyncOperation op = EditorSceneManager.LoadSceneAsyncInPlayMode(GymPath, new LoadSceneParameters(LoadSceneMode.Single));
            while (!op.isDone) yield return null;
#else
            Assert.Ignore("O smoke test carrega o gym pelo caminho do asset (só no Editor).");
#endif
            // O DevPlayBootstrap instancia o jogador no Start.
            for (int i = 0; i < 60 && probe == null; i++)
            {
                yield return null;
                foreach (MonoBehaviour mb in UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
                {
                    if (mb is IPlayerSimulationProbe p) probe = p;
                    if (mb is IPlayerFeedbackProbe f) feedback = f;
                }
            }

            Assert.NotNull(probe, "o gym não criou o jogador");
            if (!scripted) yield break;
            script = new ScriptedInput();
            probe.SetInputOverride(script);
        }

        private Vector2 Feet => probe.Motor.State.Position;

        private IEnumerator TeleportAndSettle(Vector2 feet)
        {
            script.Run(null);
            probe.Motor.QueueTeleport(feet, false, RespawnReason.ReturnToSpawn);
            for (int i = 0; i < 20; i++) yield return null;
        }

        private static TickInput Move(int x) => new TickInput((sbyte)x, 0, ButtonBits.None, ButtonBits.None);

        private static TickInput PressJump(int x = 0) => new TickInput((sbyte)x, 0, ButtonBits.Jump, ButtonBits.Jump);

        // Espera N ticks de simulação (em batchmode o frame pode ser bem mais curto que 1/60 s).
        private IEnumerator WaitTicks(long ticks)
        {
            long target = probe.CurrentTick + ticks;
            float deadline = Time.realtimeSinceStartup + ticks / 60f * 3f + 2f;
            while (probe.CurrentTick < target && Time.realtimeSinceStartup < deadline) yield return null;
        }

        [UnityTest]
        public IEnumerator Run_ReachesMaxSpeedOfTenUnitsPerSecond()
        {
            yield return TeleportAndSettle(FlatStart);
            script.Run(_ => Move(1));
            yield return WaitTicks(20); // acelera (0 → 10 u/s em ~0,1 s)

            long t0 = probe.CurrentTick;
            float x0 = Feet.x;
            yield return WaitTicks(30);
            float speed = (Feet.x - x0) / ((probe.CurrentTick - t0) / 60f);
            Assert.AreEqual(10f, speed, 0.2f, "RF-02: velocidade máxima medida");
        }

        [UnityTest]
        public IEnumerator Jump_FeedbackInSameFrame()
        {
            yield return TeleportAndSettle(FlatStart);
            int jumpFrame = -1, feedbackFrame = -2;
            FeedbackCue cue = FeedbackCue.None;
            Action<PlayerJumped> onJump = _ =>
            {
                // O PlayerFeedback assinou antes (OnEnable do jogador): já tratou este evento.
                jumpFrame = Time.frameCount;
                feedbackFrame = feedback != null ? feedback.LastFeedbackFrame : -3;
                cue = feedback != null ? feedback.LastCue : FeedbackCue.None;
            };

            EventBus<PlayerJumped>.Subscribe(onJump);
            try
            {
                script.Run(t => t == 0 ? PressJump() : default);
                yield return WaitTicks(10);
            }
            finally
            {
                EventBus<PlayerJumped>.Unsubscribe(onJump);
            }

            Assert.AreNotEqual(-1, jumpFrame, "o pulo não saiu");
            Assert.NotNull(feedback, "sem IPlayerFeedbackProbe no jogador");
            Assert.AreEqual(jumpFrame, feedbackFrame, "RF-53: feedback no mesmo frame do PlayerJumped");
            Assert.AreEqual(FeedbackCue.Jump, cue);
        }

        /// <summary>
        /// RNF-09 no Editor: o contador "GC Allocated In Frame" soma também o EditorLoop (≈ 450 B constantes por frame,
        /// medidos, que não existem no build) e o coroutine do próprio test runner. Por isso o teste liga o Profiler por
        /// ~300 frames a 60 fps e soma as amostras GC.Alloc dentro do PlayerLoop (onde rodam simulação, views, câmera e
        /// áudio), fora do coroutine do runner.
        /// </summary>
        [UnityTest]
        public IEnumerator Simulation_AllocatesNoGarbagePerFrame()
        {
#if UNITY_EDITOR
            int oldFrameRate = Application.targetFrameRate;
            Application.targetFrameRate = 60;
            try
            {
                yield return TeleportAndSettle(FlatStart);
                // Corre para lá e para cá pulando: pulos, pousos, passos e câmera no caminho.
                script.Run(t => new TickInput((sbyte)((t / 90) % 2 == 0 ? 1 : -1), 0,
                    t % 45 == 0 ? ButtonBits.Jump : ButtonBits.None, t % 45 < 10 ? ButtonBits.Jump : ButtonBits.None));
                yield return new WaitForSecondsRealtime(2f); // aquecimento (pools, primeiro uso de cada cue)

                long startTick = probe.CurrentTick;
                ProfilerDriver.ClearAllFrames();
                ProfilerDriver.enabled = true;
                Profiler.enabled = true;
                yield return new WaitForSecondsRealtime(5f);
                Profiler.enabled = false;
                ProfilerDriver.enabled = false;

                int frames = 0, allocating = 0;
                long total = 0;
                var where = new System.Text.StringBuilder();
                for (int f = ProfilerDriver.firstFrameIndex; f <= ProfilerDriver.lastFrameIndex && f >= 0; f++)
                {
                    using (HierarchyFrameDataView view = ProfilerDriver.GetHierarchyFrameDataView(f, 0,
                        HierarchyFrameDataView.ViewModes.Default, HierarchyFrameDataView.columnGcMemory, false))
                    {
                        if (view == null || !view.valid) continue;
                        frames++;
                        long bytes = PlayerLoopGc(view, view.GetRootItemID(), string.Empty, false, where);
                        if (bytes <= 0) continue;
                        allocating++;
                        total += bytes;
                    }
                }

                Assert.GreaterOrEqual(frames, 250, "frames analisados");
                Assert.GreaterOrEqual(probe.CurrentTick - startTick, 250, "ticks medidos");
                Assert.AreEqual(0, allocating, $"RNF-09: {allocating}/{frames} frames com GC ({total} B): {where}");
            }
            finally
            {
                Application.targetFrameRate = oldFrameRate;
                Profiler.enabled = false;
                ProfilerDriver.enabled = false;
            }
#else
            Assert.Ignore("A medição por amostras do Profiler usa a API do Editor.");
            yield break;
#endif
        }

#if UNITY_EDITOR
        // Soma os GC.Alloc sob o PlayerLoop, ignorando o coroutine do test runner.
        private static long PlayerLoopGc(HierarchyFrameDataView view, int id, string path, bool inPlayerLoop, System.Text.StringBuilder where)
        {
            long sum = 0;
            var children = new System.Collections.Generic.List<int>();
            view.GetItemChildren(id, children);
            foreach (int child in children)
            {
                float gc = view.GetItemColumnDataAsFloat(child, HierarchyFrameDataView.columnGcMemory);
                if (gc <= 0f) continue;
                string name = view.GetItemName(child);
                if (name.Contains("UnityEngine.TestRunner") || name.Contains("PlaymodeTestsController")) continue;
                bool player = inPlayerLoop || name == "PlayerLoop";
                if (name == "GC.Alloc")
                {
                    if (!player) continue;
                    sum += (long)gc;
                    if (where.Length < 600) where.Append(path).Append(": ").Append((long)gc).Append(" B; ");
                    continue;
                }

                sum += PlayerLoopGc(view, child, path.Length == 0 ? name : path + " > " + name, player, where);
            }

            return sum;
        }
#endif

        [UnityTest]
        public IEnumerator KillZone_SendsPlayerBackToSafeGround()
        {
            yield return TeleportAndSettle(FlatStart);
            bool fellOut = false, respawned = false;
            Action<PlayerFellOut> onFell = _ => fellOut = true;
            Action<PlayerRespawned> onRespawn = _ => respawned = true;
            EventBus<PlayerFellOut>.Subscribe(onFell);
            EventBus<PlayerRespawned>.Subscribe(onRespawn);
            try
            {
                // Poço de 9 u (x 90..99) com KillZone no fundo.
                probe.Motor.QueueTeleport(new Vector2(94.5f, 2f), false, RespawnReason.ReturnToSpawn);
                yield return WaitTicks(120);
            }
            finally
            {
                EventBus<PlayerFellOut>.Unsubscribe(onFell);
                EventBus<PlayerRespawned>.Unsubscribe(onRespawn);
            }

            Assert.IsTrue(fellOut, "RNF-08: a KillZone não disparou");
            Assert.IsTrue(respawned, "RF-42: sem respawn depois da queda");
            Assert.Greater(Feet.y, -0.5f, "voltou para o chão seguro");
        }

        [UnityTest]
        public IEnumerator EndGoal_TriggersGoalReached()
        {
            yield return TeleportAndSettle(FlatStart);
            GameObject goal = Spawn(EndGoalPrefabPath, FlatStart + new Vector2(3f, 1f));
            bool reached = false;
            Action<LevelGoalReached> onGoal = _ => reached = true;
            EventBus<LevelGoalReached>.Subscribe(onGoal);
            try
            {
                script.Run(_ => Move(1));
                yield return WaitTicks(60);
            }
            finally
            {
                EventBus<LevelGoalReached>.Unsubscribe(onGoal);
                UnityEngine.Object.Destroy(goal);
            }

            Assert.IsTrue(reached, "RNF-08: EndGoal não disparou");
        }

        [UnityTest]
        public IEnumerator EnemyBullet_KillsPlayer()
        {
            yield return TeleportAndSettle(FlatStart);
            GameObject bullet = Spawn(BulletPrefabPath, Feet + new Vector2(2.5f, 0.6f));
            bullet.SendMessage("SetDirection", Vector2.left, SendMessageOptions.RequireReceiver);
            bool died = false;
            Action<PlayerDied> onDied = _ => died = true;
            EventBus<PlayerDied>.Subscribe(onDied);
            try
            {
                yield return WaitTicks(90);
            }
            finally
            {
                EventBus<PlayerDied>.Unsubscribe(onDied);
                if (bullet != null) UnityEngine.Object.Destroy(bullet);
            }

            Assert.IsTrue(died, "RNF-08: a bala não matou");
        }

        [UnityTest]
        public IEnumerator SceneChange_WithJumpHeld_DoesNotJump()
        {
            // Em batchmode o Editor não tem foco: sem isto o Input System descarta o teclado (configuração só do teste).
            originalInputSettings = InputSystem.settings;
            testInputSettings = ScriptableObject.Instantiate(originalInputSettings);
            testInputSettings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            testInputSettings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings = testInputSettings;
            keyboard = InputSystem.AddDevice<Keyboard>();
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Space));
            yield return null;

            int jumps = 0;
            Action<PlayerJumped> onJump = _ => jumps++;
            EventBus<PlayerJumped>.Subscribe(onJump);
            try
            {
                // Recarrega o gym com o pulo segurado desde antes (input real, sem override).
                yield return LoadGym(false);
                yield return WaitTicks(60);
                Assert.AreEqual(0, jumps, "RF-63: o botão segurado na troca de cena virou pulo");

                // Sanidade: soltar e apertar de novo pula.
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                yield return WaitTicks(5);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Space));
                yield return WaitTicks(10);
                Assert.AreEqual(1, jumps, "o teclado virtual não chegou ao jogador");
            }
            finally
            {
                EventBus<PlayerJumped>.Unsubscribe(onJump);
            }
        }

        private static GameObject Spawn(string prefabPath, Vector2 position)
        {
#if UNITY_EDITOR
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            Assert.NotNull(prefab, prefabPath);
            return UnityEngine.Object.Instantiate(prefab, position, Quaternion.identity);
#else
            Assert.Ignore("Prefabs carregados pelo caminho do asset (só no Editor).");
            return null;
#endif
        }
    }
}
