using UnityEngine;

// 인트로 씬 안에서만 재생하며 공용 믹서의 사용자 음량은 변경하지 않는다.
public sealed class IntroSoundscape : MonoBehaviour
{
    [SerializeField] private IntroFlowController flow;
    [SerializeField] private IntroFadeController fade;
    [SerializeField] private AudioSource music;
    [SerializeField] private AudioSource interactionSound;
    [SerializeField, Range(0f, 1f)] private float corridorVolume = 0.065f;
    [SerializeField, Range(0f, 1f)] private float barVolume = 0.18f;
    [SerializeField, Range(0f, 1f)] private float dialogueVolume = 0.1f;
    private float gain;
    private IntroFlowController.IntroState previousState;

    private void OnEnable()
    {
        gain = 0f;
        previousState = flow != null ? flow.State : IntroFlowController.IntroState.Corridor;
        if (music == null) return;
        music.volume = 0f;
        music.loop = true;
        music.Play();
    }

    private void Update()
    {
        if (flow == null || music == null) return;
        var state = flow.State;
        if (state == IntroFlowController.IntroState.Dialogue && state != previousState && interactionSound != null)
            interactionSound.Play();
        previousState = state;
        float target = state == IntroFlowController.IntroState.Corridor ? corridorVolume :
            state == IntroFlowController.IntroState.Dialogue ? dialogueVolume : barVolume;
        // 암전이 시작되면 현재 음량을 유지한 채 화면과 같은 비율로 소리를 줄인다.
        if (state != IntroFlowController.IntroState.Transition)
            gain = Mathf.Lerp(gain, target, 1f - Mathf.Exp(-Time.unscaledDeltaTime / 0.55f));
        music.volume = gain * (fade != null ? 1f - fade.Opacity : 1f);
    }

    private void OnDisable()
    {
        if (music != null) { music.Stop(); music.volume = 0f; }
        if (interactionSound != null) interactionSound.Stop();
    }
}
