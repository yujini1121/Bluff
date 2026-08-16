using DG.Tweening;
using TMPro;
using UnityEngine;

public enum IndianHoldemBettingFeedback
{
    Call,
    Raise,
    AllIn
}

public sealed class IndianHoldemUIEffects : MonoBehaviour
{
    private const float ActionBarDistance = 14f;

    private RectTransform dealerActionRect;
    private RectTransform playerActionRect;
    private RectTransform resultRect;
    private RectTransform dealerCardRect;
    private RectTransform communityCard1Rect;
    private RectTransform communityCard2Rect;
    private RectTransform playerCardRect;

    private TMP_Text dealerNameText;
    private TMP_Text playerNameText;
    private TMP_Text dealerBetText;
    private TMP_Text playerBetText;
    private TMP_Text potText;
    private TMP_Text dealerChipsText;
    private TMP_Text playerChipsText;

    private CanvasGroup dealerActionGroup;
    private CanvasGroup playerActionGroup;
    private CanvasGroup resultGroup;
    private CanvasGroup dealerCardGroup;
    private CanvasGroup communityCard1Group;
    private CanvasGroup communityCard2Group;
    private CanvasGroup playerCardGroup;
    private TMP_Text dealerPenaltyText;
    private TMP_Text playerPenaltyText;
    private CanvasGroup dealerPenaltyGroup;
    private CanvasGroup playerPenaltyGroup;

    private Vector2 dealerActionPosition;
    private Vector2 playerActionPosition;
    private Vector3 dealerActionScale;
    private Vector3 playerActionScale;
    private Vector3 resultScale;
    private Vector3 dealerCardScale;
    private Vector3 communityCard1Scale;
    private Vector3 communityCard2Scale;
    private Vector3 playerCardScale;
    private Vector2 dealerCardPosition;
    private Vector2 communityCard1Position;
    private Vector2 communityCard2Position;
    private Vector2 playerCardPosition;
    private Vector2 dealerPenaltyPosition;
    private Vector2 playerPenaltyPosition;
    private Vector3 dealerPenaltyScale;
    private Vector3 playerPenaltyScale;

    private Sequence dealSequence;
    private Sequence turnSequence;
    private Sequence phaseSequence;
    private Sequence resultSequence;
    private Sequence penaltySequence;
    private bool initialized;

    public void Initialize(
        GameObject dealerActionBar,
        GameObject playerActionBar,
        GameObject resultOverlay,
        TMP_Text dealerCardText,
        TMP_Text communityCard1Text,
        TMP_Text communityCard2Text,
        TMP_Text playerCardText,
        TMP_Text dealerName,
        TMP_Text playerName,
        TMP_Text dealerBet,
        TMP_Text playerBet,
        TMP_Text pot,
        TMP_Text dealerChips,
        TMP_Text playerChips)
    {
        if (initialized)
        {
            return;
        }

        dealerActionRect = dealerActionBar.GetComponent<RectTransform>();
        playerActionRect = playerActionBar.GetComponent<RectTransform>();
        resultRect = resultOverlay.GetComponent<RectTransform>();
        dealerCardRect = GetCardRect(dealerCardText);
        communityCard1Rect = GetCardRect(communityCard1Text);
        communityCard2Rect = GetCardRect(communityCard2Text);
        playerCardRect = GetCardRect(playerCardText);

        dealerNameText = dealerName;
        playerNameText = playerName;
        dealerBetText = dealerBet;
        playerBetText = playerBet;
        potText = pot;
        dealerChipsText = dealerChips;
        playerChipsText = playerChips;

        dealerActionGroup = GetOrAddCanvasGroup(dealerActionBar);
        playerActionGroup = GetOrAddCanvasGroup(playerActionBar);
        resultGroup = GetOrAddCanvasGroup(resultOverlay);
        dealerCardGroup = GetOrAddCanvasGroup(dealerCardRect.gameObject);
        communityCard1Group = GetOrAddCanvasGroup(communityCard1Rect.gameObject);
        communityCard2Group = GetOrAddCanvasGroup(communityCard2Rect.gameObject);
        playerCardGroup = GetOrAddCanvasGroup(playerCardRect.gameObject);

        dealerActionPosition = dealerActionRect.anchoredPosition;
        playerActionPosition = playerActionRect.anchoredPosition;
        dealerActionScale = dealerActionRect.localScale;
        playerActionScale = playerActionRect.localScale;
        resultScale = resultRect.localScale;
        dealerCardScale = dealerCardRect.localScale;
        communityCard1Scale = communityCard1Rect.localScale;
        communityCard2Scale = communityCard2Rect.localScale;
        playerCardScale = playerCardRect.localScale;
        dealerCardPosition = dealerCardRect.anchoredPosition;
        communityCard1Position = communityCard1Rect.anchoredPosition;
        communityCard2Position = communityCard2Rect.anchoredPosition;
        playerCardPosition = playerCardRect.anchoredPosition;

        CreatePenaltyTexts();
        initialized = true;
        RestoreAll();
    }

