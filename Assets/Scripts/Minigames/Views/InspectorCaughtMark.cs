using UnityEngine;

namespace FoodIsekaiZ.Minigames.Views
{
    // The floor effect under a player the Health Inspector caught moving: a red ring and cross that
    // follow the player and pulse, so they can see on the floor that they are out.
    public sealed class InspectorCaughtMark : MonoBehaviour
    {
        // Pulses per second and how much bigger the mark gets at the top of a pulse.
        [SerializeField] private float pulseSpeed = 2.5f;
        [SerializeField, Range(0f, 1f)] private float pulseAmount = 0.15f;

        private MinigamePlayer player;
        private MinigameStage stage;
        private RectTransform rect;

        public void Follow(MinigamePlayer caught, MinigameStage onStage)
        {
            player = caught;
            stage = onStage;
            rect = (RectTransform)transform;
            Place();
        }

        private void LateUpdate()
        {
            if (player == null) return;
            Place();
            float pulse = 1f + pulseAmount * (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * pulseSpeed * Mathf.PI * 2f));
            rect.localScale = Vector3.one * pulse;
        }

        private void Place()
        {
            rect.anchoredPosition = stage.FloorPoint(player.Position);
        }
    }
}
