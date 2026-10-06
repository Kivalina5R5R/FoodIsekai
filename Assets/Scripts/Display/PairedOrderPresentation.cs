using System.Collections.Generic;
using FoodIsekaiZ.Gameplay;
using UnityEngine;

namespace FoodIsekaiZ.Display
{
    // Coordinates the partial-delivery transitions without delaying gameplay or editing the authored layout.
    // Two dishes down to one: the served card pops and fades while the other glides onto the single card.
    // Three dishes down to two: the served card pops and fades while the other two glide onto the pair cards.
    // Three dishes down to one, when a player hands over two dishes at once: both served cards pop and fade
    // while the last one glides onto the single card.
    // Each served card plays the same delivery sparkles.
    [DisallowMultipleComponent]
    public sealed class PairedOrderPresentation : MonoBehaviour
    {
        [SerializeField] private RectTransform firstCard;
        [SerializeField] private RectTransform secondCard;
        [SerializeField] private RectTransform singleCard;
        [SerializeField] private CustomerPanelSuccessParticles firstParticles;
        [SerializeField] private CustomerPanelSuccessParticles secondParticles;
        // The three cards of a three-dish order, left to right, and their delivery sparkles.
        [SerializeField] private RectTransform[] tripleCards = new RectTransform[3];
        [SerializeField] private CustomerPanelSuccessParticles[] tripleParticles = new CustomerPanelSuccessParticles[3];
        [SerializeField, Min(0.1f)] private float durationSeconds = 0.58f;

        private OrderCardMeshEffect[] meshEffects;
        private FoodType firstFood;
        private bool hadPair;
        private bool hadTriple;
        private readonly List<FoodType> tripleFoods = new List<FoodType>(3);
        private int servedCard;
        // For the three-dish transitions: which triple cards were served, and the ones that move onto
        // pair card 0 and pair card 1 (or, going straight to one dish, onto the single card).
        private readonly bool[] tripleServed = new bool[3];
        private readonly int[] tripleSurvivors = new int[2];
        private int tripleSurvivorCount;
        private float elapsed;

        // True during either transition; card meshes follow their poses only while this is set.
        public bool IsTransitioning { get; private set; }
        // True while the three-dish cards are leaving, so the layout keeps them on screen until they finish.
        public bool IsTripleTransitioning => IsTransitioning && tripleTransition;
        private bool tripleTransition;

        // Observe food identities as well as counts so serving any dish removes the matching card.
        public void Synchronize(ArenaSlot2D slot)
        {
            if (slot == null || slot.CustomerState != CustomerSlotState.WaitingForFood || slot.IsOmakase ||
                slot.RemainingFoods.Count > 3)
            {
                ResetPresentation();
                return;
            }

            if (IsTransitioning && elapsed >= durationSeconds)
            {
                IsTransitioning = false;
                tripleTransition = false;
                RefreshMeshes();
            }

            int count = slot.RemainingFoods.Count;
            if (count == 3)
            {
                if (IsTransitioning) ResetPresentation();
                hadTriple = true;
                hadPair = false;
                tripleFoods.Clear();
                tripleFoods.AddRange(slot.RemainingFoods);
                return;
            }

            if (count == 2)
            {
                if (hadTriple && HasTripleCards())
                {
                    hadTriple = false;
                    StartTripleTransition(slot.RemainingFoods);
                }
                else if (IsTransitioning && !tripleTransition)
                {
                    ResetPresentation();
                }
                hadTriple = false;
                hadPair = true;
                firstFood = slot.RemainingFoods[0];
                return;
            }

            if (count == 1 && hadTriple && HasTripleCards() && singleCard != null)
            {
                hadTriple = false;
                hadPair = false;
                StartTripleTransition(slot.RemainingFoods);
                return;
            }

            hadTriple = false;
            if (!hadPair || count != 1) return;
            hadPair = false;
            servedCard = slot.LastServedFood == firstFood ? 0 : 1;
            StartTransition(false);
            (servedCard == 0 ? firstParticles : secondParticles)?.Play();
        }

        // Matches the remaining menus back to the three cards; every card without a match was served.
        // Menus in one order are distinct, so each remaining dish identifies exactly one card.
        private void StartTripleTransition(IReadOnlyList<FoodType> remaining)
        {
            tripleSurvivorCount = 0;
            for (int card = 0; card < 3; card++)
            {
                bool kept = card < tripleFoods.Count && Contains(remaining, tripleFoods[card]) && tripleSurvivorCount < 2;
                tripleServed[card] = !kept;
                if (kept) tripleSurvivors[tripleSurvivorCount++] = card;
            }
            StartTransition(true);
            for (int card = 0; card < 3; card++)
                if (tripleServed[card] && card < tripleParticles.Length) tripleParticles[card]?.Play();
        }

