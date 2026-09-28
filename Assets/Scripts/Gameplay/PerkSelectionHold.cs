using System;

namespace FoodIsekaiZ.Gameplay
{
    // A different, missing or contested player cannot inherit a previous player's hold.
    public sealed class PerkSelectionHold
    {
        private int? candidate;
        private float elapsed;
        private readonly float duration;
        public float Progress => Math.Min(1f, elapsed / duration);

        public PerkSelectionHold(float duration)
        {
            if (duration <= 0f) throw new ArgumentOutOfRangeException(nameof(duration));
            this.duration = duration;
        }

        public bool Tick(int? playerId, float delta)
        {
            if (!playerId.HasValue || candidate != playerId)
            {
                candidate = playerId;
                elapsed = 0f;
                return false;
            }
            elapsed += Math.Max(0f, Math.Min(delta, 0.1f));
            return elapsed >= duration;
        }

        public void Reset()
        {
            candidate = null;
            elapsed = 0f;
        }
    }
}
