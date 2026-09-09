using FoodIsekaiZ.Gameplay;
using UnityEngine;

namespace FoodIsekaiZ.Audio
{
    // Maps completed gameplay actions to sound; wave notifications also arrive for timer ticks.
    public sealed class GameplaySoundFeedback : MonoBehaviour
    {
        [SerializeField] private FoodIsekaiZGameManager gameManager;
        [SerializeField] private GameSoundPlayer soundPlayer;
        private MealWavePhase lastPhase;
        private int lastWave;
        private int lastWarningSecond = -1;

        private void OnEnable()
        {
            if (gameManager == null || soundPlayer == null) return;
            lastPhase = gameManager.CurrentMealWavePhase;
            lastWave = gameManager.CurrentWaveNumber;
            lastWarningSecond = -1;
            gameManager.FoodPickedUp += Pickup;
            gameManager.FoodServed += Served;
            gameManager.WrongFoodDiscarded += Wrong;
            gameManager.CustomerMoneySpawned += Paid;
            gameManager.PlayerMoneyCollected += Collected;
            gameManager.PlayerMoneyDeposited += Deposited;
            gameManager.CustomerOrderExpired += Expired;
            gameManager.MealWaveDisplayChanged += WaveChanged;
        }

        private void OnDisable()
        {
            if (gameManager == null) return;
            gameManager.FoodPickedUp -= Pickup;
            gameManager.FoodServed -= Served;
            gameManager.WrongFoodDiscarded -= Wrong;
            gameManager.CustomerMoneySpawned -= Paid;
            gameManager.PlayerMoneyCollected -= Collected;
            gameManager.PlayerMoneyDeposited -= Deposited;
            gameManager.CustomerOrderExpired -= Expired;
            gameManager.MealWaveDisplayChanged -= WaveChanged;
        }

        private void Pickup(FoodIsekaiZPlayerState player, ArenaSlot2D slot) => soundPlayer.TryPlay(GameSoundCue.FoodPickup);
        private void Served(FoodIsekaiZPlayerState player, ArenaSlot2D slot) => soundPlayer.TryPlay(GameSoundCue.FoodServed);
        private void Wrong(FoodIsekaiZPlayerState player, ArenaSlot2D slot) => soundPlayer.TryPlay(GameSoundCue.WrongFood);
        private void Paid(ArenaSlot2D slot, int amount) => soundPlayer.TryPlay(GameSoundCue.MoneyPaid);
        private void Collected(FoodIsekaiZPlayerState player, ArenaSlot2D slot, int amount) => soundPlayer.TryPlay(GameSoundCue.MoneyCollected);
        private void Deposited(int playerId, int amount) => soundPlayer.TryPlay(GameSoundCue.BankDeposit);
        private void Expired(ArenaSlot2D slot) => soundPlayer.TryPlay(GameSoundCue.CustomerExpired, true);

        private void WaveChanged()
        {
            MealWavePhase phase = gameManager.CurrentMealWavePhase;
            int wave = gameManager.CurrentWaveNumber;
            if (phase != lastPhase || wave != lastWave)
            {
                lastWarningSecond = -1;
                switch (phase)
                {
                    case MealWavePhase.Active: soundPlayer.TryPlay(GameSoundCue.WaveStart, true); break;
                    case MealWavePhase.Intermission: soundPlayer.TryPlay(GameSoundCue.WaveBreak, true); break;
                    case MealWavePhase.Completed: soundPlayer.TryPlay(GameSoundCue.ServiceComplete, true); break;
                }
                lastPhase = phase;
                lastWave = wave;
            }
            int second = Mathf.CeilToInt(gameManager.MealPhaseRemainingSeconds);
            if (phase == MealWavePhase.Active && (second == 10 || second == 5) && second != lastWarningSecond)
            {
                lastWarningSecond = second;
                soundPlayer.TryPlay(GameSoundCue.TimeWarning, true);
            }
        }
    }
}
