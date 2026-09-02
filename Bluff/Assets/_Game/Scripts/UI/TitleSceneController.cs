using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class TitleSceneController : MonoBehaviour
{
    private const string GameplaySceneName = "Dev_Yujin";
    private const float ButtonWidth = 260f;
    private const float ButtonHeight = 52f;
    private const float ButtonGap = 16f;

    private void OnGUI()
    {
        float left = (Screen.width - ButtonWidth) * 0.5f;
        float firstTop = Screen.height * 0.62f;

        if (GUI.Button(
                new Rect(left, firstTop, ButtonWidth, ButtonHeight),
                "ROUND LIMITED"))
        {
            StartGame(GameMode.RoundLimited);
        }

        if (GUI.Button(
                new Rect(
                    left,
                    firstTop + ButtonHeight + ButtonGap,
                    ButtonWidth,
                    ButtonHeight),
                "ENDLESS"))
        {
            StartGame(GameMode.Endless);
        }
    }

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
