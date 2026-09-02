using System;

public static class GameModeSelection
{
    public static GameMode SelectedMode { get; private set; } =
        GameMode.RoundLimited;

    public static void Select(GameMode gameMode)
    {
        if (gameMode != GameMode.RoundLimited && gameMode != GameMode.Endless)
        {
            throw new ArgumentOutOfRangeException(nameof(gameMode));
        }

        SelectedMode = gameMode;
    }
}
