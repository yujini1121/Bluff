using UnityEngine;

[DisallowMultipleComponent]
public sealed class IntroSlotCoinPresentation : MonoBehaviour
{
    [SerializeField] private Transform paymentCoin;
    [SerializeField] private Transform coinSlot;
    [SerializeField] private Transform payoutOrigin;
    [SerializeField] private Transform[] payoutCoins;
    [SerializeField, Min(.2f)] private float insertDuration = .65f;
    [SerializeField, Min(.1f)] private float dispenseDuration = .38f;
    [SerializeField, Min(.1f)] private float collectDuration = .55f;
    [SerializeField, Min(0)] private float coinInterval = .09f;
    private enum Phase { Idle, Inserting, Dispensing, Collecting }
    private Phase phase;
    private Camera view;
    private Vector3 paymentStart;
    private Vector3[] homes, scales;
    private Quaternion[] rotations;
    private float elapsed;
    private int count;
    public bool IsBusy => phase != Phase.Idle;
    public bool IsDispensing => phase == Phase.Dispensing;
    public bool PaymentReady { get; private set; }
    public Vector3 FocusPosition => phase == Phase.Inserting && coinSlot ? coinSlot.position :
        payoutOrigin ? payoutOrigin.position : transform.position;

    private void Awake()
    {
        int length = payoutCoins == null ? 0 : payoutCoins.Length;
        homes = new Vector3[length];
        scales = new Vector3[length];
        rotations = new Quaternion[length];
        for (int i = 0; i < length; i++)
        {
            if (!payoutCoins[i]) continue;
            homes[i] = payoutCoins[i].localPosition;
            scales[i] = payoutCoins[i].localScale;
            rotations[i] = payoutCoins[i].localRotation;
        }
        Cancel();
    }

    public void BeginPayment(Camera camera)
    {
        Cancel();
        view = camera;
        if (!paymentCoin || !coinSlot || !view || !isActiveAndEnabled)
        {
            PaymentReady = true;
            return;
        }
        paymentStart = PocketPosition();
        paymentCoin.position = paymentStart;
        paymentCoin.gameObject.SetActive(true);
        phase = Phase.Inserting;
    }

    public void BeginPayout(Camera camera, int amount)
    {
        Cancel();
        view = camera;
        if (!view || !payoutOrigin || payoutCoins == null || amount <= 0 || !isActiveAndEnabled) return;
        count = Mathf.Min(amount, payoutCoins.Length);
        if (count > 0) phase = Phase.Dispensing;
    }

    private Vector3 PocketPosition() => view.transform.TransformPoint(new Vector3(.23f, -.38f, .65f));

    private void Update()
    {
        if (!IsBusy) return;
        if (!view || (phase != Phase.Inserting && !payoutOrigin)) { Cancel(); return; }
        elapsed += Time.deltaTime;
        if (phase == Phase.Inserting)
        {
            if (!paymentCoin || !coinSlot) { Cancel(); PaymentReady = true; return; }
            float t = Mathf.Clamp01((elapsed - .18f) / Mathf.Max(.2f, insertDuration));
            float eased = Mathf.SmoothStep(0, 1, t);
            paymentCoin.position = Vector3.Lerp(paymentStart, coinSlot.position, eased) +
                Vector3.up * (.12f * Mathf.Sin(t * Mathf.PI));
            paymentCoin.rotation = coinSlot.rotation * Quaternion.Euler(0, 0, Mathf.Lerp(55, 90, eased));
            if (t < 1) return;
            paymentCoin.gameObject.SetActive(false);
            phase = Phase.Idle;
            PaymentReady = true;
            return;
        }
        bool collecting = phase == Phase.Collecting;
        float duration = Mathf.Max(.1f, collecting ? collectDuration : dispenseDuration);
        for (int i = 0; i < count; i++)
        {
            var coin = payoutCoins[i];
            if (!coin) continue;
            float age = elapsed - .2f - i * Mathf.Max(0, coinInterval);
            if (age < 0) continue;
            float t = Mathf.Clamp01(age / duration);
            float eased = Mathf.SmoothStep(0, 1, t);
            Vector3 home = coin.parent ? coin.parent.TransformPoint(homes[i]) : homes[i];
            coin.gameObject.SetActive(t < 1 || !collecting);
            coin.position = Vector3.Lerp(collecting ? home : payoutOrigin.position,
                collecting ? PocketPosition() : home, eased) +
                Vector3.up * (Mathf.Sin(t * Mathf.PI) * (collecting ? .16f : .06f));
            coin.localRotation = rotations[i] * Quaternion.Euler(collecting ? 140 * eased : 180 * (1 - eased), 0, 0);
            coin.localScale = scales[i] * (collecting ? Mathf.Lerp(1, .25f, eased) : 1);
        }
        if (elapsed < .2f + duration + (count - 1) * Mathf.Max(0, coinInterval) + (collecting ? 0 : .4f)) return;
        if (collecting) Cancel();
        else { phase = Phase.Collecting; elapsed = 0; }
    }

    public void Cancel()
    {
        phase = Phase.Idle;
        elapsed = 0;
        PaymentReady = false;
        if (paymentCoin) paymentCoin.gameObject.SetActive(false);
        if (payoutCoins == null) return;
        for (int i = 0; i < payoutCoins.Length; i++)
        {
            if (!payoutCoins[i]) continue;
            payoutCoins[i].gameObject.SetActive(false);
            if (homes == null || i >= homes.Length) continue;
            payoutCoins[i].localPosition = homes[i];
            payoutCoins[i].localScale = scales[i];
            payoutCoins[i].localRotation = rotations[i];
        }
    }

    private void OnDisable() => Cancel();
}
