using UnityEngine;

public sealed class IntroHoldToSkip : MonoBehaviour
{
    [SerializeField] private IntroFlowController flow;
    [SerializeField] private GameObject keycap;
    [SerializeField] private IntroKeycapOutline outline;
    [SerializeField, Min(0.1f)] private float holdSeconds = 1.25f;
    [SerializeField, Min(0.01f)] private float releaseSeconds = 0.18f;
    private bool available;
    private float heldSeconds;
    private float progress;
    public float Progress => progress;
    public bool IsAvailable => available;

    public void SetAvailable(bool value)
    {
        available = value;
        heldSeconds = progress = 0f;
        if (outline != null) outline.Progress = 0f;
        if (keycap != null) keycap.SetActive(false);
    }

    private void Update() => Tick(Input.GetKey(KeyCode.Escape), Time.unscaledDeltaTime);

    public void Tick(bool held, float deltaTime)
    {
        if (!available) return;
        if (flow == null || flow.State == IntroFlowController.IntroState.Transition)
        {
            SetAvailable(false);
            return;
        }
        if (held)
        {
            heldSeconds += Mathf.Max(0f, deltaTime);
            progress = Mathf.Clamp01(heldSeconds / Mathf.Max(0.1f, holdSeconds));
        }
        else
        {
            heldSeconds = 0f;
            progress = Mathf.MoveTowards(progress, 0f, Mathf.Max(0f, deltaTime) / Mathf.Max(0.01f, releaseSeconds));
        }
        if (outline != null) outline.Progress = progress;
        if (keycap != null) keycap.SetActive(held || progress > 0f);
        if (progress >= 1f && held)
        {
            SetAvailable(false);
            flow.SkipIntro();
        }
    }

    private void OnApplicationFocus(bool focus)
    {
        if (!focus) SetAvailable(available);
    }
    private void OnDisable() => SetAvailable(false);
}
