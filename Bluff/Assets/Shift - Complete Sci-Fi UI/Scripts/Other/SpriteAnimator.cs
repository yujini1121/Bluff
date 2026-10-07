using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Michsky.UI.Shift
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Image))]
    public class SpriteAnimator : MonoBehaviour
    {
        [SerializeField] List<Sprite> sprites = new();
        [SerializeField, Range(1, 120)] float frameRate = 24;

        Image image;
        Coroutine animCoroutine;
        int currentFrame;
        float frameTimer;

        void Awake()
        {
            image = GetComponent<Image>();
            if (sprites.Count > 0 && sprites[0] != null)
                image.sprite = sprites[0];
        }

        void OnEnable() => Play();

        void OnDisable() => Stop();

        public void Play()
        {
            if (sprites.Count < 2)
                return;

            Stop();
            animCoroutine = StartCoroutine(Animate());
        }

        public void Stop()
        {
            if (animCoroutine == null)
                return;

            StopCoroutine(animCoroutine);
            animCoroutine = null;
        }

        IEnumerator Animate()
        {
            float frameDuration = 1f / Mathf.Max(frameRate, 0.01f);

            while (true)
            {
                frameTimer += Time.deltaTime;

                while (frameTimer >= frameDuration)
                {
                    frameTimer -= frameDuration;
                    currentFrame = (currentFrame + 1) % sprites.Count;
                }

                if (sprites[currentFrame] != null)
                    image.sprite = sprites[currentFrame];

                yield return null;
            }
        }
    }
}