        private static bool Contains(IReadOnlyList<FoodType> foods, FoodType food)
        {
            for (int i = 0; i < foods.Count; i++)
                if (foods[i] == food) return true;
            return false;
        }

        private void StartTransition(bool fromTriple)
        {
            elapsed = 0f;
            IsTransitioning = true;
            tripleTransition = fromTriple;
            RefreshMeshes();
        }

        private bool HasTripleCards() => tripleCards != null && tripleCards.Length == 3 &&
            tripleCards[0] != null && tripleCards[1] != null && tripleCards[2] != null &&
            firstCard != null && secondCard != null;

        private RectTransform GetCard(int card)
        {
            if (card == 0) return firstCard;
            if (card == 1) return secondCard;
            if (card == 2) return singleCard;
            int triple = card - 3;
            return tripleCards != null && triple >= 0 && triple < tripleCards.Length ? tripleCards[triple] : null;
        }

        // Cards 0 and 1 are the pair, card 2 is the single-dish display and cards 3 to 5 are the three-dish cards.
        public void GetPose(int card, out Vector3 center, out Vector3 offset, out float scale, out float alpha)
        {
            RectTransform pivot = GetCard(card);
            center = pivot != null ? CenterOf(pivot) : Vector3.zero;
            offset = Vector3.zero;
            scale = alpha = 1f;
            if (!IsTransitioning) return;

            float progress = Mathf.Clamp01(elapsed / Mathf.Max(0.1f, durationSeconds));
            if (tripleTransition)
            {
                if (!HasTripleCards()) return;
                if (card >= 3 && tripleServed[card - 3])
                {
                    ServedPose(progress, out offset, out scale, out alpha);
                    return;
                }
                for (int target = 0; target < tripleSurvivorCount; target++)
                {
                    RectTransform survivor = tripleCards[tripleSurvivors[target]];
                    // One survivor goes to the single card; two survivors go to the pair cards in order.
                    int destinationCard = tripleSurvivorCount == 1 ? 2 : target;
                    RectTransform destination = GetCard(destinationCard);
                    if (destination == null) return;
                    if (card == 3 + tripleSurvivors[target])
                    {
                        MovePose(survivor, destination, progress, true, out offset, out scale, out alpha);
                        return;
                    }
                    if (card == destinationCard)
                    {
                        MovePose(survivor, destination, progress, false, out offset, out scale, out alpha);
                        return;
                    }
                }
                return;
            }

            if (firstCard == null || secondCard == null || singleCard == null || card > 2) return;
            if (card == servedCard)
            {
                ServedPose(progress, out offset, out scale, out alpha);
                return;
            }
            RectTransform pairSurvivor = servedCard == 0 ? secondCard : firstCard;
            MovePose(pairSurvivor, singleCard, progress, card != 2, out offset, out scale, out alpha);
        }

        // The served card swells briefly, then shrinks upward and fades out.
        private static void ServedPose(float progress, out Vector3 offset, out float scale, out float alpha)
        {
            float pop = Mathf.SmoothStep(0f, 1f, progress / 0.16f);
            float collapse = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.16f, 0.55f, progress));
            scale = Mathf.Lerp(Mathf.Lerp(1f, 1.09f, pop), 0.35f, collapse);
            alpha = 1f - collapse;
            offset = Vector3.up * (18f * collapse);
        }

        // A remaining card glides onto its new card and cross-fades into it. The leaving card fades out as it
        // arrives, while the arriving card starts at the leaving card's place and size and fades in.
        private void MovePose(RectTransform survivor, RectTransform destination, float progress, bool leaving,
            out Vector3 offset, out float scale, out float alpha)
        {
            Vector3 origin = CenterOf(survivor);
            Vector3 target = CenterOf(destination);
            float move = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.16f, 0.86f, progress));
            float blend = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.4f, 0.8f, progress));
            float relativeSize = survivor.localScale.x / Mathf.Max(0.01f, destination.localScale.x);
            float visibleScale = Mathf.Lerp(relativeSize, 1f, move) + Mathf.Sin(blend * Mathf.PI) * 0.025f;
            if (leaving)
            {
                offset = (target - origin) * move;
                scale = visibleScale / Mathf.Max(0.01f, relativeSize);
                alpha = 1f - blend;
                return;
            }
            offset = (origin - target) * (1f - move);
            scale = visibleScale;
            alpha = blend;
        }

        private Vector3 CenterOf(RectTransform card) =>
            transform.InverseTransformPoint(card.TransformPoint(card.rect.center));

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
            hadTriple = false;
            if (!IsTransitioning) return;
            IsTransitioning = false;
            tripleTransition = false;
            elapsed = 0f;
            firstParticles?.Stop();
            secondParticles?.Stop();
            if (tripleParticles != null)
                foreach (CustomerPanelSuccessParticles particles in tripleParticles) particles?.Stop();
            RefreshMeshes();
        }

        private void OnDisable() => ResetPresentation();
    }
}
