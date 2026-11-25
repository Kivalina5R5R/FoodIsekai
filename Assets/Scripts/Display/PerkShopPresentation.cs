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
        // The floor coin panel animates in and out like the perk tiles, but appears with the wall cards.
        private PerkCardAnimation coinPanel;
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
        private readonly int[] audibleCandidates = new int[4];
        // The player each card's hold belongs to. Any number of players may share a card; the hold keeps
        // counting while its player stays on, and passes to another player on the card if they step off.
        private readonly int[] holdBuyers = new int[4];
        private const string PurchasedText = "PURCHASED";
        private const string NotPurchasedText = "NOT PURCHASED";

        public int LayoutRevision => layoutRevision;
        // The shop's guide, stage and floor tiles, also used by the minigame choice after each meal
        // (the shop itself is switched off then).
        public NpcGuidePresentation Guide => guide;
        public RectTransform GuideStart => guideStart;
        public RectTransform GuideDestination => guideDestination;
        public RectTransform GuideStage => guideStage;
        public MealMenuTransition MenuTransition => menuTransition;
        public GameObject FloorRoot => floorRoot;
        public IReadOnlyList<PerkFloorZone> Zones => zones;
        // The team coin panel in the middle of the floor, or null.
        public GameObject CoinPanel => walletText != null ? walletText.GetComponentInParent<PerkCardAnimation>(true)?.gameObject : null;
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
            coinPanel = walletText != null ? walletText.GetComponentInParent<PerkCardAnimation>(true) : null;
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
                audibleCandidates[i] = 0;
                holdBuyers[i] = 0;
                PerkOffer offer = i < session.Offers.Count ? session.Offers[i] : null;
                slots[i].SetOffer(offer, offer != null ? catalog.GetPrefab(offer.Id) : null, big);
                // Tiles stay off until the guide finishes speaking; the floor root itself opens earlier for the coin panel.
                zones[i].gameObject.SetActive(false);
                // Clear the previous shop's text before the floor reveals, so no stale "PURCHASED" flashes.
                zones[i].ShowStatus(NotPurchasedText, 0f, false, false, false);
            }
            entranceStarted = false;
            selectionStarted = false;
            wallShown = false;
            floorShown = false;
            closing = false;
            departureStarted = false;
            offersRoot.SetActive(false);
            floorRoot.SetActive(false);
            SetCoinPanelActive(false);
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
                // The coin panel enters on the wall with the perk cards; the floor opens early for its tiles.
                floorRoot.SetActive(true);
                SetCoinPanelActive(true);
                if (session.Offers.Count > 0) soundPlayer?.TryPlay(GameSoundCue.SmallPerkReveal);
            }
            if (!floorShown)
            {
                foreach (PerkCardSlot slot in slots)
                    if (!slot.HasRevealed) return;
                // The floor zones stay hidden until the guide finishes speaking, so players never see tiles they cannot use.
                if (!guide.HasFinishedSpeaking) return;
                floorShown = true;
                for (int i = 0; i < zones.Length; i++)
                    zones[i].gameObject.SetActive(i < session.Offers.Count);
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
                int firstOnCard = 0;
                bool buyerStillOn = false;
                if (playerSpawner != null)
                {
                    foreach (UWBPlayerController player in playerSpawner.SpawnedPlayers)
                    {
                        if (player == null || player.PlayerId <= 0 || !player.IsAvailableForSelection ||
                            !player.gameObject.activeInHierarchy || !zones[i].Contains(player.transform.position)) continue;
                        occupants++;
                        if (firstOnCard == 0) firstOnCard = player.PlayerId;
                        if (player.PlayerId == holdBuyers[i]) buyerStillOn = true;
                    }
                }
                int candidate = buyerStillOn ? holdBuyers[i] : firstOnCard;
                holdBuyers[i] = candidate;
                bool canBuy = CanCountDown && session.CanBuy(i);
                int audibleCandidate = CanCountDown && occupants > 0 && !session.IsPurchased(i) ? candidate : 0;
                if (audibleCandidate > 0 && audibleCandidate != audibleCandidates[i])
                {
                    soundPlayer?.TryPlay(canBuy ? GameSoundCue.SmallPerkFocus : GameSoundCue.SmallPerkUnavailable);
                    // Unaffordable tiles shake; buyable tiles use the occupied pulse below instead.
                    if (!canBuy) zones[i].ShakeWarning();
                }
                audibleCandidates[i] = audibleCandidate;
                float previousProgress = holds[i].Progress;
                bool confirmed = holds[i].Tick(canBuy && occupants > 0 ? (int?)candidate : null, Time.unscaledDeltaTime);
                int holdStep = Mathf.FloorToInt(holds[i].Progress * 3f);
                // Ticks climb a major third per step so the hold builds toward the purchase chord.
                if (!confirmed && holdStep > Mathf.FloorToInt(previousProgress * 3f))
                    soundPlayer?.TryPlay(GameSoundCue.SmallPerkHoldTick, false, Mathf.Pow(1.26f, holdStep - 1));
                if (!confirmed && previousProgress > .1f && holds[i].Progress == 0f)
                    soundPlayer?.TryPlay(GameSoundCue.SmallPerkCancel);
                if (confirmed)
                {
                    if (session.TryBuy(i, candidate))
                    {
                        PlayerSessionLog.RecordPerkPurchase(session.Offers[i].Id, session.Offers[i].Price, candidate,
                            gameManager.NextWaveName, bigShop, gameManager.TotalBankedMoney);
                        soundPlayer?.TryPlay(GameSoundCue.SmallPerkPurchase, true);
                        // The floor tile the player bought from bursts while the wall card takes flight.
                        zones[i].PlayPurchaseBurst(bigShop);
                        // Both tiers share the purchase animation and sounds; Big plays it in rainbow.
                        if (smallPurchases != null) smallPurchases.Play(slots[i], bigShop);
                        // A Big shop allows one pick: the break ends, the other cards leave and the guide walks out
                        // once the purchase animation finishes, then the next meal begins.
                        if (bigShop) gameManager.EndIntermissionEarly();
                    }
                    holds[i].Reset();
                }
                bool bought = session.IsPurchased(i);
                zones[i].SetOccupied(canBuy && occupants > 0 && !bought);
                string status = bought ? PurchasedText : NotPurchasedText;
                bool showGauge = occupants > 0 && CanCountDown && session.CanBuy(i);
                zones[i].ShowStatus(status, holds[i].Progress, bought, false, showGauge);
            }
        }

        private void UpdateLabels()
        {
            // The floor coin panel shows the team's coins as a number only.
            if (walletText != null) walletText.text = gameManager.TotalBankedMoney.ToString();
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
                if (coinPanel != null) coinPanel.Hide();
                if (countdownText != null) countdownText.text = "00";
            }
            foreach (PerkCardSlot slot in slots)
                if (!slot.IsHidden) return false;
            foreach (PerkFloorZone zone in zones)
                if (!zone.IsHidden) return false;
            if (coinPanel != null && coinPanel.isActiveAndEnabled && !coinPanel.IsHidden) return false;
            if (smallPurchases != null && smallPurchases.IsPlaying) return false;
            if (!departureStarted)
            {
                departureStarted = true;
                offersRoot.SetActive(false);
                floorRoot.SetActive(false);
                SetCoinPanelActive(false);
                if (entranceStarted && guide != null) guide.WalkOut(guideStart.anchoredPosition);
            }
            return !entranceStarted || guide == null || guide.HasExited || !guide.gameObject.activeInHierarchy;
        }

        private void OnDisable()
        {
            session?.Close();
            gameManager?.ReleaseIntermissionGate(this);
            if (floorRoot != null) floorRoot.SetActive(false);
            SetCoinPanelActive(false);
            if (guide != null) guide.gameObject.SetActive(false);
        }

        // The coin panel sits on the wall in front of the guide, outside the offers root, so it is
        // shown and hidden here together with the wall cards.
        private void SetCoinPanelActive(bool active)
        {
            if (coinPanel != null) coinPanel.gameObject.SetActive(active);
        }
    }
}
