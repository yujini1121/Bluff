using UnityEngine;

[DisallowMultipleComponent, RequireComponent(typeof(Animator))]
public sealed class SlotMachineNpc : MonoBehaviour
{
    [Header("Scene references")]
    [SerializeField] private SlotMachineController machine;
    [SerializeField] private Transform buttonContact;
    [SerializeField] private Transform reelLookTarget;
    [SerializeField] private Transform rightHandRest, leftHandRest;
    [SerializeField] private Transform rightFootPlant, leftFootPlant;
    [Header("Timing")]
    [SerializeField, Range(.2f, .7f)] private float pressContactTime = .48f;
    [SerializeField, Min(.05f)] private float blendTime = .18f;
    [SerializeField] private Vector2 restSeconds = new Vector2(3.5f, 6.5f);
    [SerializeField, Min(0)] private float readResultSeconds = .35f;
    [Header("Coin credit (presentation only)")]
    [SerializeField] private Transform coinContact;
    [SerializeField] private Transform coinProp;
    [SerializeField, Range(.2f, .75f)] private float coinContactTime = .5f;
    [Header("Visual winnings (scene instance props)")]
    [SerializeField] private Transform payoutOrigin;
    [SerializeField] private Transform payoutReceiver;
    [SerializeField] private Transform[] payoutCoins;

    private static readonly int Rest = Animator.StringToHash("Base Layer.Rest");
    private static readonly int Release = Animator.StringToHash("Base Layer.Release");
    private static readonly int Press = Animator.StringToHash("Base Layer.Press");
    private static readonly int Lean = Animator.StringToHash("Base Layer.Lean");
    private static readonly int Win = Animator.StringToHash("Base Layer.Win");
    private static readonly int Lose1 = Animator.StringToHash("Base Layer.Lose1");
    private static readonly int Lose2 = Animator.StringToHash("Base Layer.Lose2");
    private static readonly int CoinStart = Animator.StringToHash("Base Layer.CoinStart");
    private static readonly int CoinLoop = Animator.StringToHash("Base Layer.CoinLoop");
    private static readonly int CoinEnd = Animator.StringToHash("Base Layer.CoinEnd");
    private static readonly int Frustrate = Animator.StringToHash("Base Layer.Frustrate");
    private enum Phase { Rest, Release, CoinStart, CoinLoop, CoinEnd, CoinPause, Press, Lean, Waiting, React, Payout, CoinTransfer }
    private enum PayoutPhase { Hidden, Dispensing, Waiting, Collecting }
    private Animator animator;
    private IntroSlotNpcCoinTransfer coinTransfer;
    private Transform rightHand, indexTip;
    private Phase phase;
    private int state, generation;
    private float restRemaining, resultRemaining, restWeight, feetWeight = 1;
    private bool pressed, ownsSpin, reactingToWin;
    private bool coinInserted, longRest;
    private int visualCredits, coinsToInsert, consecutiveLosses, frustrateCooldown, lastLoss;
    private bool choseFunding, lastBulkFunding;
    private float coinPauseRemaining, coinHoldTime;
    private PayoutPhase payoutPhase;
    private float payoutElapsed;
    private int payoutCount;
    private Vector3[] payoutHomes, payoutScales;
    private SlotSpinResult result;
    private SlotMachineController subscribedMachine;

    public SlotMachineController Machine => machine;
    public bool IsWaitingForResult => ownsSpin;
    public int VisualCredits => visualCredits;

    private void Awake()
    {
        animator = GetComponent<Animator>();
        coinTransfer = GetComponent<IntroSlotNpcCoinTransfer>();
        if (!animator.avatar || !animator.avatar.isValid || !animator.isHuman) {
            Debug.LogError("Slot NPC requires the dealer's valid Humanoid avatar.", this);
            enabled = false;
            return;
        }
        rightHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
        var distal = animator.GetBoneTransform(HumanBodyBones.RightIndexDistal);
        indexTip = distal && distal.childCount > 0 ? distal.GetChild(0) : distal;
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        animator.updateMode = AnimatorUpdateMode.Normal;
        int count = payoutCoins == null ? 0 : payoutCoins.Length;
        payoutHomes = new Vector3[count]; payoutScales = new Vector3[count];
        for (int i = 0; i < count; i++) if (payoutCoins[i]) {
            payoutHomes[i] = payoutCoins[i].localPosition;
            payoutScales[i] = payoutCoins[i].localScale;
        }
    }

