using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class GameplayUiReviewEditor
{
    private const string RunKey = "Bluff.GameplayUiReview.Running";
    private const string SceneKey = "Bluff.GameplayUiReview.PreviousScene";
    private const string RequestPath = "Logs/UIParity/run-review.request";
    private static readonly TestRunnerApi Runner;

    static GameplayUiReviewEditor()
    {
        Runner = ScriptableObject.CreateInstance<TestRunnerApi>();
        Runner.RegisterCallbacks(new ReviewCallbacks());
        EditorApplication.update += CheckRequest;
    }

    private static void CheckRequest()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating ||
            EditorApplication.isPlayingOrWillChangePlaymode || SessionState.GetBool(RunKey, false) ||
            !File.Exists(RequestPath)) return;
        File.Delete(RequestPath);
        RunReview();
    }

    [MenuItem("Tools/Bluff/Run Gameplay UI Scene Review")]
    public static void RunReview()
    {
        Directory.CreateDirectory("Logs/UIParity");
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        for (int index = 0; index < SceneManager.sceneCount; index++)
        {
            if (!SceneManager.GetSceneAt(index).isDirty) continue;
            File.WriteAllText("Logs/UIParity/review-status.txt", "Save open scenes before running the UI review.");
            Debug.LogWarning("Save open scenes before running the UI review.");
            return;
        }
        SessionState.SetString(SceneKey, SceneManager.GetActiveScene().path);
        SessionState.SetBool(RunKey, true);
        File.WriteAllText("Logs/UIParity/review-status.txt", "Running UI scene and betting regression tests.");
        Runner.Execute(new ExecutionSettings(new Filter
        {
            testMode = TestMode.EditMode,
            groupNames = new[] { "^GameplayUiSceneTests", "^BettingActionTests", "^FoldPenaltyTests", "^RoundTransitionTests" }
        }));
    }

    private sealed class ReviewCallbacks : ICallbacks
    {
        public void RunStarted(ITestAdaptor testsToRun) { }
        public void TestStarted(ITestAdaptor test) { }
        public void TestFinished(ITestResultAdaptor result) { }
        public void RunFinished(ITestResultAdaptor result)
        {
            if (!SessionState.GetBool(RunKey, false)) return;
            Directory.CreateDirectory("Logs/UIParity");
            File.WriteAllText("Logs/UIParity/Results.xml", result.ToXml().OuterXml);
            string status = $"Gameplay UI review: {result.PassCount} passed / {result.FailCount} failed.";
            File.WriteAllText("Logs/UIParity/review-status.txt", status);
            SessionState.SetBool(RunKey, false);
            string previous = SessionState.GetString(SceneKey, "");
            EditorApplication.delayCall += () =>
            {
                if (!string.IsNullOrEmpty(previous) && File.Exists(previous)) EditorSceneManager.OpenScene(previous);
                Debug.Log(status);
            };
        }
    }
}