    public void PrepareForNewRound()
    {
        if (!initialized)
        {
            return;
        }

        RestoreAll();
        SetCardDealStart(dealerCardRect, dealerCardGroup, dealerCardScale);
        SetCardDealStart(
            communityCard1Rect,
            communityCard1Group,
            communityCard1Scale);
        SetCardDealStart(
            communityCard2Rect,
            communityCard2Group,
            communityCard2Scale);
        SetCardDealStart(playerCardRect, playerCardGroup, playerCardScale);
    }

    public void PlayNewRound(TurnOwner firstTurn)
    {
        KillSequence(ref dealSequence);

        dealSequence = DOTween.Sequence().SetTarget(this);
        InsertCardDeal(dealSequence, 0f, dealerCardRect,
            dealerCardGroup, dealerCardScale);
        InsertCardDeal(dealSequence, 0.08f, communityCard1Rect,
            communityCard1Group, communityCard1Scale);
        InsertCardDeal(dealSequence, 0.16f, communityCard2Rect,
            communityCard2Group, communityCard2Scale);
        InsertCardDeal(dealSequence, 0.24f, playerCardRect,
            playerCardGroup, playerCardScale);
        dealSequence.InsertCallback(0.43f, () => PlayTurnChange(firstTurn));
    }

    public void PlayTurnChange(TurnOwner turn)
    {
        if (!initialized)
        {
            return;
        }

        KillSequence(ref turnSequence);
        KillActionBarTweens();

        turnSequence = DOTween.Sequence().SetTarget(this);

        if (turn == TurnOwner.Player)
        {
            FadeOutActionBar(
                turnSequence,
                dealerActionRect,
                dealerActionGroup,
                dealerActionPosition,
                dealerActionScale);
            InsertActionBarEntrance(
                turnSequence,
                playerActionRect,
                playerActionGroup,
                playerActionPosition,
                playerActionScale,
                -ActionBarDistance);
            PunchTurnTarget(playerNameText.rectTransform, 0.055f, 0.18f);
        }
        else if (turn == TurnOwner.Dealer)
        {
            FadeOutActionBar(
                turnSequence,
                playerActionRect,
                playerActionGroup,
                playerActionPosition,
                playerActionScale);
            InsertActionBarEntrance(
                turnSequence,
                dealerActionRect,
                dealerActionGroup,
                dealerActionPosition,
                dealerActionScale,
                ActionBarDistance);
            PunchTurnTarget(dealerNameText.rectTransform, 0.055f, 0.18f);
        }
        else
        {
            HideActionBars();
        }
    }

    public void PlayBettingFeedback(
        IndianHoldemBettingFeedback feedback,
        TurnOwner actor)
    {
        RectTransform betRect = actor == TurnOwner.Player
            ? playerBetText.rectTransform
            : dealerBetText.rectTransform;
        RectTransform actorCard = actor == TurnOwner.Player
            ? playerCardRect
            : dealerCardRect;
        RectTransform actorName = actor == TurnOwner.Player
            ? playerNameText.rectTransform
            : dealerNameText.rectTransform;

        switch (feedback)
        {
            case IndianHoldemBettingFeedback.Call:
                Punch(betRect, 0.055f, 0.13f, 5);
                Punch(potText.rectTransform, 0.035f, 0.13f, 4);
                break;
            case IndianHoldemBettingFeedback.Raise:
                Punch(betRect, 0.09f, 0.17f, 6);
                Punch(potText.rectTransform, 0.06f, 0.17f, 5);
                Punch(actorName, 0.035f, 0.14f, 4);
                break;
            case IndianHoldemBettingFeedback.AllIn:
                Punch(betRect, 0.14f, 0.22f, 7);
                Punch(potText.rectTransform, 0.11f, 0.22f, 6);
                Punch(actorCard, 0.09f, 0.22f, 6);
                Punch(actorName, 0.075f, 0.2f, 6);
                break;
        }
    }