    private void OnEnable()
    {
        if (!animator || !animator.isHuman) return;
        if (!machine || !buttonContact || !reelLookTarget || !rightHandRest || !leftHandRest ||
            !rightFootPlant || !leftFootPlant || !indexTip ||
            !animator.runtimeAnimatorController || !animator.HasState(0, Rest) ||
            !animator.HasState(0, Release) || !animator.HasState(0, Press) ||
            !animator.HasState(0, Lean) || !animator.HasState(0, Win) ||
            !animator.HasState(0, Lose1) || !animator.HasState(0, Lose2) || !CanInsertCoin) {
            Debug.LogError("Slot NPC is missing its machine, contact targets, or animation states.", this);
            enabled = false;
            return;
        }
        subscribedMachine = machine;
        subscribedMachine.SpinCancelled += CancelSpin;
        generation++;
        ownsSpin = false;
        result = null;
        HideCoin();
        HidePayout();
        coinsToInsert = 0;
        longRest = false;
        restWeight = feetWeight = 1;
        state = Rest;
        phase = Phase.Rest;
        animator.Play(Rest, 0, 0);
        restRemaining = RandomRest();
    }

    private void OnDisable()
    {
        if (subscribedMachine) subscribedMachine.SpinCancelled -= CancelSpin;
        subscribedMachine = null;
        generation++;
        ownsSpin = false;
        result = null;
        HideCoin();
        HidePayout();
        coinsToInsert = 0;
    }

    private void CancelSpin()
    {
        generation++;
        ownsSpin = false;
        result = null;
        resultRemaining = 0;
        HideCoin();
        HidePayout();
        coinsToInsert = 0;
        if (InsertingCoin) Play(Lean, Phase.Lean);
    }

    private float RandomRest() => Random.Range(Mathf.Max(1, restSeconds.x), Mathf.Max(1, Mathf.Max(restSeconds.x, restSeconds.y)));
    private bool Ready => machine && machine.isActiveAndEnabled && machine.IsConfigured && !machine.IsSpinning;
    private bool InsertingCoin => phase == Phase.CoinStart || phase == Phase.CoinLoop || phase == Phase.CoinEnd || phase == Phase.CoinPause || phase == Phase.CoinTransfer;
    private bool CanInsertCoin => coinTransfer ? coinTransfer.IsConfigured : coinContact && coinProp && animator.HasState(0, CoinStart) &&
        animator.HasState(0, CoinLoop) && animator.HasState(0, CoinEnd);
    private void HideCoin()
    {
        if (coinProp) coinProp.gameObject.SetActive(false);
        if (coinTransfer) coinTransfer.CancelTransfer();
    }

    private void BeginFunding()
    {
        float bulkChance = !choseFunding ? .45f : lastBulkFunding ? .3f : .65f;
        lastBulkFunding = Random.value < bulkChance;
        choseFunding = true;
        coinsToInsert = coinTransfer ? 1 : lastBulkFunding ? Random.Range(2, 5) : 1;
        BeginCoin();
    }

    private void BeginCoin()
    {
        coinInserted = false;
        coinHoldTime = Random.Range(.12f, .26f);
        HideCoin();
        if (coinTransfer)
        {
            if (coinTransfer.BeginTransfer()) phase = Phase.CoinTransfer;
            else { coinsToInsert = 0; Play(Lean, Phase.Lean); }
            return;
        }
        Play(CoinStart, Phase.CoinStart);
    }

    private void HidePayout()
    {
        payoutPhase = PayoutPhase.Hidden;
        if (payoutCoins == null) return;
        for (int i = 0; i < payoutCoins.Length; i++) if (payoutCoins[i]) {
            payoutCoins[i].gameObject.SetActive(false);
            if (payoutHomes == null || i >= payoutHomes.Length) continue;
            payoutCoins[i].localPosition = payoutHomes[i];
            payoutCoins[i].localScale = payoutScales[i];
        }
    }

