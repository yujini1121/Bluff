using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

// 열린 Editor에서 이 Intro의 실제 Game View와 Scene 전환을 검증합니다.
[InitializeOnLoad]
public static class IntroPlayReviewEditor
{
    public const string ReviewKey = "IntroFinal.VisualReview";
    private static TestRunnerApi runner;

    static IntroPlayReviewEditor()
    {
        runner = ScriptableObject.CreateInstance<TestRunnerApi>();
        runner.RegisterCallbacks(new ReviewCallbacks());
    }

    [MenuItem("Tools/Intro/Run Play Review")]
    public static void RunReview()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogError("Play Mode를 먼저 종료하세요.");
            return;
        }
        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().path != "Assets/_Game/Scenes/Intro.unity")
        {
            Debug.LogError("현재 Intro Scene을 열어 주세요.");
            return;
        }
        EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        Directory.CreateDirectory(Path.GetFullPath("Logs/IntroRebuildReview"));
        SessionState.SetBool(ReviewKey, true);
        runner.Execute(new ExecutionSettings(new Filter
        {
            testMode = TestMode.EditMode,
            groupNames = new[] { "^IntroFlowControllerTests", "^DialogueControllerTests",
                "^IntroDoorTriggerTests", "^IntroSceneSkeletonTests" }
        }));
    }

    private sealed class ReviewCallbacks : ICallbacks
    {
        public void RunStarted(ITestAdaptor testsToRun) { }
        public void TestStarted(ITestAdaptor test) { }
        public void TestFinished(ITestResultAdaptor result) { }

        public void RunFinished(ITestResultAdaptor result)
        {
            if (!SessionState.GetBool(ReviewKey, false))
            {
                return;
            }
            Directory.CreateDirectory(Path.GetFullPath("Logs/IntroRebuildReview"));
            File.WriteAllText(Path.GetFullPath("Logs/IntroRebuildReview/Results.xml"), result.ToXml().OuterXml);
            SessionState.SetBool(ReviewKey, false);
            Debug.Log("Intro 실제 Play Review: " + result.PassCount + " 통과 / " + result.FailCount +
                " 실패. Game View 캡처와 결과: Logs/IntroRebuildReview");
        }
    }
}

