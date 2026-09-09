using FoodIsekaiZ.Display;
using UnityEngine;

namespace FoodIsekaiZ.Audio
{
    // Each coin pile emits one cue when its visible reminder cycle begins.
    public sealed class MoneyReminderSoundFeedback : MonoBehaviour
    {
        [SerializeField] private FloorAnimatedSprite[] coinPiles;
        [SerializeField] private GameSoundPlayer soundPlayer;

        private void OnEnable()
        {
            if (coinPiles == null) return;
            foreach (FloorAnimatedSprite pile in coinPiles)
                if (pile != null) pile.ReminderStarted += PlayReminder;
        }

        private void OnDisable()
        {
            if (coinPiles == null) return;
            foreach (FloorAnimatedSprite pile in coinPiles)
                if (pile != null) pile.ReminderStarted -= PlayReminder;
        }

        private void PlayReminder() => soundPlayer?.TryPlay(GameSoundCue.MoneyReminder);
    }
}
