using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

namespace Roguelike.Tests.PlayMode
{
    /// <summary>
    /// M.17 na cena SettingsMenu: os botões abrem os painéis, as linhas cabem na janela, um rebinding feito pela UI vai
    /// para o PlayerPrefs e volta num InputActionAsset novo (persistência entre sessões, RF-64) e as opções de feedback
    /// gravam na hora (RF-65). Restaura o PlayerPrefs do Editor no fim.
    /// </summary>
    [Category("PlayMode")]
    public class SettingsMenuTests
    {
        private const string ScenePath = "Assets/Scenes/SettingsMenu.unity";
        private const string ActionsPath = "Assets/PlayerControls.inputactions";
        private const string OverridesKey = "input.bindingOverrides";
        private const string ShakeKey = "feedback.shakeScale";

        private readonly Dictionary<string, string> savedStrings = new Dictionary<string, string>();
        private readonly Dictionary<string, float> savedFloats = new Dictionary<string, float>();
        private InputSettings originalInputSettings;
        private InputSettings testInputSettings;
        private Keyboard keyboard;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            SaveString(OverridesKey);
            SaveFloat(ShakeKey);
#if UNITY_EDITOR
            AsyncOperation op = EditorSceneManager.LoadSceneAsyncInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
            while (!op.isDone) yield return null;
#else
            Assert.Ignore("Carrega a cena pelo caminho do asset (só no Editor).");
#endif
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (keyboard != null) InputSystem.RemoveDevice(keyboard);
            keyboard = null;
            if (testInputSettings != null)
            {
                InputSystem.settings = originalInputSettings;
                Object.Destroy(testInputSettings);
                testInputSettings = null;
            }

            foreach (KeyValuePair<string, string> kv in savedStrings)
            {
                if (kv.Value == null) PlayerPrefs.DeleteKey(kv.Key);
                else PlayerPrefs.SetString(kv.Key, kv.Value);
            }

            foreach (KeyValuePair<string, float> kv in savedFloats)
            {
                if (float.IsNaN(kv.Value)) PlayerPrefs.DeleteKey(kv.Key);
                else PlayerPrefs.SetFloat(kv.Key, kv.Value);
            }

            PlayerPrefs.Save();
            savedStrings.Clear();
            savedFloats.Clear();
            yield return null;
        }

        [UnityTest]
        public IEnumerator Panels_OpenFromButtons_AndFitTheWindow()
        {
            foreach (string opener in new[] { "ACESSIBILIDADE", "CONTROLES" })
            {
                Button button = FindButton(null, opener);
                Assert.NotNull(button, $"botão '{opener}' na SettingsMenu");
                button.onClick.Invoke();
                yield return null;
                Canvas.ForceUpdateCanvases();

                GameObject panel = GameObject.Find(opener == "CONTROLES" ? "RebindingPanel" : "FeedbackOptionsPanel");
                Assert.NotNull(panel, "painel aberto");
                var window = (RectTransform)panel.transform.Find("Janela/Fundo");
                Rect windowRect = WorldRect(window);
                Button[] buttons = panel.GetComponentsInChildren<Button>();
                Assert.GreaterOrEqual(buttons.Length, opener == "CONTROLES" ? 20 : 6);
                foreach (Button b in buttons)
                {
                    Rect r = WorldRect((RectTransform)b.transform);
                    Vector2 tolerance = Vector2.one * 0.5f;
                    Assert.IsTrue(windowRect.Contains(r.min + tolerance) && windowRect.Contains(r.max - tolerance),
                        $"{opener}: botão '{Label(b)}' fora da janela ({r} × {windowRect})");
                    Assert.IsFalse(string.IsNullOrEmpty(Label(b)), $"{opener}: botão sem texto");
                }

                FindButton(panel.transform, "VOLTAR").onClick.Invoke();
                yield return null;
                Assert.IsFalse(panel.activeSelf, "VOLTAR fecha o painel");
            }
        }

