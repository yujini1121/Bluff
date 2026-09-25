using System;

// SFX 새 클립을 추가하려고 할 때,
// 1. GameplayPresentationCue에 Cue 추가
// 2. 아래처럼 switch-case문에 SoundSystem이랑 연결해서 케이스별 실제 효과음 재생 추가
// 해주고 나한테 보고하면 제가 실제 호출할 위치에 호출코드 넣을게요
public sealed class GameplaySoundController : IDisposable
{
    private readonly GameplayPresentationController presentation;

    public GameplaySoundController(GameplayPresentationController presentation)
    {
        this.presentation = presentation;
        presentation.CueRaised += OnPresentationCue;
    }

    public void Dispose()
    {
        presentation.CueRaised -= OnPresentationCue;
    }

    private void OnPresentationCue(GameplayPresentationCue cue)
    {
        switch (cue)
        {
            case GameplayPresentationCue.CardDeal:
                SoundSystem.Instance.PlayCardSFX();
                break;

            case GameplayPresentationCue.ChipBet:
                SoundSystem.Instance.PlayChipStackSFX();
                break;
        }
    }
}