    private void ShowPayout()
    {
        HidePayout();
        if (!payoutOrigin || !payoutReceiver || payoutCoins == null || payoutCoins.Length == 0) return;
        payoutCount = Random.Range(Mathf.Min(3, payoutCoins.Length), payoutCoins.Length + 1);
        payoutElapsed = 0;
        payoutPhase = PayoutPhase.Dispensing;
    }

    private void AnimatePayout()
    {
        if (payoutPhase == PayoutPhase.Hidden || payoutPhase == PayoutPhase.Waiting) return;
        if (!payoutOrigin || !payoutReceiver) { HidePayout(); return; }
        payoutElapsed += Time.deltaTime;
        bool collecting = payoutPhase == PayoutPhase.Collecting;
        float duration = collecting ? .55f : .35f;
        for (int i = 0; i < payoutCount; i++) {
            var coin = payoutCoins[i];
            if (!coin) continue;
            float age = payoutElapsed - i * .08f - (collecting ? .2f : 0);
            if (age < 0) continue;
            float t = Mathf.Clamp01(age / duration);
            Vector3 home = coin.parent ? coin.parent.TransformPoint(payoutHomes[i]) : payoutHomes[i];
            coin.gameObject.SetActive(!collecting || t < 1);
            coin.position = Vector3.Lerp(collecting ? home : payoutOrigin.position,
                collecting ? payoutReceiver.position : home, Mathf.SmoothStep(0, 1, t)) +
                Vector3.up * (Mathf.Sin(t * Mathf.PI) * (collecting ? .07f : .025f));
            coin.localScale = payoutScales[i] * (collecting ? 1 - Mathf.SmoothStep(0, .85f, Mathf.InverseLerp(.75f, 1, t)) : 1);
        }
        if (payoutElapsed < duration + (payoutCount - 1) * .08f + (collecting ? .2f : 0)) return;
        if (collecting) HidePayout();
        else payoutPhase = PayoutPhase.Waiting;
    }

    private int ChooseReaction()
    {
        bool canFrustrate = frustrateCooldown == 0;
        if (frustrateCooldown > 0) frustrateCooldown--;
        if (reactingToWin) return Win;
        if (consecutiveLosses >= 3 && canFrustrate && animator.HasState(0, Frustrate) && Random.value < .25f) {
            frustrateCooldown = 5;
            longRest = true;
            return Frustrate;
        }
        lastLoss = lastLoss == Lose1 ? Lose2 : lastLoss == Lose2 ? Lose1 : Random.value < .55f ? Lose1 : Lose2;
        return lastLoss;
    }

