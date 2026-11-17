using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

// Editor-only, one-shot preview. No match, scene or prefab data is saved.
[InitializeOnLoad]
public static class VictoryCutsceneQuickPreview
{
    private const string PendingKey = "Bluff.VictoryQuickPreview.Pending";
    private const string Menu = "Tools/Victory/승리 컷신 바로 보기 (1회 테스트)";
    private static VictoryCutsceneController preview;

    static VictoryCutsceneQuickPreview()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
    }

    [MenuItem(Menu)]
    public static void PlayOnce()
    {
        SessionState.SetBool(PendingKey, true);
        EditorApplication.EnterPlaymode();
    }

    [MenuItem(Menu, true)]
    private static bool CanPlayOnce()
    {
        return !EditorApplication.isPlayingOrWillChangePlaymode &&
            !EditorApplication.isCompiling &&
            SceneManager.GetActiveScene().path == VictoryCutsceneSetupEditor.ScenePath &&
            Object.FindObjectOfType<VictoryCutsceneController>(true) != null;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (SessionState.GetBool(PendingKey, false)) SuspendGameplay();
    }

    private static void SuspendGameplay()
    {
        // Run before Start so the opening deal and dealer AI never begin.
        foreach (var game in Object.FindObjectsOfType<GameplayController>())
        {
            game.enabled = false;
            game.StopAllCoroutines();
        }
    }

    private static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredPlayMode &&
            SessionState.GetBool(PendingKey, false))
        {
            SessionState.SetBool(PendingKey, false);
            SuspendGameplay();
            preview = Object.FindObjectOfType<VictoryCutsceneController>();
            Time.timeScale = 1f;
            if (preview != null)
            {
                preview.ResetForGame();
                preview.Completed += FinishPreview;
                if (preview.TryPlay(GamePhase.GameOver, GameWinner.Player, true)) return;
            }
            Debug.LogError("승리 컷신 테스트를 시작하지 못했습니다. Victory Cutscene 참조를 확인해주세요.");
            FinishPreview();
        }
        else if (state == PlayModeStateChange.ExitingPlayMode ||
                 state == PlayModeStateChange.EnteredEditMode)
        {
            SessionState.SetBool(PendingKey, false);
            if (preview != null) preview.Completed -= FinishPreview;
            preview = null;
        }
    }

    private static void FinishPreview()
    {
        if (preview != null) preview.Completed -= FinishPreview;
        preview = null;
        EditorApplication.delayCall += ExitPreview;
    }

    private static void ExitPreview()
    {
        if (EditorApplication.isPlaying) EditorApplication.ExitPlaymode();
    }
}
