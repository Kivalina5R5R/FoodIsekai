using System;
using System.Collections.Generic;
using FoodIsekaiZ.Audio;
using FoodIsekaiZ.Gameplay;
using FoodIsekaiZ.Players;
using TMPro;
using UnityEngine;

namespace FoodIsekaiZ.Display
{
    // Coordinates the break shop's guide, UWB selection and recorded purchases.
    public sealed class PerkShopPresentation : MonoBehaviour, IMealIntermissionGate
    {
        [SerializeField] private FoodIsekaiZGameManager gameManager;
        [SerializeField] private UWBPlayerSpawner playerSpawner;
        [SerializeField] private GameSoundPlayer soundPlayer;
        [SerializeField] private PerkCatalog catalog;
        [SerializeField] private MealMenuTransition menuTransition;
        [SerializeField] private NpcGuidePresentation guide;
        [SerializeField] private RectTransform guideStart;
        [SerializeField] private RectTransform guideDestination;
        [SerializeField] private RectTransform guideStage;
        [SerializeField] private GameObject offersRoot;
        [SerializeField] private GameObject floorRoot;
        [SerializeField] private PerkCardSlot[] slots;
        [SerializeField] private PerkFloorZone[] zones;
        [SerializeField] private TMP_Text walletText;
        [SerializeField] private TMP_Text countdownText;
        [SerializeField, Min(0.1f)] private float holdSeconds = 1.5f;
        [SerializeField, HideInInspector] private int layoutRevision;
        private PerkShopSession session;
        private PerkSelectionHold[] holds;
        private bool entranceStarted;
        private bool selectionStarted;
        private bool closing;

        public int LayoutRevision => layoutRevision;
        public bool CanCountDown => selectionStarted && !closing && guide != null && guide.HasFinishedSpeaking;
        public IReadOnlyList<PerkPurchase> Purchases => session != null
            ? session.Purchases : Array.Empty<PerkPurchase>();

        private void OnEnable()
        {
            if (gameManager == null || catalog == null || slots == null || zones == null ||
                slots.Length != 4 || zones.Length != 4 || guide == null ||
                guideStart == null || guideDestination == null || guideStage == null)
            {
                Debug.LogError("Assign the perk catalog, guide, four cards and four floor zones.", this);
                enabled = false;
                return;
            }
            if (session == null) session = new PerkShopSession(gameManager, new System.Random());
            bool big = gameManager.CurrentWaveNumber >= 2;
            session.Open(catalog.GetOffers(big), big, gameManager.CurrentWaveNumber + 1);
            holds = new PerkSelectionHold[4];
            for (int i = 0; i < slots.Length; i++)
            {
                holds[i] = new PerkSelectionHold(Mathf.Max(0.1f, holdSeconds));
                PerkOffer offer = i < session.Offers.Count ? session.Offers[i] : null;
                slots[i].SetOffer(offer, offer != null ? catalog.GetPrefab(offer.Id) : null);
                zones[i].gameObject.SetActive(offer != null);
            }
            entranceStarted = false;
            selectionStarted = false;
            closing = false;
            offersRoot.SetActive(false);
            floorRoot.SetActive(false);
            guide.gameObject.SetActive(false);
            gameManager.RegisterIntermissionGate(this);
            UpdateLabels();
        }

        private void Update()
        {
            if (closing || session == null || !session.IsOpen) return;
            if (!entranceStarted)
            {
                if (menuTransition != null && menuTransition.IsVisible) return;
                entranceStarted = true;
                guide.gameObject.SetActive(true);
                guide.UsePerkDialogue(true);
                guide.WalkIn(guideStart.anchoredPosition, guideDestination.anchoredPosition, guideStage.rect.width);
            }
            if (!selectionStarted)
            {
                if (!guide.HasOpenedDialogue) return;
                selectionStarted = true;
                offersRoot.SetActive(true);
                floorRoot.SetActive(true);
            }
            if (gameManager.MealPhaseRemainingSeconds <= 0f) return;
            UpdateSelection();
            UpdateLabels();
        }

        private void UpdateSelection()
        {
            for (int i = 0; i < zones.Length; i++)
            {
                int occupants = 0;
                int candidate = 0;
                if (playerSpawner != null)
                {
                    foreach (UWBPlayerController player in playerSpawner.SpawnedPlayers)
                    {
                        if (player == null || player.PlayerId <= 0 || !player.IsAvailableForSelection ||
                            !player.gameObject.activeInHierarchy || !zones[i].Contains(player.transform.position)) continue;
                        occupants++;
                        candidate = player.PlayerId;
                    }
                }
                bool canBuy = CanCountDown && session.CanBuy(i);
                bool confirmed = holds[i].Tick(canBuy && occupants == 1 ? (int?)candidate : null, Time.unscaledDeltaTime);
                if (confirmed)
                {
                    if (session.TryBuy(i, candidate)) soundPlayer?.TryPlay(GameSoundCue.BankDeposit, true);
                    holds[i].Reset();
                }
                bool bought = session.IsPurchased(i);
                string status = !CanCountDown ? "LISTEN TO LUNAR" : bought ? "PURCHASED" : session.IsLocked ? "LOCKED" :
                    !session.CanBuy(i) ? "NOT ENOUGH COINS" : occupants > 1 ? "ONE PLAYER AT A TIME" :
                    occupants == 1 ? "HOLD TO BUY" : "STAND HERE TO BUY";
                zones[i].ShowStatus(status, holds[i].Progress, bought, occupants > 1);
            }
        }

        private void UpdateLabels()
        {
            if (walletText != null) walletText.text = $"TEAM COINS  {gameManager.TotalBankedMoney}";
            if (countdownText != null)
                countdownText.text = CanCountDown
                    ? $"{Mathf.CeilToInt(gameManager.MealPhaseRemainingSeconds):00}s"
                    : "LUNAR'S PERK SHOP";
        }

        public bool TryFinish()
        {
            if (!closing)
            {
                closing = true;
                session?.Close();
                offersRoot.SetActive(false);
                floorRoot.SetActive(false);
                if (countdownText != null) countdownText.text = string.Empty;
                if (entranceStarted && guide != null) guide.WalkOut(guideStart.anchoredPosition);
            }
            return !entranceStarted || guide == null || guide.HasExited || !guide.gameObject.activeInHierarchy;
        }

        private void OnDisable()
        {
            session?.Close();
            gameManager?.ReleaseIntermissionGate(this);
            if (floorRoot != null) floorRoot.SetActive(false);
            if (guide != null) guide.gameObject.SetActive(false);
        }
    }
}
