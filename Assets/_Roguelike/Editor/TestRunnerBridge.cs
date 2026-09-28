using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace Roguelike.EditorTools
{
    /// <summary>
    /// Roda os testes EditMode do roguelike e grava o resultado em JSON, para que agentes
    /// (via Unity MCP) e a linha de comando leiam o resultado sem depender da janela do Test Runner.
    ///
    /// Formas de chamar:
    /// - Menu "Tools/Roguelike/Rodar testes EditMode" (ex.: execute_menu_item pelo MCP).
    /// - Código: TestRunnerBridge.RunEditModeTests().
    /// - Linha de comando: Unity -batchmode -projectPath . -executeMethod
    ///   Roguelike.EditorTools.TestRunnerBridge.RunFromCommandLine [-resultsPath caminho.json]
    ///   (sai com código 0 se tudo passou, 1 se houve falha, 2 se não conseguiu rodar).
    ///
    /// O resultado vai para Temp/TestResults.json (a Unity apaga a pasta Temp ao fechar o Editor).
    /// A execução é síncrona: testes que levam vários frames ([UnityTest], [UnitySetUp]) são ignorados.
    /// </summary>
    public static class TestRunnerBridge
    {
        private const string MenuPath = "Tools/Roguelike/Rodar testes EditMode";
        private static readonly string[] TestAssemblies = { "Roguelike.Tests.EditMode" };

        public static string DefaultResultsPath =>
            Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Temp", "TestResults.json");

        [MenuItem(MenuPath)]
        public static void RunEditModeTests()
        {
            Run(DefaultResultsPath, null);
        }

        // Ponto de entrada para -executeMethod; encerra o Editor com o código de saída.
        public static void RunFromCommandLine()
        {
            string resultsPath = GetCommandLineArgument("-resultsPath") ?? DefaultResultsPath;
            bool started = Run(resultsPath, report => EditorApplication.Exit(report.failed > 0 ? 1 : 0));
            if (!started)
            {
                EditorApplication.Exit(2);
            }
        }

        /// <summary>
        /// Dispara os testes e grava o relatório em <paramref name="resultsPath"/>.
        /// Retorna false se não foi possível iniciar (o relatório de erro é gravado mesmo assim).
        /// </summary>
        public static bool Run(string resultsPath, Action<TestRunReport> onFinished)
        {
            var report = new TestRunReport
            {
                status = "running",
                startedAt = DateTime.Now.ToString("o")
            };

            if (EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                report.status = "error";
                report.error = "Editor compilando ou em Play Mode; tente de novo depois.";
                WriteReport(resultsPath, report);
                Debug.LogError("[TestRunnerBridge] - " + report.error);
                return false;
            }

            // Marca a execução como em andamento para ninguém ler um resultado antigo.
            WriteReport(resultsPath, report);

            var api = ScriptableObject.CreateInstance<TestRunnerApi>();
            var collector = new ResultCollector(report);
            collector.Finished = () =>
            {
                api.UnregisterCallbacks(collector);
                UnityEngine.Object.DestroyImmediate(api);
                WriteReport(resultsPath, report);
                Debug.Log($"[TestRunnerBridge] - {report.passed}/{report.total} testes passaram " +
                          $"({report.failed} falhas, {report.skipped} ignorados) em {report.durationSeconds:0.00}s -> {resultsPath}");
                onFinished?.Invoke(report);
            };
            api.RegisterCallbacks(collector);

            var filter = new Filter { testMode = TestMode.EditMode, assemblyNames = TestAssemblies };
            api.Execute(new ExecutionSettings(filter) { runSynchronously = true });
            return true;
        }

        private static void WriteReport(string path, TestRunReport report)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, JsonUtility.ToJson(report, true));
        }

        private static string GetCommandLineArgument(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == name)
                {
                    return args[i + 1];
                }
            }
            return null;
        }

        private sealed class ResultCollector : ICallbacks
        {
            private readonly TestRunReport report;
            public Action Finished;

            public ResultCollector(TestRunReport report)
            {
                this.report = report;
            }

            public void RunStarted(ITestAdaptor testsToRun) { }

            public void TestStarted(ITestAdaptor test) { }

            public void TestFinished(ITestResultAdaptor result)
            {
                // Só os casos de teste (folhas); suites e fixtures são agregadores.
                if (result.HasChildren)
                {
                    return;
                }

                report.tests.Add(new TestCaseReport
                {
                    fullName = result.FullName,
                    result = result.TestStatus.ToString(),
                    durationSeconds = result.Duration,
                    message = result.Message,
                    stackTrace = result.TestStatus == TestStatus.Failed ? result.StackTrace : null
                });

                report.total++;
                switch (result.TestStatus)
                {
                    case TestStatus.Passed: report.passed++; break;
                    case TestStatus.Failed: report.failed++; break;
                    case TestStatus.Skipped: report.skipped++; break;
                    default: report.inconclusive++; break;
                }
            }

            public void RunFinished(ITestResultAdaptor result)
            {
                report.status = "finished";
                report.finishedAt = DateTime.Now.ToString("o");
                report.durationSeconds = result.Duration;
                Finished?.Invoke();
            }
        }
    }

    [Serializable]
    public class TestRunReport
    {
        public string status;       // "running" | "finished" | "error"
        public string startedAt;    // ISO 8601, horário local
        public string finishedAt;
        public double durationSeconds;
        public int total;
        public int passed;
        public int failed;
        public int skipped;
        public int inconclusive;
        public string error;
        public List<TestCaseReport> tests = new List<TestCaseReport>();
    }

    [Serializable]
    public class TestCaseReport
    {
        public string fullName;
        public string result;       // Passed | Failed | Skipped | Inconclusive
        public double durationSeconds;
        public string message;
        public string stackTrace;
    }
}
