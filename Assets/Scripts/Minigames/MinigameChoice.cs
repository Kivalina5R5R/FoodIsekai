using System.Collections.Generic;
using FoodIsekaiZ.Display;
using FoodIsekaiZ.Localization;
using UnityEngine;
using UnityEngine.UI;

namespace FoodIsekaiZ.Minigames
{
    // The minigame choice after a meal, played like the perk shop and with the shop's own pieces:
    // once the meal transition has opened, golden light bursts in the middle of the wall and the
    // offers come out of it face down, slide to their places and flip over one by one
    // (MinigameCardAnimation); when they have all turned, the perk shop's
    // floor tiles open, each named after a minigame. Standing on a tile for HoldSeconds picks it (any
    // number of players may share one). If nobody picks within ChoiceSeconds one is drawn at random.
    // Then the cards and tiles animate away and IsFinished turns true with Picked set.
    // Runs on unscaled time.
    public sealed class MinigameChoice
    {
        public const float HoldSeconds = 1.5f;
        public const float ChoiceSeconds = 30f;

        private enum Stage { WaitForTransition, Revealing, Choosing, Leaving, Finished }

        // Four perk-sized cards across the wall, in the floor tiles' order, two each side with a gap in
        // the middle so the meal clock at the top centre stays in view.
        private static readonly float[] WallCardX = { -536f, -285f, 285f, 536f };
        private static readonly Vector2 WallCardSize = new Vector2(228f, 328f);

        private readonly MinigameStage stage;
        private readonly IReadOnlyList<MinigamePlayer> players;
        private readonly PerkShopPresentation shop;
        private readonly IReadOnlyList<PerkFloorZone> zones;
        private readonly GameObject coinPanel;
        private readonly int count;
        private readonly float[] held;
        private readonly MinigameCardAnimation[] wallCards;
        // The burst of golden light in the middle of the wall the cards come out of.
        private readonly Image introGlow;
        private float introAge;
        private readonly string[] tileNames;
        private readonly RectTransform wallGroup;
        private readonly bool coinPanelWasActive;
        // The shop's own tile positions, put back when the choice ends.
        private readonly Vector2[] shopTilePositions;
        private Stage step;
        private float choosingLeft;

        public MinigameChoice(MinigameStage stage, IReadOnlyList<MinigamePlayer> players, IReadOnlyList<Minigame> games,
            IReadOnlyList<string> pictures, PerkShopPresentation shop)
        {
            this.stage = stage;
            this.players = players;
            this.shop = shop;
            zones = shop.Zones;
            coinPanel = shop.CoinPanel;
            coinPanelWasActive = coinPanel != null && coinPanel.activeSelf;
            count = Mathf.Min(games.Count, Mathf.Min(WallCardX.Length, zones.Count));
            held = new float[count];
            wallCards = new MinigameCardAnimation[count];
            tileNames = new string[count];
            choosingLeft = ChoiceSeconds;
            shopTilePositions = new Vector2[zones.Count];

            wallGroup = stage.Group("Minigame Choice Wall", stage.Wall);
            wallGroup.gameObject.SetActive(false);
            Sprite star = MinigameArt.Picture(SparkleName);
            if (star != null) introGlow = stage.Shape("Intro Glow", wallGroup, star, RaysGold, Vector2.zero, Vector2.one * 520f);
            for (int i = 0; i < count; i++)
            {
                BuildWallCard(i, games[i], pictures[i]);
                tileNames[i] = Pick(games[i].EnglishTitle, games[i].ThaiTitle);
            }

            step = Stage.WaitForTransition;
        }

        public bool IsFinished => step == Stage.Finished;

        // Seconds left to choose while the cards are up (shown on the meal clock), otherwise zero.
        public float SecondsLeft => step == Stage.Revealing || step == Stage.Choosing ? Mathf.Max(0f, choosingLeft) : 0f;

        // The chosen offer, or -1 until one is picked (or drawn).
        public int Picked { get; private set; } = -1;

        public void Tick(float deltaTime, bool drawNow)
        {
            switch (step)
            {
                case Stage.WaitForTransition:
                    if (shop.MenuTransition != null && shop.MenuTransition.IsVisible) return;
                    wallGroup.gameObject.SetActive(true);
                    step = Stage.Revealing;
                    break;
                case Stage.Revealing:
                    TickRevealing(deltaTime);
                    break;
                case Stage.Choosing:
                    TickChoosing(deltaTime, drawNow);
                    break;
                case Stage.Leaving:
                    TickLeaving();
                    break;
            }
        }

