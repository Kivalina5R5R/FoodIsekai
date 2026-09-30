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
        [SerializeField] private SmallPerkPurchasePresentation smallPurchases;
        [SerializeField] private TMP_Text walletText;
        private TMP_Text countdownText;
        [SerializeField, Min(0.1f)] private float holdSeconds = 1.5f;
        [SerializeField, HideInInspector] private int layoutRevision;
        private PerkShopSession session;
        private PerkSelectionHold[] holds;
        private bool entranceStarted;
        private bool selectionStarted;
        private bool wallShown;
        private bool floorShown;
        private bool closing;
        private bool departureStarted;
        private bool bigShop;

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
            countdownText = guide.transform.Find("TextPerk/Time/TextTime")?.GetComponent<TMP_Text>();
            if (session == null) session = new PerkShopSession(gameManager, new System.Random(), gameManager.Perks);
            bool big = gameManager.CurrentWaveNumber >= 2;
            bigShop = big;
            var perkMonitor = gameManager.GetComponentInChildren<PerkManagerMonitor>(true);
            bool simulation = gameManager.IsSimulationMode;
            session.Open(catalog.GetOffers(big), big, gameManager.CurrentWaveNumber + 1,
                simulation ? perkMonitor != null ? perkMonitor.GetSimulationOffers(big) : Array.Empty<string>() : null);
            // These messages also reach Player.log on the installation computer.
            Debug.Log($"[PerkShop] Open: version={Application.version}, tier={(big ? "Big" : "Small")}, offers={session.Offers.Count}, coins={gameManager.TotalBankedMoney}, players={playerSpawner?.SpawnedPlayers.Count ?? 0}.", this);
            if (session.Offers.Count == 0 && simulation)
                Debug.Log("[PerkShop] Simulation: no eligible offers for this break. An empty selection uses normal random offers; checked selections remain exclusive.", this);
            else if (session.Offers.Count == 0)
                Debug.LogError("[PerkShop] No valid cards in the built catalog. Low money does not hide offers; check the catalog included in this build.", this);
            holds = new PerkSelectionHold[4];
            for (int i = 0; i < slots.Length; i++)
            {
                holds[i] = new PerkSelectionHold(Mathf.Max(0.1f, holdSeconds));
                PerkOffer offer = i < session.Offers.Count ? session.Offers[i] : null;
                slots[i].SetOffer(offer, offer != null ? catalog.GetPrefab(offer.Id) : null, big);
                zones[i].gameObject.SetActive(offer != null);
            }
            entranceStarted = false;
            selectionStarted = false;
            wallShown = false;
            floorShown = false;
            closing = false;
            departureStarted = false;
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
            if (!wallShown)
            {
                if (!guide.HasOpenedDialogue) return;
                wallShown = true;
                offersRoot.SetActive(true);
            }
            if (!floorShown)
            {
                foreach (PerkCardSlot slot in slots)
                    if (!slot.HasRevealed) return;
                floorShown = true;
                floorRoot.SetActive(true);
                Debug.Log($"[PerkShop] Reveal: wall={offersRoot.activeInHierarchy}, floor={floorRoot.activeInHierarchy}, offers={session.Offers.Count}.", this);
            }
            if (!selectionStarted)
            {
                foreach (PerkFloorZone zone in zones)
                    if (!zone.HasRevealed) return;
                selectionStarted = true;
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
                    if (session.TryBuy(i, candidate))
                    {
                        soundPlayer?.TryPlay(GameSoundCue.BankDeposit, true);
                        if (!bigShop && smallPurchases != null) smallPurchases.Play(slots[i]);
                    }
                    holds[i].Reset();
                }
                bool bought = session.IsPurchased(i);
                string status = bought ? "PURCHASED" : "NOT PURCHASED";
                bool showGauge = occupants > 0 && CanCountDown && session.CanBuy(i);
                zones[i].ShowStatus(status, holds[i].Progress, bought, occupants > 1, showGauge);
            }
        }

        private void UpdateLabels()
        {
            if (walletText != null) walletText.text = $"TEAM COINS  {gameManager.TotalBankedMoney}";
            if (countdownText != null)
                countdownText.text = $"{Mathf.Max(0, Mathf.CeilToInt(gameManager.MealPhaseRemainingSeconds)):00}";
        }

        public bool TryFinish()
        {
            if (!closing)
            {
                closing = true;
                session?.Close();
                foreach (PerkCardSlot slot in slots) slot.Hide();
                foreach (PerkFloorZone zone in zones) zone.Hide();
                if (countdownText != null) countdownText.text = "00";
            }
            foreach (PerkCardSlot slot in slots)
                if (!slot.IsHidden) return false;
            foreach (PerkFloorZone zone in zones)
                if (!zone.IsHidden) return false;
            if (smallPurchases != null && smallPurchases.IsPlaying) return false;
            if (!departureStarted)
            {
                departureStarted = true;
                offersRoot.SetActive(false);
                floorRoot.SetActive(false);
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
