using UnityEngine;
using UnityEngine.UI;

namespace FoodIsekaiZ.Display
{

    [DisallowMultipleComponent]
    public sealed class NpcEmojiPresentation : MonoBehaviour
    {
        private const float EntranceSeconds = 0.4f;
        private const float ChangeOutSeconds = 0.1f;
        private const float ChangeInSeconds = 0.3f;
        private enum ChangePhase { None, Out, In }

        private static readonly string[] EmotionObjectNames =
        {
            "Angry", "Bad", "Normal", "Smile", "Fun", "Love"
        };

        private readonly GameObject[] emotionObjects = new GameObject[EmotionObjectNames.Length];
        private readonly CanvasGroup[] emotionGroups = new CanvasGroup[EmotionObjectNames.Length];
        private readonly Vector3[] authoredEmotionScales = new Vector3[EmotionObjectNames.Length];
        private readonly float[] authoredEmotionAlphas = new float[EmotionObjectNames.Length];
        private Transform emojiRoot;
        private CanvasGroup bubbleGroup;
        private NpcEmojiBurstGraphic burst;
        private NpcLoveHeartParticles loveHearts;
        private Vector3 authoredBubbleScale;
        private float authoredBubbleAlpha;
        private int initialMoodLevel;
        private int visibleEmotionIndex = -1;
        private int requestedEmotionIndex = -1;
        private float entranceElapsed;
        private float changeElapsed;
        private float emotionScale = 1f;
        private float emotionAlpha = 1f;
        private float changeStartScale;
        private float changeStartAlpha;
        private ChangePhase changePhase;

        // Binds the authored Emoji bubble and its direct emotion children, then hides the bubble.
        // Initial preference is clamped from zero (Normal) to two (Fun).
        // The NPC's body image provides the bounds for Love hearts.
        public void Initialize(Transform emojiRoot, int initialMoodLevel, Image visualBody = null)
        {
            Hide();
            if (burst != null)
            {
                Destroy(burst.gameObject);
                burst = null;
            }
            if (loveHearts != null)
            {
                loveHearts.Stop();
                Destroy(loveHearts.gameObject);
                loveHearts = null;
            }
            this.emojiRoot = emojiRoot;
            this.initialMoodLevel = Mathf.Clamp(initialMoodLevel, 0, 2);
            bubbleGroup = null;
            if (emojiRoot != null)
            {
                authoredBubbleScale = emojiRoot.localScale;
                bubbleGroup = GetOrAddGroup(emojiRoot.gameObject);
                authoredBubbleAlpha = bubbleGroup.alpha;
                CreateBurst(emojiRoot as RectTransform);
                CreateLoveHearts(visualBody);
            }

            for (int index = 0; index < emotionObjects.Length; index++)
            {
                Transform child = emojiRoot != null ? emojiRoot.Find(EmotionObjectNames[index]) : null;
                emotionObjects[index] = child != null ? child.gameObject : null;
                emotionGroups[index] = child != null ? GetOrAddGroup(child.gameObject) : null;
                authoredEmotionScales[index] = child != null ? child.localScale : Vector3.one;
                authoredEmotionAlphas[index] = child != null ? emotionGroups[index].alpha : 1f;
            }

            Hide();
        }

        public void ShowInitialMood()
        {
            ShowMood(initialMoodLevel);
        }

        public void ShowBad()
        {
            ShowMood(-1);
        }

        public void ShowAngry()
        {
            ShowMood(-2);
        }
        public void ShowLove()
        {
            ShowMood(3);
        }
        public void Hide()
        {
            visibleEmotionIndex = -1;
            requestedEmotionIndex = -1;
            changePhase = ChangePhase.None;
            entranceElapsed = 0f;
            changeElapsed = 0f;
            burst?.Stop();
            loveHearts?.FadeOut();
            for (int index = 0; index < emotionObjects.Length; index++)
            {
                RestoreEmotion(index);
                GameObject emotion = emotionObjects[index];
                if (emotion != null && emotion.activeSelf)
                {
                    emotion.SetActive(false);
                }
            }

            if (emojiRoot != null)
            {
                emojiRoot.localScale = authoredBubbleScale;
            }
            if (bubbleGroup != null)
            {
                bubbleGroup.alpha = authoredBubbleAlpha;
            }
            if (emojiRoot != null && emojiRoot.gameObject.activeSelf)
            {
                emojiRoot.gameObject.SetActive(false);
            }
        }