        // Puts the shop's floor back as the shop expects it and removes the wall cards.
        public void End()
        {
            foreach (PerkFloorZone zone in zones)
            {
                if (zone == null) continue;
                zone.SetOccupied(false);
                zone.gameObject.SetActive(false);
            }

            if (shop.FloorRoot != null) shop.FloorRoot.SetActive(false);
            if (coinPanel != null) coinPanel.SetActive(coinPanelWasActive);
            RestoreShopTiles();
            if (wallGroup != null) Object.Destroy(wallGroup.gameObject);
        }

        // A burst of golden light opens in the middle of the wall and fades as the cards slide out.
        // The floor tiles open once every wall card has flipped face up.
        private void TickRevealing(float deltaTime)
        {
            introAge += deltaTime;
            if (introGlow != null)
            {
                float grow = Mathf.Clamp01(introAge / 0.5f);
                float alpha = introAge < 0.35f ? introAge / 0.35f * 0.9f : Mathf.Max(0f, 0.9f * (1f - (introAge - 0.35f) / 1.1f));
                introGlow.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.3f, 1.4f, 1f - (1f - grow) * (1f - grow));
                introGlow.rectTransform.localRotation = Quaternion.Euler(0f, 0f, introAge * 20f);
                Color colour = introGlow.color;
                colour.a = alpha;
                introGlow.color = colour;
            }

            foreach (MinigameCardAnimation card in wallCards)
                if (card != null && !card.HasRevealed) return;
            OpenFloor();
            step = Stage.Choosing;
        }

        private void OpenFloor()
        {
            // The middle of the floor stays empty: no coin panel while choosing a minigame.
            if (coinPanel != null) coinPanel.SetActive(false);
            LineUpTiles();

            for (int i = 0; i < zones.Count; i++)
            {
                if (zones[i] == null) continue;
                zones[i].gameObject.SetActive(i < count);
                if (i < count) zones[i].ShowStatus(tileNames[i], 0f, false, false, false);
            }

            if (shop.FloorRoot != null) shop.FloorRoot.SetActive(true);
        }

        // The shop leaves a gap in the middle of the floor for its coin panel; the minigame tiles sit
        // under their wall cards instead, two each side of the middle: each tile's x is its card's x as
        // a share of the wall's width, squeezed no closer than a tile's own width.
        private void LineUpTiles()
        {
            if (zones.Count < 2 || zones[0] == null) return;
            var firstTile = (RectTransform)zones[0].transform;
            var floor = firstTile.parent as RectTransform;
            if (floor == null || floor.rect.width <= 0f || stage.Wall.rect.width <= 0f) return;
            float tileWidth = firstTile.rect.width * firstTile.localScale.x;
            float scale = floor.rect.width / stage.Wall.rect.width;
            float closest = WallCardX[1] - WallCardX[0];
            if (closest * scale < tileWidth) scale = tileWidth / closest;
            for (int i = 0; i < zones.Count; i++)
            {
                if (zones[i] == null) continue;
                var tile = (RectTransform)zones[i].transform;
                shopTilePositions[i] = tile.anchoredPosition;
                if (i < count) tile.anchoredPosition = new Vector2(WallCardX[i] * scale, tile.anchoredPosition.y);
            }
        }

        private void RestoreShopTiles()
        {
            if (step == Stage.WaitForTransition || step == Stage.Revealing) return;
            for (int i = 0; i < zones.Count; i++)
                if (zones[i] != null) ((RectTransform)zones[i].transform).anchoredPosition = shopTilePositions[i];
        }

        private void TickChoosing(float deltaTime, bool drawNow)
        {
            choosingLeft -= deltaTime;
            for (int i = 0; i < count; i++)
            {
                bool occupied = false;
                foreach (MinigamePlayer player in players)
                    if (player.IsPresent && zones[i].Contains(player.WorldPosition)) occupied = true;

                held[i] = occupied ? held[i] + Mathf.Min(deltaTime, 0.1f) : 0f;
                float progress = Mathf.Clamp01(held[i] / HoldSeconds);
                zones[i].SetOccupied(occupied);
                zones[i].ShowStatus(tileNames[i], progress, false, false, occupied);
                if (Picked < 0 && progress >= 1f) Picked = i;
            }

            if (Picked < 0 && (choosingLeft <= 0f || drawNow))
            {
                Picked = Random.Range(0, count);
                Debug.Log($"[Minigames] Nobody picked in time: drew offer {Picked + 1}");
            }

            if (Picked < 0) return;
            Leave();
        }