    public void PlayShowdownIntro(float extraDelay)
    {
        KillSequence(ref phaseSequence);
        HideActionBars();

        phaseSequence = DOTween.Sequence()
            .SetDelay(extraDelay)
            .AppendInterval(0.28f)
            .AppendCallback(() =>
            {
                Punch(dealerCardRect, 0.045f, 0.17f, 4);
                Punch(communityCard1Rect, 0.045f, 0.17f, 4);
                Punch(communityCard2Rect, 0.045f, 0.17f, 4);
                Punch(playerCardRect, 0.045f, 0.17f, 4);
            })
            .SetTarget(this);
    }

    public void PlayChipsFeedback(TurnOwner owner)
    {
        RectTransform chipsRect = owner == TurnOwner.Player
            ? playerChipsText.rectTransform
            : dealerChipsText.rectTransform;
        Punch(chipsRect, 0.045f, 0.15f, 4);
    }

    public void PlayFold(
        TurnOwner foldedBy,
        int penaltyAmount,
        TurnOwner winner,
        bool potWasPaid,
        bool isGameOver)
    {
        KillSequence(ref phaseSequence);
        HideActionBars();

        RectTransform foldedCard = foldedBy == TurnOwner.Player
            ? playerCardRect
            : dealerCardRect;
        CanvasGroup foldedCardGroup = foldedBy == TurnOwner.Player
            ? playerCardGroup
            : dealerCardGroup;
        Vector2 foldedCardPosition = foldedBy == TurnOwner.Player
            ? playerCardPosition
            : dealerCardPosition;
        Vector2 foldOffset = foldedBy == TurnOwner.Player
            ? new Vector2(18f, -16f)
            : new Vector2(18f, 16f);

        foldedCard.DOKill();
        foldedCardGroup.DOKill();
        foldedCard.anchoredPosition = foldedCardPosition;
        foldedCardGroup.alpha = 1f;

        phaseSequence = DOTween.Sequence()
            .Join(foldedCard.DOAnchorPos(
                    foldedCardPosition + foldOffset,
                    0.28f)
                .SetEase(Ease.InCubic))
            .Join(foldedCardGroup.DOFade(0f, 0.26f)
                .SetEase(Ease.InCubic))
            .SetTarget(this);

        if (penaltyAmount > 0)
        {
            phaseSequence.InsertCallback(
                0.17f,
                () => PlayFoldPenalty(foldedBy, penaltyAmount));
            phaseSequence.InsertCallback(
                0.21f,
                () => Punch(
                    foldedBy == TurnOwner.Player
                        ? playerChipsText.rectTransform
                        : dealerChipsText.rectTransform,
                    0.08f,
                    0.19f,
                    5));
        }

        phaseSequence.InsertCallback(
            0.34f,
            () => PlayPotSettlement(winner, potWasPaid));
        phaseSequence.InsertCallback(
            isGameOver ? 0.48f : 0.36f,
            () => PlayResult(isGameOver));
    }

    public void PlayShowdownResult(
        RoundWinner winner,
        bool potWasPaid,
        bool isGameOver)
    {
        if (winner == RoundWinner.Player)
        {
            PlayPotSettlement(TurnOwner.Player, potWasPaid);
        }
        else if (winner == RoundWinner.Dealer)
        {
            PlayPotSettlement(TurnOwner.Dealer, potWasPaid);
        }

        PlayResult(isGameOver, isGameOver ? 0.28f : 0.08f);
    }