        private void ShowMood(int moodLevel)
        {
            int emotionIndex = moodLevel + 2;
            if (emojiRoot == null || emotionObjects[emotionIndex] == null)
            {
                Hide();
                return;
            }

            if (requestedEmotionIndex == emotionIndex && emojiRoot.gameObject.activeSelf)
            {
                return;
            }

            requestedEmotionIndex = emotionIndex;
            if (visibleEmotionIndex < 0 || !emojiRoot.gameObject.activeSelf)
            {
                SetVisibleEmotion(emotionIndex);
                entranceElapsed = 0f;
                changePhase = ChangePhase.None;
                emojiRoot.gameObject.SetActive(true);
                ApplyBubbleMotion(0.45f, 0f);
                burst?.Play(moodLevel, true);
                return;
            }

            changeStartScale = emotionScale;
            changeStartAlpha = emotionAlpha;
            changeElapsed = 0f;
            changePhase = ChangePhase.Out;
        }

        private void SetVisibleEmotion(int emotionIndex)
        {
            for (int index = 0; index < emotionObjects.Length; index++)
            {
                RestoreEmotion(index);
                GameObject emotion = emotionObjects[index];
                bool shouldShow = index == emotionIndex;
                if (emotion != null && emotion.activeSelf != shouldShow)
                {
                    emotion.SetActive(shouldShow);
                }
            }

            visibleEmotionIndex = emotionIndex;
            emotionScale = 1f;
            emotionAlpha = 1f;
            // Start only when Love is actually revealed, after the old face has faded out.
            if (emotionIndex == EmotionObjectNames.Length - 1)
            {
                loveHearts?.Play();
            }
            else
            {
                loveHearts?.FadeOut();
            }
        }

        private void Update()
        {
            if (visibleEmotionIndex < 0 || emojiRoot == null || !emojiRoot.gameObject.activeSelf || Time.deltaTime <= 0f)
            {
                return;
            }

            entranceElapsed = Mathf.Min(EntranceSeconds, entranceElapsed + Time.deltaTime);
            float entranceProgress = entranceElapsed / EntranceSeconds;
            float bubbleScale = PopScale(entranceProgress, 0.45f, 1.14f);
            float bubbleAlpha = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(entranceProgress / 0.45f));

            if (changePhase != ChangePhase.None)
            {
                changeElapsed += Time.deltaTime;
                if (changePhase == ChangePhase.Out)
                {
                    float progress = Mathf.Clamp01(changeElapsed / ChangeOutSeconds);
                    float eased = Mathf.SmoothStep(0f, 1f, progress);
                    ApplyEmotionMotion(Mathf.Lerp(changeStartScale, 0.6f, eased),
                        Mathf.Lerp(changeStartAlpha, 0f, eased));
                    if (progress >= 1f)
                    {
                        SetVisibleEmotion(requestedEmotionIndex);
                        changeElapsed -= ChangeOutSeconds;
                        changePhase = ChangePhase.In;
                        burst?.Play(requestedEmotionIndex - 2, false);
                    }
                }

                if (changePhase == ChangePhase.In)
                {
                    float progress = Mathf.Clamp01(changeElapsed / ChangeInSeconds);
                    ApplyEmotionMotion(PopScale(progress, 0.55f, 1.2f),
                        Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(progress / 0.45f)));
                    bubbleScale *= 1f + 0.06f * Mathf.Sin(progress * Mathf.PI);
                    if (progress >= 1f)
                    {
                        changePhase = ChangePhase.None;
                    }
                }
            }