        // The other cards are covered again and sink away at once; the chosen card spins to the middle
        // of the wall, bursts with light and stars, shows off for a moment, then is covered and sinks too.
        private void Leave()
        {
            for (int i = 0; i < count; i++)
            {
                zones[i].SetOccupied(false);
                zones[i].Hide();
                if (i == Picked) wallCards[i].PlayChosen();
                else wallCards[i].Hide();
            }

            step = Stage.Leaving;
        }

        private void TickLeaving()
        {
            for (int i = 0; i < count; i++)
                if (!zones[i].IsHidden || !wallCards[i].IsHidden) return;
            step = Stage.Finished;
        }

        private void BuildWallCard(int index, Minigame game, string picture)
        {
            Vector2 centre = new Vector2(WallCardX[index], 0f);
            Vector2 size = WallCardSize;
            // The card sits in its own slot so the perk card animation scales it about the slot's centre.
            RectTransform slot = stage.Group("Slot " + (index + 1), wallGroup);
            slot.anchorMin = slot.anchorMax = new Vector2(0.5f, 0.5f);
            slot.sizeDelta = size;
            slot.anchoredPosition = centre;
            Image card = stage.Shape("Card", slot, MinigameArt.Square, MinigameArt.Cream, Vector2.zero, size);
            MinigameArt.UsePicture(card, "ChoiceCard");
            card.preserveAspect = false;

            Image art = stage.Shape("Picture", card.rectTransform, MinigameArt.Hexagon, MinigameArt.Copper,
                new Vector2(0f, size.y * 0.14f), new Vector2(size.x * 0.72f, size.y * 0.36f));
            if (!string.IsNullOrEmpty(picture)) MinigameArt.UsePicture(art, picture);
            stage.Label("Name", card.rectTransform, Pick(game.EnglishTitle, game.ThaiTitle), 24f, MinigameArt.Brown,
                new Vector2(0f, -size.y * 0.13f), new Vector2(size.x * 0.84f, size.y * 0.12f));
            stage.Label("Rule", card.rectTransform, Pick(game.EnglishRule, game.ThaiRule), 16f, MinigameArt.Copper,
                new Vector2(0f, -size.y * 0.3f), new Vector2(size.x * 0.8f, size.y * 0.2f));

            // The face-down card drawn over the face until it flips: its own painted back (a golden
            // serving dome on a burgundy tablecloth), steaming before the flip like a dish being served.
            // Light rays and a soft glow sit behind; a white flash covers it.
            Sprite star = MinigameArt.Picture(SparkleName);
            Image rays = star != null ? stage.Shape("Rays", slot, star, RaysGold, Vector2.zero, Vector2.one * size.y * 2.2f) : null;
            Image glow = star != null ? stage.Shape("Back Glow", slot, star, RaysGold, Vector2.zero, Vector2.one * size.y * 1.6f) : null;
            rays?.transform.SetAsFirstSibling();
            glow?.transform.SetSiblingIndex(1);
            Image back = stage.Shape("Back", slot, MinigameArt.Square, Color.white, Vector2.zero, size);
            MinigameArt.UsePicture(back, "ChoiceCardBack");
            back.preserveAspect = false;
            // A soft white burst of light rather than a hard white box over the card.
            Image flash = star != null
                ? stage.Shape("Flash", slot, star, new Color(1f, 1f, 1f, 0f), Vector2.zero, Vector2.one * size.y * 1.5f)
                : stage.Shape("Flash", slot, MinigameArt.Square, new Color(1f, 1f, 1f, 0f), Vector2.zero, size);

            wallCards[index] = slot.gameObject.AddComponent<MinigameCardAnimation>();
            wallCards[index].Setup(card.rectTransform, back, glow, flash, rays, star, index);
        }

        private const string SparkleName = "sparkle-star";
        private static readonly Color RaysGold = new Color(1f, 0.82f, 0.4f, 0f);

        private static string Pick(string english, string thai)
        {
            return GameLanguage.Pick(english, thai);
        }
    }
}
