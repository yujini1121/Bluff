using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class TitleSceneController : MonoBehaviour
{
    private const string GameplaySceneName = "Intro";

    public void StartRoundLimited()
    {
        StartGame(GameMode.RoundLimited);
    }

    public void StartEndless()
    {
        StartGame(GameMode.Endless);
    }

    private static void StartGame(GameMode gameMode)
    {
        GameModeSelection.Select(gameMode);
        SceneManager.LoadScene(GameplaySceneName);
    }
}