            ApplyBubbleMotion(bubbleScale, bubbleAlpha);
        }

        private void ApplyBubbleMotion(float scale, float alpha)
        {
            // Breathing owns position; this effect only animates scale and opacity.
            emojiRoot.localScale = authoredBubbleScale * scale;
            bubbleGroup.alpha = authoredBubbleAlpha * alpha;
        }

        private void ApplyEmotionMotion(float scale, float alpha)
        {
            emotionScale = scale;
            emotionAlpha = alpha;
            emotionObjects[visibleEmotionIndex].transform.localScale = authoredEmotionScales[visibleEmotionIndex] * scale;
            emotionGroups[visibleEmotionIndex].alpha = authoredEmotionAlphas[visibleEmotionIndex] * alpha;
        }

        private void RestoreEmotion(int index)
        {
            if (emotionObjects[index] != null)
            {
                emotionObjects[index].transform.localScale = authoredEmotionScales[index];
            }
            if (emotionGroups[index] != null)
            {
                emotionGroups[index].alpha = authoredEmotionAlphas[index];
            }
        }

        private void CreateBurst(RectTransform bubble)
        {
            if (bubble == null)
            {
                return;
            }

            // A sibling renders behind the bubble's own Image; a child would render over it.
            GameObject effectObject = new GameObject("Emoji Burst", typeof(RectTransform), typeof(CanvasRenderer));
            effectObject.layer = bubble.gameObject.layer;
            effectObject.transform.SetParent(bubble.parent, false);
            effectObject.transform.SetSiblingIndex(bubble.GetSiblingIndex());
            burst = effectObject.AddComponent<NpcEmojiBurstGraphic>();
            burst.Initialize(bubble);
        }

        private static CanvasGroup GetOrAddGroup(GameObject target)
        {
            CanvasGroup group = target.GetComponent<CanvasGroup>();
            return group != null ? group : target.AddComponent<CanvasGroup>();
        }

        private void CreateLoveHearts(Image visualBody)
        {
            if (visualBody == null || emojiRoot.parent == null)
            {
                return;
            }

            GameObject prefabObject = Resources.Load<GameObject>("UI/NpcLoveHeartParticles");
            NpcLoveHeartParticles prefab = prefabObject != null ? prefabObject.GetComponent<NpcLoveHeartParticles>() : null;
            if (prefab != null)
            {
                loveHearts = Instantiate(prefab, emojiRoot.parent, false);
            }
            else
            {
                GameObject effectObject = new GameObject("NPC Love Hearts", typeof(RectTransform), typeof(CanvasRenderer));
                effectObject.transform.SetParent(emojiRoot.parent, false);
                loveHearts = effectObject.AddComponent<NpcLoveHeartParticles>();
            }

            loveHearts.gameObject.name = "NPC Love Hearts";
            loveHearts.gameObject.layer = emojiRoot.gameObject.layer;
            // Keep the hearts above the NPC body and below its emoji bubble.
            loveHearts.transform.SetSiblingIndex(emojiRoot.GetSiblingIndex());
            loveHearts.Initialize(visualBody);
        }

        private static float PopScale(float progress, float start, float peak)
        {
            const float peakTime = 0.6f;
            if (progress < peakTime)
            {
                float inverse = 1f - progress / peakTime;
                return Mathf.Lerp(start, peak, 1f - inverse * inverse * inverse);
            }
            return Mathf.Lerp(peak, 1f, Mathf.SmoothStep(0f, 1f, (progress - peakTime) / (1f - peakTime)));
        }

        private void OnDisable()
        {
            Hide();
            loveHearts?.Stop();
        }

        private void OnDestroy()
        {
            if (burst != null)
            {
                Destroy(burst.gameObject);
            }
            if (loveHearts != null)
            {
                Destroy(loveHearts.gameObject);
            }
        }
    }
}