    private void Update()
    {
        if (!animator || !animator.enabled || Time.deltaTime <= 0) return;
        if ((InsertingCoin || (phase == Phase.Press && !pressed)) && !Ready) {
            HideCoin();
            coinsToInsert = 0;
            Play(Lean, Phase.Lean);
            return;
        }
        float t = StateTime();
        float desiredRest = phase == Phase.Rest || phase == Phase.Waiting ? 1 :
            phase == Phase.Lean ? Mathf.SmoothStep(0, 1, t) :
            phase == Phase.Release ? 1 - Mathf.SmoothStep(0, 1, t) : 0;
        restWeight = Mathf.MoveTowards(restWeight, desiredRest, Time.deltaTime / blendTime);
        float desiredFeet = phase == Phase.React && reactingToWin ?
            1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.08f, .25f, t)) *
            (1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.8f, 1f, t))) : 1;
        feetWeight = Mathf.MoveTowards(feetWeight, desiredFeet, Time.deltaTime / blendTime);
        if (ownsSpin && (!machine || !machine.isActiveAndEnabled)) CancelSpin();

        switch (phase) {
            case Phase.Rest:
                if (Ready && (restRemaining -= Time.deltaTime) <= 0) Play(Release, Phase.Release);
                break;
            case Phase.Release:
                if (Finished()) {
                    pressed = false; result = null;
                    if (!Ready) Play(Lean, Phase.Lean);
                    else if (visualCredits == 0) BeginFunding();
                    else Play(Press, Phase.Press);
                }
                break;
            case Phase.CoinTransfer:
                if (coinTransfer && coinTransfer.isActiveAndEnabled && coinTransfer.HasArrived) {
                    HideCoin();
                    visualCredits++;
                    coinsToInsert = Mathf.Max(0, coinsToInsert - 1);
                    if (coinsToInsert > 0) {
                        coinPauseRemaining = Random.Range(.16f, .32f);
                        phase = Phase.CoinPause;
                    } else Play(Press, Phase.Press);
                } else if (!coinTransfer || !coinTransfer.isActiveAndEnabled || !coinTransfer.IsTravelling) {
                    HideCoin();
                    coinsToInsert = 0;
                    Play(Lean, Phase.Lean);
                }
                break;
            case Phase.CoinStart:
                if (t >= .43f && coinProp) coinProp.gameObject.SetActive(true);
                if (Finished()) Play(CoinLoop, Phase.CoinLoop);
                break;
            case Phase.CoinLoop:
                if (t >= coinHoldTime && !animator.IsInTransition(0)) Play(CoinEnd, Phase.CoinEnd);
                break;
            case Phase.CoinEnd:
                if (Finished()) {
                    HideCoin();
                    pressed = false;
                    if (!coinInserted) {
                        coinsToInsert = 0;
                        Play(Lean, Phase.Lean);
                    } else if (coinsToInsert > 0) {
                        coinPauseRemaining = Random.Range(.16f, .32f);
                        phase = Phase.CoinPause;
                    } else Play(Press, Phase.Press);
                }
                break;
            case Phase.CoinPause:
                if ((coinPauseRemaining -= Time.deltaTime) <= 0) BeginCoin();
                break;
            case Phase.Press:
                if (!pressed && t >= pressContactTime) {
                    pressed = true;
                    if (visualCredits <= 0) { Play(Lean, Phase.Lean); break; }
                    int ticket = ++generation;
                    ownsSpin = machine && machine.TrySpin(completed => {
                        if (!this || !isActiveAndEnabled || ticket != generation) return;
                        ownsSpin = false;
                        result = completed;
                        resultRemaining = readResultSeconds + Random.Range(0f, .65f);
                        if (Random.value < .18f) resultRemaining += Random.Range(1.2f, 2.1f);
                        consecutiveLosses = completed.IsWin ? 0 : consecutiveLosses + 1;
                    });
                    if (ownsSpin) visualCredits--;
                }
                if (Finished()) Play(Lean, Phase.Lean);
                break;
            case Phase.Lean:
                if (Finished()) {
                    Play(Rest, ownsSpin || result != null ? Phase.Waiting : Phase.Rest);
                    restRemaining = longRest || Random.value < .12f ? Random.Range(7f, 11f) :
                        visualCredits > 0 ? Random.Range(2.4f, 4.4f) : RandomRest();
                    longRest = false;
                }
                break;
            case Phase.Waiting:
                if (ownsSpin) break;
                if (result == null) { phase = Phase.Rest; restRemaining = RandomRest(); break; }
                if ((resultRemaining -= Time.deltaTime) > 0 || (machine && machine.IsSpinning)) break;
                reactingToWin = result.IsWin;
                if (reactingToWin) ShowPayout();
                Play(ChooseReaction(), Phase.React);
                result = null;
                break;
            case Phase.React:
                if (Finished()) {
                    if (reactingToWin && payoutPhase != PayoutPhase.Hidden) {
                        payoutPhase = PayoutPhase.Collecting;
                        payoutElapsed = 0;
                        phase = Phase.Payout;
                    } else Play(Lean, Phase.Lean);
                }
                break;
            case Phase.Payout:
                if (payoutPhase == PayoutPhase.Hidden) Play(Lean, Phase.Lean);
                break;
        }
    }

    private void LateUpdate()
    {
        AnimatePayout();
        if (phase != Phase.CoinEnd || coinInserted || !coinProp || !coinContact ||
            StateTime() < coinContactTime || !coinProp.gameObject.activeSelf) return;
        if (Vector3.Distance(coinProp.position, coinContact.position) > .025f) return;
        HideCoin();
        coinInserted = true;
        visualCredits++;
        coinsToInsert = Mathf.Max(0, coinsToInsert - 1);
    }

    private float StateTime()
    {
        var info = animator.IsInTransition(0) ? animator.GetNextAnimatorStateInfo(0) : animator.GetCurrentAnimatorStateInfo(0);
        return info.fullPathHash == state ? Mathf.Max(0, info.normalizedTime) : 0;
    }
    private bool Finished() => !animator.IsInTransition(0) && StateTime() >= 1;
    private void Play(int next, Phase nextPhase)
    {
        state = next; phase = nextPhase;
        animator.CrossFadeInFixedTime(next, Mathf.Max(.05f, blendTime), 0, 0);
    }

    private void OnAnimatorIK(int layerIndex)
    {
        if (layerIndex != 0 || !enabled || !rightFootPlant || !leftFootPlant) return;
        Plant(AvatarIKGoal.RightFoot, rightFootPlant);
        Plant(AvatarIKGoal.LeftFoot, leftFootPlant);
        if (phase == Phase.CoinTransfer && coinTransfer && coinTransfer.IsTravelling) {
            coinTransfer.ApplyHandAndLookIK();
            return;
        }
        if (reelLookTarget) {
            animator.SetLookAtWeight(.65f * restWeight, .05f, .85f, .6f, .6f);
            animator.SetLookAtPosition(reelLookTarget.position);
        }
        Hand(AvatarIKGoal.LeftHand, leftHandRest, restWeight);
        if (phase == Phase.CoinEnd && coinContact && coinProp) {
            float time = StateTime();
            float coinWeight = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.12f, coinContactTime - .03f, time)) *
                (1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(coinContactTime + .06f, .85f, time)));
            animator.SetIKPositionWeight(AvatarIKGoal.RightHand, coinWeight);
            Quaternion rotation = animator.GetIKRotation(AvatarIKGoal.RightHand);
            animator.SetIKRotationWeight(AvatarIKGoal.RightHand, coinWeight);
            animator.SetIKPosition(AvatarIKGoal.RightHand, coinContact.position + rightHand.position - coinProp.position);
            animator.SetIKRotation(AvatarIKGoal.RightHand, rotation);
            return;
        }
        if (phase != Phase.Press || !buttonContact || !indexTip) {
            Hand(AvatarIKGoal.RightHand, rightHandRest, restWeight);
            return;
        }
        float t = StateTime();
        float weight = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.2f, pressContactTime - .03f, t)) *
            (1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(pressContactTime + .05f, .78f, t)));
        animator.SetIKPositionWeight(AvatarIKGoal.RightHand, weight);
        Quaternion handRotation = animator.GetIKRotation(AvatarIKGoal.RightHand);
        animator.SetIKRotationWeight(AvatarIKGoal.RightHand, weight);
        animator.SetIKPosition(AvatarIKGoal.RightHand, buttonContact.position + rightHand.position - indexTip.position);
        animator.SetIKRotation(AvatarIKGoal.RightHand, handRotation);
    }

    private void Plant(AvatarIKGoal goal, Transform target)
    {
        animator.SetIKPositionWeight(goal, feetWeight);
        animator.SetIKRotationWeight(goal, feetWeight);
        animator.SetIKPosition(goal, target.position);
        animator.SetIKRotation(goal, target.rotation);
    }
    private void Hand(AvatarIKGoal goal, Transform target, float weight)
    {
        if (!target) return;
        animator.SetIKPositionWeight(goal, weight);
        animator.SetIKRotationWeight(goal, weight);
        Quaternion rotation = animator.GetIKRotation(goal);
        animator.SetIKPosition(goal, target.position);
        animator.SetIKRotation(goal, rotation);
    }
}
