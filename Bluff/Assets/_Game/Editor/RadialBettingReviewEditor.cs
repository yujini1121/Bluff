using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

// An explicit request file lets the already-open Editor run the same menu commands.
// It never saves or discards an unrelated dirty scene.
[InitializeOnLoad]
public static class RadialBettingReviewEditor
{
    private const string Request = "Logs/RadialReview.request";
    private const string Running = "Bluff.RadialReview.Running";
    private static TestRunnerApi runner;
    static RadialBettingReviewEditor()
    {
        runner = ScriptableObject.CreateInstance<TestRunnerApi>();
        runner.RegisterCallbacks(new Results());
        EditorApplication.update += CheckRequest;
    }
    private static void CheckRequest()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating ||
            EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists(Request)) return;
        string command;
        try
        {
            command = File.ReadAllText(Request).Trim();
            File.Delete(Request);
        }
        catch (IOException) { return; } // The writer may still own the file this frame.
        try
        {
            if (command == "build") RadialBettingSetupEditor.Build();
            else if (command == "test") RunTests();
            else if (command == "shift-test") RunTests(true);
            else if (command == "shift-open")
            {
                RequireCleanScenes();
                EditorSceneManager.OpenScene("Assets/_Game/Scenes/UI_Yujin_Shift.unity");
            }
            else throw new InvalidOperationException("Unknown radial review request.");
            File.WriteAllText("Logs/RadialReview.status", command + " dispatched " + DateTime.Now);
        }
        catch (Exception e) { File.WriteAllText("Logs/RadialReview.status", e.ToString()); Debug.LogException(e); }
    }
    [MenuItem("Tools/Bluff/Run Radial Betting Review")]
    public static void RunTests()
        => RunTests(false);

    private static void RequireCleanScenes()
    {
        for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
            if (UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty)
                throw new InvalidOperationException("Save the open scenes before running the Play review. No edits were discarded.");
    }

    private static void RunTests(bool shift)
    {
        RequireCleanScenes();
        SessionState.SetBool(Running, true);
        SessionState.SetBool("Bluff.RadialReview.Shift", shift);
        runner.Execute(new ExecutionSettings(new Filter
        {
            testMode = TestMode.EditMode,
            groupNames = shift
                ? new[] { "^ShiftRadialBettingTests", "^RadialBettingTests", "^BettingActionTests", "^RoundTransitionTests", "^FoldPenaltyTests" }
                : new[] { "^RadialBettingTests", "^BettingActionTests", "^RoundTransitionTests", "^FoldPenaltyTests" }
        }));
    }
    private sealed class Results : ICallbacks
    {
        public void RunStarted(ITestAdaptor testsToRun) { }
        public void TestStarted(ITestAdaptor test) { }
        public void TestFinished(ITestResultAdaptor result) { }
        public void RunFinished(ITestResultAdaptor result)
        {
            if (!SessionState.GetBool(Running, false)) return;
            SessionState.SetBool(Running, false);
            string directory = SessionState.GetBool("Bluff.RadialReview.Shift", false)
                ? "Logs/ShiftRadialReview" : "Logs/RadialReview";
            Directory.CreateDirectory(directory);
            File.WriteAllText(directory + "/Results.xml", result.ToXml().OuterXml);
            File.WriteAllText("Logs/RadialReview.status", $"Tests: {result.PassCount} passed / {result.FailCount} failed / {result.SkipCount} skipped");
        }
    }
}
