using TMPro;
using UnityEngine;

namespace FoodIsekaiZ.Minigames
{
    // Animates one count-in number on the wall like Bounty Hunter's countdown: it falls in from big,
    // shrinking faster and fading in, squashes as it lands and springs back, holds, then fades.
    // Runs on unscaled time, like the minigame runner.
    public sealed class MinigameCountIn : MonoBehaviour
    {
        private const float FallPart = 0.25f;
        private const float DropFromScale = 3f;
        private const float LandSquash = 0.15f;
        private const float BounceEnd = 0.5f;
        private const float FadeFrom = 0.8f;

        private TMP_Text number;
        private float stepSeconds = 1f;
        private float age;

        // Starts the drop for this number; stepSeconds is how long it stays on the wall.
        public void Play(TMP_Text label, float seconds)
        {
            number = label;
            stepSeconds = Mathf.Max(0.05f, seconds);
            age = 0f;
            Apply();
        }

        private void Update()
        {
            if (number == null) return;
            age += Time.unscaledDeltaTime;
            Apply();
        }

        private void Apply()
        {
            Pose(Mathf.Clamp01(age / stepSeconds), out Vector3 scale, out float alpha);
            number.rectTransform.localScale = scale;
            number.alpha = alpha;
        }

        private static void Pose(float t, out Vector3 scale, out float alpha)
        {
            scale = Vector3.one;
            alpha = 1f;
            if (t < FallPart)
            {
                float u = t / FallPart;
                scale *= Mathf.Lerp(DropFromScale, 1f, u * u);
                alpha = Mathf.Clamp01(u * 3f);
            }
            else if (t < BounceEnd)
            {
                float u = (t - FallPart) / (BounceEnd - FallPart);
                float spring = Mathf.Cos(u * Mathf.PI * 2.5f) * (1f - u) * (1f - u) * LandSquash;
                scale = new Vector3(1f + spring, 1f - spring, 1f);
            }
            else if (t > FadeFrom)
            {
                alpha = 1f - (t - FadeFrom) / (1f - FadeFrom);
            }
        }
    }
}
