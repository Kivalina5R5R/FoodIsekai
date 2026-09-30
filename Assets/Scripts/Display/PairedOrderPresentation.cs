using FoodIsekaiZ.Gameplay;
using UnityEngine;

namespace FoodIsekaiZ.Display
{
    // Coordinates the partial-delivery transition without delaying gameplay or editing the authored layout.
    [DisallowMultipleComponent]
    public sealed class PairedOrderPresentation : MonoBehaviour
    {
        [SerializeField] private RectTransform firstCard;
        [SerializeField] private RectTransform secondCard;
        [SerializeField] private RectTransform singleCard;
        [SerializeField] private CustomerPanelSuccessParticles firstParticles;
        [SerializeField] private CustomerPanelSuccessParticles secondParticles;
        [SerializeField, Min(0.1f)] private float durationSeconds = 0.58f;

        private OrderCardMeshEffect[] meshEffects;
        private FoodType firstFood;
        private bool hadPair;
        private int servedCard;
        private float elapsed;

        public bool IsTransitioning { get; private set; }

        // Observe food identities as well as counts so serving the right-hand dish removes the correct card.
        public void Synchronize(ArenaSlot2D slot)
        {
            if (slot == null || slot.CustomerState != CustomerSlotState.WaitingForFood || slot.IsOmakase)
            {
                ResetPresentation();
                return;
            }

            if (IsTransitioning && elapsed >= durationSeconds)
            {
                IsTransitioning = false;
                RefreshMeshes();
            }

            if (slot.RemainingFoods.Count > 1)
            {
                if (IsTransitioning) ResetPresentation();
                hadPair = true;
                firstFood = slot.RemainingFoods[0];
                return;
            }

            if (!hadPair || slot.RemainingFoods.Count != 1) return;
            hadPair = false;
            servedCard = slot.LastServedFood == firstFood ? 0 : 1;
            elapsed = 0f;
            IsTransitioning = true;
            (servedCard == 0 ? firstParticles : secondParticles)?.Play();
            RefreshMeshes();
        }

        // Cards 0 and 1 are the pair; card 2 is the normal single-dish display.
        public void GetPose(int card, out Vector3 center, out Vector3 offset, out float scale, out float alpha)
        {
            RectTransform pivot = card == 0 ? firstCard : card == 1 ? secondCard : singleCard;
            center = pivot != null ? transform.InverseTransformPoint(pivot.TransformPoint(pivot.rect.center)) : Vector3.zero;
            offset = Vector3.zero;
            scale = alpha = 1f;
            if (!IsTransitioning || firstCard == null || secondCard == null || singleCard == null) return;

            float progress = Mathf.Clamp01(elapsed / Mathf.Max(0.1f, durationSeconds));
            if (card == servedCard)
            {
                float pop = Mathf.SmoothStep(0f, 1f, progress / 0.16f);
                float collapse = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.16f, 0.55f, progress));
                scale = Mathf.Lerp(Mathf.Lerp(1f, 1.09f, pop), 0.35f, collapse);
                alpha = 1f - collapse;
                offset = Vector3.up * (18f * collapse);
                return;
            }

            RectTransform survivor = servedCard == 0 ? secondCard : firstCard;
            Vector3 origin = transform.InverseTransformPoint(survivor.TransformPoint(survivor.rect.center));
            Vector3 destination = transform.InverseTransformPoint(singleCard.TransformPoint(singleCard.rect.center));
            float move = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.16f, 0.86f, progress));
            float blend = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.4f, 0.8f, progress));
            float relativeSize = survivor.localScale.x / Mathf.Max(0.01f, singleCard.localScale.x);
            float visibleScale = Mathf.Lerp(relativeSize, 1f, move) + Mathf.Sin(blend * Mathf.PI) * 0.025f;
            if (card != 2)
            {
                offset = (destination - origin) * move;
                scale = visibleScale / Mathf.Max(0.01f, relativeSize);
                alpha = 1f - blend;
                return;
            }

            offset = (origin - destination) * (1f - move);
            scale = visibleScale;
            alpha = blend;
        }

        private void Update()
        {
            if (!IsTransitioning) return;
            elapsed = Mathf.Min(durationSeconds, elapsed + Time.deltaTime);
            RefreshMeshes();
        }

        private void RefreshMeshes()
        {
            if (meshEffects == null) meshEffects = GetComponentsInChildren<OrderCardMeshEffect>(true);
            foreach (OrderCardMeshEffect effect in meshEffects) effect.Refresh();
        }

        private void ResetPresentation()
        {
            hadPair = false;
            if (!IsTransitioning) return;
            IsTransitioning = false;
            elapsed = 0f;
            firstParticles?.Stop();
            secondParticles?.Stop();
            RefreshMeshes();
        }

        private void OnDisable() => ResetPresentation();
    }
}