    public void PlayResult(bool isGameOver, float delay = 0f)
    {
        KillSequence(ref resultSequence);
        resultRect.DOKill();
        resultGroup.DOKill();

        resultRect.gameObject.SetActive(true);
        resultRect.localScale = resultScale * (isGameOver ? 0.88f : 0.92f);
        resultGroup.alpha = 0f;

        resultSequence = DOTween.Sequence()
            .SetDelay(delay)
            .SetTarget(this);

        if (isGameOver)
        {
            resultSequence
                .AppendCallback(() => DimCards(0.68f, 0.22f))
                .AppendInterval(0.22f)
                .AppendInterval(0.12f)
                .Append(resultGroup.DOFade(1f, 0.32f)
                    .SetEase(Ease.OutQuad))
                .Join(resultRect.DOScale(resultScale, 0.34f)
                    .SetEase(Ease.OutCubic));
        }
        else
        {
            resultSequence
                .Append(resultGroup.DOFade(1f, 0.24f)
                    .SetEase(Ease.OutQuad))
                .Join(resultRect.DOScale(resultScale, 0.27f)
                    .SetEase(Ease.OutBack, 1.25f));
        }
    }

    public void HideActionBars()
    {
        KillSequence(ref turnSequence);
        KillActionBarTweens();

        turnSequence = DOTween.Sequence().SetTarget(this);
        FadeOutActionBar(
            turnSequence,
            dealerActionRect,
            dealerActionGroup,
            dealerActionPosition,
            dealerActionScale);
        FadeOutActionBar(
            turnSequence,
            playerActionRect,
            playerActionGroup,
            playerActionPosition,
            playerActionScale);
    }

    public void StopAndRestore()
    {
        if (initialized)
        {
            RestoreAll();
        }
    }

    private void PlayPotSettlement(TurnOwner winner, bool potWasPaid)
    {
        if (!potWasPaid)
        {
            return;
        }

        RectTransform winnerChips = winner == TurnOwner.Player
            ? playerChipsText.rectTransform
            : dealerChipsText.rectTransform;

        Punch(potText.rectTransform, 0.09f, 0.18f, 5);
        DOVirtual.DelayedCall(
                0.13f,
                () => Punch(winnerChips, 0.085f, 0.19f, 5))
            .SetTarget(this);
    }

    private void PlayFoldPenalty(TurnOwner foldedBy, int penaltyAmount)
    {
        KillSequence(ref penaltySequence);

        TMP_Text penaltyText = foldedBy == TurnOwner.Player
            ? playerPenaltyText
            : dealerPenaltyText;
        CanvasGroup penaltyGroup = foldedBy == TurnOwner.Player
            ? playerPenaltyGroup
            : dealerPenaltyGroup;
        Vector2 startPosition = foldedBy == TurnOwner.Player
            ? playerPenaltyPosition
            : dealerPenaltyPosition;
        Vector3 startScale = foldedBy == TurnOwner.Player
            ? playerPenaltyScale
            : dealerPenaltyScale;
        RectTransform penaltyRect = penaltyText.rectTransform;

        penaltyRect.DOKill();
        penaltyGroup.DOKill();
        penaltyText.text = "PENALTY -" + penaltyAmount;
        penaltyText.gameObject.SetActive(true);
        penaltyRect.anchoredPosition = startPosition;
        penaltyRect.localScale = startScale;
        penaltyGroup.alpha = 1f;

        penaltySequence = DOTween.Sequence()
            .Join(penaltyRect.DOPunchScale(
                Vector3.one * 0.12f,
                0.22f,
                6,
                0.55f))
            .Join(penaltyRect.DOAnchorPosY(
                    startPosition.y + 28f,
                    0.42f)
                .SetEase(Ease.OutCubic))
            .Insert(0.12f, penaltyGroup.DOFade(0f, 0.3f)
                .SetEase(Ease.InCubic))
            .OnComplete(() =>
            {
                penaltyRect.anchoredPosition = startPosition;
                penaltyRect.localScale = startScale;
                penaltyGroup.alpha = 0f;
                penaltyText.gameObject.SetActive(false);
            })
            .SetTarget(this);
    }

    private void CreatePenaltyTexts()
    {
        dealerPenaltyText = Instantiate(
            dealerNameText,
            dealerNameText.transform.parent);
        playerPenaltyText = Instantiate(
            playerNameText,
            playerNameText.transform.parent);

        ConfigurePenaltyText(dealerPenaltyText, "DealerPenaltyFeedback");
        ConfigurePenaltyText(playerPenaltyText, "PlayerPenaltyFeedback");

        dealerPenaltyGroup = GetOrAddCanvasGroup(
            dealerPenaltyText.gameObject);
        playerPenaltyGroup = GetOrAddCanvasGroup(
            playerPenaltyText.gameObject);
        dealerPenaltyPosition = dealerPenaltyText.rectTransform.anchoredPosition;
        playerPenaltyPosition = playerPenaltyText.rectTransform.anchoredPosition;
        dealerPenaltyScale = dealerPenaltyText.rectTransform.localScale;
        playerPenaltyScale = playerPenaltyText.rectTransform.localScale;
    }

