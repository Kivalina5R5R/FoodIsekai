using UnityEngine;
using UnityEngine.UI;

namespace FoodIsekaiZ.Minigames.Views
{
    // The wolfgirl chef on one side of the Chef Says wall. Show plays her jump in from below, then loops
    // her searching pose until Hide, which plays her ducking back down. Popping up and ducking down
    // each throw a burst of big star sparkles that shrink away.
    public sealed class ChefSaysPopup : MonoBehaviour
    {
        [SerializeField] private Image portrait;
        [SerializeField] private Sprite[] enterFrames;
        // The last idle frame leads straight back into the first, so the pose loops without a jump.
        [SerializeField] private Sprite[] idleFrames;
        [SerializeField] private Sprite[] exitFrames;
        [SerializeField, Min(1f)] private float framesPerSecond = 12f;
        [SerializeField] private Sprite sparkleSprite;
        [SerializeField] private SparkleBurstStyle enterSparkles = new SparkleBurstStyle();
        [SerializeField] private SparkleBurstStyle exitSparkles = new SparkleBurstStyle();

        private enum Phase
        {
            Hidden,
            Waiting,
            Entering,
            Idle,
            Leaving,
        }

        // How long one full appearance takes: the jump in plus one pass of the searching loop, so a
        // call timed to this ends just as she has looked both ways, at the pose her duck-down starts from.
        public float AppearanceSeconds => (Count(enterFrames) + Count(idleFrames)) / framesPerSecond;

        private static int Count(Sprite[] frames)
        {
            return frames != null ? frames.Length : 0;
        }

        private Phase phase = Phase.Hidden;
        private float elapsed;
        private float waitSeconds;
        private SparkleBurst sparkles;

        // Pops her out after delay seconds; she stays hidden until then.
        public void Show(float delay = 0f)
        {
            if (!HasFrames())
            {
                HideImmediately();
                return;
            }

            if (delay > 0f)
            {
                HideImmediately();
                phase = Phase.Waiting;
                waitSeconds = delay;
                elapsed = 0f;
                return;
            }

            Enter();
        }

        // Ducks down from wherever she is; does nothing if she is already gone or going, and
        // simply cancels a pop-out that has not happened yet.
        public void Hide()
        {
            if (phase == Phase.Waiting)
            {
                HideImmediately();
                return;
            }

            if (phase == Phase.Hidden || phase == Phase.Leaving) return;
            Begin(Phase.Leaving, exitFrames[0]);
            Sparkles()?.Play(exitSparkles);
        }

        public void HideImmediately()
        {
            phase = Phase.Hidden;
            if (portrait != null) portrait.enabled = false;
            sparkles?.Clear();
        }

        private void Enter()
        {
            Begin(Phase.Entering, enterFrames[0]);
            Sparkles()?.Play(enterSparkles);
        }

        private void Begin(Phase next, Sprite first)
        {
            phase = next;
            elapsed = 0f;
            portrait.enabled = true;
            portrait.sprite = first;
        }

        private bool HasFrames()
        {
            return portrait != null && Filled(enterFrames) && Filled(idleFrames) && Filled(exitFrames);
        }

        private static bool Filled(Sprite[] frames)
        {
            return frames != null && frames.Length > 0;
        }

        // The sparkles are made on first use, on top of the chef.
        private SparkleBurst Sparkles()
        {
            if (sparkles == null && portrait != null) sparkles = new SparkleBurst(portrait.rectTransform, sparkleSprite);
            return sparkles;
        }

        private void Update()
        {
            float deltaTime = Time.deltaTime;
            sparkles?.Tick(deltaTime);
            if (phase == Phase.Hidden) return;

            elapsed += deltaTime;
            int frame = Mathf.FloorToInt(elapsed * framesPerSecond);
            switch (phase)
            {
                case Phase.Waiting:
                    if (elapsed >= waitSeconds) Enter();
                    return;
                case Phase.Entering:
                    if (frame < enterFrames.Length)
                    {
                        portrait.sprite = enterFrames[frame];
                        return;
                    }

                    // Carry the leftover time into the loop so the hand-over keeps the beat.
                    elapsed -= enterFrames.Length / framesPerSecond;
                    phase = Phase.Idle;
                    portrait.sprite = idleFrames[0];
                    return;
                case Phase.Idle:
                    portrait.sprite = idleFrames[frame % idleFrames.Length];
                    return;
                case Phase.Leaving:
                    if (frame < exitFrames.Length)
                    {
                        portrait.sprite = exitFrames[frame];
                        return;
                    }

                    phase = Phase.Hidden;
                    portrait.enabled = false;
                    return;
            }
        }

        private void OnDisable()
        {
            HideImmediately();
        }
    }
}