        [UnityTest]
        public IEnumerator Rebind_FromUi_PersistsAcrossSessions()
        {
            UseVirtualKeyboard();
            FindButton(null, "CONTROLES").onClick.Invoke();
            yield return null;
            GameObject panel = GameObject.Find("RebindingPanel");
            Button jumpKeyboard = RowButton(panel.transform, "Pulo", 0);
            Assert.NotNull(jumpKeyboard, "célula Pulo × Teclado");
            Assert.AreEqual("SPACE", Label(jumpKeyboard));

            jumpKeyboard.onClick.Invoke();
            yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.J));
            yield return new WaitForSecondsRealtime(0.3f);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return new WaitForSecondsRealtime(0.1f);

            Assert.AreEqual("J", Label(jumpKeyboard), "a célula mostra o novo botão");
            string json = PlayerPrefs.GetString(OverridesKey, string.Empty);
            StringAssert.Contains("<Keyboard>/j", json, "override gravado no PlayerPrefs");

#if UNITY_EDITOR
            // "Próxima sessão": um asset novo carregando o JSON salvo usa o binding novo.
            var asset = Object.Instantiate(AssetDatabase.LoadAssetAtPath<InputActionAsset>(ActionsPath));
            asset.LoadBindingOverridesFromJson(json);
            InputAction jump = asset.FindAction("Player/Jump", true);
            Assert.AreEqual("<Keyboard>/j", jump.bindings[0].effectivePath);
            Object.Destroy(asset);
#endif

            FindButton(panel.transform, "RESTAURAR PADRÃO").onClick.Invoke();
            yield return null;
            Assert.AreEqual("SPACE", Label(jumpKeyboard), "restaurar volta o padrão");
            Assert.IsFalse(PlayerPrefs.HasKey(OverridesKey), "restaurar apaga os overrides salvos");
        }

        [UnityTest]
        public IEnumerator FeedbackOption_CyclesAndSaves()
        {
            PlayerPrefs.DeleteKey(ShakeKey);
            FindButton(null, "ACESSIBILIDADE").onClick.Invoke();
            yield return null;
            GameObject panel = GameObject.Find("FeedbackOptionsPanel");
            Button shake = RowButton(panel.transform, "Tremor de tela", 0);
            Assert.AreEqual("50 %", Label(shake), "padrão do SPEC §10.4");

            shake.onClick.Invoke();
            yield return null;
            Assert.AreEqual("100 %", Label(shake));
            Assert.AreEqual(1f, PlayerPrefs.GetFloat(ShakeKey, -1f));
        }

        private void UseVirtualKeyboard()
        {
            // Em batchmode o Editor não tem foco: sem isto o Input System descarta o teclado (configuração só do teste).
            originalInputSettings = InputSystem.settings;
            testInputSettings = Object.Instantiate(originalInputSettings);
            testInputSettings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            testInputSettings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings = testInputSettings;
            keyboard = InputSystem.AddDevice<Keyboard>();
        }

        private void SaveString(string key) => savedStrings[key] = PlayerPrefs.HasKey(key) ? PlayerPrefs.GetString(key) : null;

        private void SaveFloat(string key) => savedFloats[key] = PlayerPrefs.HasKey(key) ? PlayerPrefs.GetFloat(key) : float.NaN;

        private static string Label(Button b)
        {
            var text = b.GetComponentInChildren<TMP_Text>(true);
            return text != null ? text.text : null;
        }

        private static Button FindButton(Transform root, string label)
        {
            IEnumerable<Button> buttons = root != null ? root.GetComponentsInChildren<Button>(true) : Object.FindObjectsByType<Button>(FindObjectsSortMode.None);
            foreach (Button b in buttons)
            {
                if (Label(b) == label) return b;
            }

            return null;
        }

        // Botão n da linha cujo rótulo é "label" (as linhas são HorizontalLayoutGroups: rótulo, célula, célula).
        private static Button RowButton(Transform root, string label, int index)
        {
            foreach (HorizontalLayoutGroup row in root.GetComponentsInChildren<HorizontalLayoutGroup>(true))
            {
                TMP_Text first = row.transform.childCount > 0 ? row.transform.GetChild(0).GetComponent<TMP_Text>() : null;
                if (first == null || first.text != label) continue;
                Button[] buttons = row.GetComponentsInChildren<Button>(true);
                return index < buttons.Length ? buttons[index] : null;
            }

            return null;
        }

        private static Rect WorldRect(RectTransform rt)
        {
            var corners = new Vector3[4];
            rt.GetWorldCorners(corners);
            return Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
        }
    }
}