    private static void ConfigurePenaltyText(TMP_Text text, string objectName)
    {
        text.name = objectName;
        text.text = string.Empty;
        text.raycastTarget = false;
        text.color = new Color32(235, 190, 88, 255);
        text.gameObject.SetActive(false);
        text.transform.SetAsLastSibling();
    }

    private void RestoreAll()
    {
        KillAllTweens();

        ResetActionBar(
            dealerActionRect,
            dealerActionGroup,
            dealerActionPosition,
            dealerActionScale);
        ResetActionBar(
            playerActionRect,
            playerActionGroup,
            playerActionPosition,
            playerActionScale);

        ResetCard(
            dealerCardRect,
            dealerCardGroup,
            dealerCardPosition,
            dealerCardScale);
        ResetCard(
            communityCard1Rect,
            communityCard1Group,
            communityCard1Position,
            communityCard1Scale);
        ResetCard(
            communityCard2Rect,
            communityCard2Group,
            communityCard2Position,
            communityCard2Scale);
        ResetCard(
            playerCardRect,
            playerCardGroup,
            playerCardPosition,
            playerCardScale);

        resultRect.localScale = resultScale;
        resultGroup.alpha = 0f;
        resultRect.gameObject.SetActive(false);
        ResetPenalty(
            dealerPenaltyText,
            dealerPenaltyGroup,
            dealerPenaltyPosition,
            dealerPenaltyScale);
        ResetPenalty(
            playerPenaltyText,
            playerPenaltyGroup,
            playerPenaltyPosition,
            playerPenaltyScale);
    }

    private void KillAllTweens()
    {
        KillSequence(ref dealSequence);
        KillSequence(ref turnSequence);
        KillSequence(ref phaseSequence);
        KillSequence(ref resultSequence);
        KillSequence(ref penaltySequence);
        DOTween.Kill(this);

        dealerActionRect.DOKill();
        playerActionRect.DOKill();
        resultRect.DOKill();
        dealerCardRect.DOKill();
        communityCard1Rect.DOKill();
        communityCard2Rect.DOKill();
        playerCardRect.DOKill();
        dealerNameText.rectTransform.DOKill();
        playerNameText.rectTransform.DOKill();
        dealerBetText.rectTransform.DOKill();
        playerBetText.rectTransform.DOKill();
        potText.rectTransform.DOKill();
        dealerChipsText.rectTransform.DOKill();
        playerChipsText.rectTransform.DOKill();
    }

    private void KillActionBarTweens()
    {
        dealerActionRect.DOKill();
        playerActionRect.DOKill();
        dealerActionGroup.DOKill();
        playerActionGroup.DOKill();
        dealerActionRect.localScale = dealerActionScale;
        playerActionRect.localScale = playerActionScale;
    }

    private void DimCards(float alpha, float duration)
    {
        FadeVisibleCard(dealerCardGroup, alpha, duration);
        FadeVisibleCard(communityCard1Group, alpha, duration);
        FadeVisibleCard(communityCard2Group, alpha, duration);
        FadeVisibleCard(playerCardGroup, alpha, duration);
    }

    private static void InsertCardDeal(
        Sequence sequence,
        float startTime,
        RectTransform cardRect,
        CanvasGroup cardGroup,
        Vector3 originalScale)
    {
        sequence.Insert(
            startTime,
            cardGroup.DOFade(1f, 0.15f).SetEase(Ease.OutQuad));
        sequence.Insert(
            startTime,
            cardRect.DOScale(originalScale, 0.18f)
                .SetEase(Ease.OutCubic));
    }

