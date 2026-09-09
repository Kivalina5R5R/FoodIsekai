using FoodIsekaiZ.Display;
using UnityEngine;

namespace FoodIsekaiZ.Audio
{
    // Audio follows order entrances and success particles and the first NPC emoji popup.
    public sealed class NpcPresentationSoundFeedback : MonoBehaviour
    {
        [SerializeField] private FoodIsekaiZNpcWaveSpawner npcSpawner;
        [SerializeField] private GameSoundPlayer soundPlayer;
        [SerializeField] private CustomerPanelPresentation[] orderPanels;

        [SerializeField, Min(0.06f)] private float emojiSpacing = 0.12f;
        private const int MaximumQueuedEmojis = 6;
        private int pendingEmojis;
        private float nextEmojiTime;

        private void OnEnable()
        {
            if (npcSpawner == null || soundPlayer == null) return;
            npcSpawner.OrderPanelShown += OrderShown;
            npcSpawner.EmojiShown += EmojiShown;
            if (orderPanels != null)
                foreach (CustomerPanelPresentation panel in orderPanels)
                    if (panel != null) panel.SuccessParticlesPlayed += SuccessParticlesPlayed;
        }

        private void OnDisable()
        {
            pendingEmojis = 0;
            nextEmojiTime = 0f;
            if (npcSpawner == null) return;
            npcSpawner.OrderPanelShown -= OrderShown;
            npcSpawner.EmojiShown -= EmojiShown;
            if (orderPanels != null)
                foreach (CustomerPanelPresentation panel in orderPanels)
                    if (panel != null) panel.SuccessParticlesPlayed -= SuccessParticlesPlayed;
        }

        private void OrderShown() => soundPlayer.TryPlay(GameSoundCue.OrderArrived);
        private void SuccessParticlesPlayed() => soundPlayer.TryPlay(GameSoundCue.SuccessPop);

        private void EmojiShown()
        {
            pendingEmojis = Mathf.Min(MaximumQueuedEmojis, pendingEmojis + 1);
            FlushEmojiQueue();
        }

        private void Update() => FlushEmojiQueue();

        private void FlushEmojiQueue()
        {
            if (pendingEmojis == 0 || soundPlayer == null || Time.unscaledTime < nextEmojiTime) return;
            if (!soundPlayer.TryPlay(GameSoundCue.EmojiBubble)) return;
            pendingEmojis--;
            nextEmojiTime = Time.unscaledTime + Mathf.Max(0.06f, emojiSpacing);
        }
    }
}