    private static void InsertActionBarEntrance(
        Sequence sequence,
        RectTransform actionRect,
        CanvasGroup actionGroup,
        Vector2 originalPosition,
        Vector3 originalScale,
        float startOffsetY)
    {
        actionRect.gameObject.SetActive(true);
        actionRect.anchoredPosition =
            originalPosition + new Vector2(0f, startOffsetY);
        actionRect.localScale = originalScale;
        actionGroup.alpha = 0f;
        actionGroup.interactable = true;
        actionGroup.blocksRaycasts = true;

        sequence.Insert(
            0f,
            actionRect.DOAnchorPos(originalPosition, 0.22f)
                .SetEase(Ease.OutCubic));
        sequence.Insert(
            0f,
            actionGroup.DOFade(1f, 0.2f)
                .SetEase(Ease.OutQuad));
    }

    private static void FadeOutActionBar(
        Sequence sequence,
        RectTransform actionRect,
        CanvasGroup actionGroup,
        Vector2 originalPosition,
        Vector3 originalScale)
    {
        if (!actionRect.gameObject.activeSelf)
        {
            actionRect.anchoredPosition = originalPosition;
            actionRect.localScale = originalScale;
            actionGroup.alpha = 0f;
            return;
        }

        actionGroup.interactable = false;
        actionGroup.blocksRaycasts = false;
        sequence.Insert(
            0f,
            actionGroup.DOFade(0f, 0.16f)
                .SetEase(Ease.InCubic)
                .OnComplete(() =>
                {
                    actionRect.anchoredPosition = originalPosition;
                    actionRect.localScale = originalScale;
                    actionRect.gameObject.SetActive(false);
                }));
    }

    private static void ResetActionBar(
        RectTransform actionRect,
        CanvasGroup actionGroup,
        Vector2 originalPosition,
        Vector3 originalScale)
    {
        actionRect.anchoredPosition = originalPosition;
        actionRect.localScale = originalScale;
        actionGroup.alpha = 0f;
        actionGroup.interactable = false;
        actionGroup.blocksRaycasts = false;
        actionRect.gameObject.SetActive(false);
    }

    private static void ResetCard(
        RectTransform cardRect,
        CanvasGroup cardGroup,
        Vector2 originalPosition,
        Vector3 originalScale)
    {
        cardRect.anchoredPosition = originalPosition;
        cardRect.localScale = originalScale;
        cardGroup.alpha = 1f;
    }

    private static void ResetPenalty(
        TMP_Text penaltyText,
        CanvasGroup penaltyGroup,
        Vector2 originalPosition,
        Vector3 originalScale)
    {
        penaltyText.rectTransform.anchoredPosition = originalPosition;
        penaltyText.rectTransform.localScale = originalScale;
        penaltyGroup.alpha = 0f;
        penaltyText.text = string.Empty;
        penaltyText.gameObject.SetActive(false);
    }

    private static void SetCardDealStart(
        RectTransform cardRect,
        CanvasGroup cardGroup,
        Vector3 originalScale)
    {
        cardRect.localScale = originalScale * 0.92f;
        cardGroup.alpha = 0f;
    }

    private static void PunchTurnTarget(
        RectTransform target,
        float strength,
        float duration)
    {
        Punch(target, strength, duration, 5);
    }

    private static void Punch(
        RectTransform target,
        float strength,
        float duration,
        int vibrato)
    {
        target.DOKill(true);
        target.DOPunchScale(
            Vector3.one * strength,
            duration,
            vibrato,
            0.55f);
    }

    private static void FadeVisibleCard(
        CanvasGroup cardGroup,
        float alpha,
        float duration)
    {
        if (cardGroup.alpha <= 0.01f)
        {
            return;
        }

        cardGroup.DOKill();
        cardGroup.DOFade(alpha, duration).SetEase(Ease.OutQuad);
    }

    private static RectTransform GetCardRect(TMP_Text cardText)
    {
        RectTransform parentRect = cardText.transform.parent as RectTransform;
        return parentRect != null ? parentRect : cardText.rectTransform;
    }

    private static CanvasGroup GetOrAddCanvasGroup(GameObject target)
    {
        CanvasGroup canvasGroup = target.GetComponent<CanvasGroup>();
        return canvasGroup != null
            ? canvasGroup
            : target.AddComponent<CanvasGroup>();
    }

    private static void KillSequence(ref Sequence sequence)
    {
        if (sequence == null)
        {
            return;
        }

        sequence.Kill();
        sequence = null;
    }

    private void OnDisable()
    {
        if (initialized)
        {
            KillAllTweens();
        }
    }

    private void OnDestroy()
    {
        if (initialized)
        {
            KillAllTweens();
        }
    }
}
