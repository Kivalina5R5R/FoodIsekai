using System;
using System.IO;
using System.Reflection;
using FoodIsekaiZ.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace FoodIsekaiZ.Display.Editor
{
    // Exercises the purchase timeline and renders its stages without touching the open scene.
    public static class SmallPerkPurchaseVerification
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private const string Output = "Library/SmallPerkPurchaseVerification";

        [MenuItem("Food Isekai/Verify Small Perk Purchase")]
        public static void Verify()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            Directory.CreateDirectory(Output);
            Scene preview = default;
            try
            {
                preview = EditorSceneManager.OpenPreviewScene("Assets/Scenes/FoodIsekai.unity");
                PerkShopPresentation shop = Find<PerkShopPresentation>(preview);
                var presentation = shop.GetComponent<SmallPerkPurchasePresentation>();
                if (presentation == null || Get(shop, "smallPurchases") != presentation)
                    throw new InvalidOperationException("Small purchase presentation is not connected to the shop.");
                Canvas canvas = (Canvas)Get(presentation, "wallCanvas");
                var soundPlayer = (FoodIsekaiZ.Audio.GameSoundPlayer)Get(presentation, "soundPlayer");
                Check(soundPlayer != null && (FoodIsekaiZ.Audio.GameSoundPlayer)Get(shop, "soundPlayer") == soundPlayer, "Shop and animation share the scene sound player.");
                var clips = (AudioClip[])Get(soundPlayer, "clips");
                Check(clips.Length == Enum.GetValues(typeof(FoodIsekaiZ.Audio.GameSoundCue)).Length, "Every sound cue has a serialized slot.");
                for (int cue = (int)FoodIsekaiZ.Audio.GameSoundCue.PerkReveal; cue < clips.Length; cue++)
                {
                    Check(clips[cue] != null && clips[cue].name == ((FoodIsekaiZ.Audio.GameSoundCue)cue).ToString(), "Perk sound is assigned to its matching cue.");
                    Check(soundPlayer.TryPlay((FoodIsekaiZ.Audio.GameSoundCue)cue, true), "The scene sound player accepts each perk cue for playback.");
                    soundPlayer.StopCue((FoodIsekaiZ.Audio.GameSoundCue)cue);
                }
                Camera camera = null;
                foreach (GameObject root in preview.GetRootGameObjects())
                    foreach (Camera candidate in root.GetComponentsInChildren<Camera>(true))
                        if (candidate.name == "SideCamera") camera = candidate;
                if (camera == null) throw new InvalidOperationException("Wall camera is missing.");

                shop.enabled = false;
                Transform branch = shop.transform;
                while (branch != canvas.transform)
                {
                    Transform parent = branch.parent;
                    foreach (Transform sibling in parent)
                        if (sibling != branch && sibling.name != "Background") sibling.gameObject.SetActive(false);
                    parent.gameObject.SetActive(true);
                    branch = parent;
                }
                shop.gameObject.SetActive(true);
                ((GameObject)Get(shop, "offersRoot")).SetActive(true);
                var guide = (NpcGuidePresentation)Get(shop, "guide");
                guide.gameObject.SetActive(false);
                var slots = (PerkCardSlot[])Get(shop, "slots");
                var catalog = (PerkCatalog)Get(shop, "catalog");
                var offers = catalog.GetOffers(false);
                for (int i = 0; i < slots.Length; i++) slots[i].SetOffer(offers[i], catalog.GetPrefab(offers[i].Id), false);
                Canvas.ForceUpdateCanvases();
                Vector3 sourcePosition = slots[0].transform.localPosition;
                Vector3 sourceScale = slots[0].transform.localScale;
                Transform sourceParent = slots[0].transform.parent;
                Render(camera, preview, "01-Offers");
                presentation.Play(slots[0]);
                presentation.Play(slots[1]);
                var phasePanels = Find<MealPhasePanels>(preview);
                var outsideShop = new GameObject("Purchase visibility regression probe", typeof(RectTransform));
                outsideShop.transform.SetParent(canvas.transform, false);
                typeof(MealPhasePanels).GetMethod("HideOtherUi", Private).Invoke(phasePanels, new object[] { shop.gameObject });
                Check(!outsideShop.activeSelf, "The old canvas-sibling placement reproduces the disappearing visual.");
                UnityEngine.Object.DestroyImmediate(outsideShop);
                var purchaseRoot = shop.transform.Find("Small Perk Purchase");
                Check(purchaseRoot != null && purchaseRoot.gameObject.activeInHierarchy,
                    "Purchase visual survives the break panel UI visibility refresh.");
                Check(presentation.IsPlaying && !slots[0].gameObject.activeSelf && !slots[1].gameObject.activeSelf,
                    "Both bought originals are hidden while their copies are queued.");
                for (int sample = 0; sample < 25; sample++)
                {
                    Advance(presentation, (float)Get(presentation, "flightSeconds") / 50f + .00001f);
                    if (sample == 6) Render(camera, preview, "02-Inward-Flip"); if (sample == 14 && (float)Get(presentation, "elapsed") < .6f) { Check(purchaseRoot.Find("Card Back").gameObject.activeSelf, "The reverse face appears while the card turns away."); Render(camera, preview, "02-Reverse-Face"); }
                    if (sample == 10) { Render(camera, preview, "02b-Enlarged-Hover"); if ((float)Get(presentation, "elapsed") > .8f) Check(purchaseRoot.TransformVector(Vector3.up * ((RectTransform)purchaseRoot).rect.height).magnitude > canvas.transform.TransformVector(Vector3.up * ((RectTransform)canvas.transform).rect.height).magnitude * 1.25f, "The raised card grows beyond the screen before falling."); } if (sample == 21) Render(camera, preview, "02c-Shockwave");
                    Check(Mathf.Abs(purchaseRoot.localScale.x / sourceScale.x - purchaseRoot.localScale.y / sourceScale.y) < .001f, "The card never squashes or stretches on impact."); Check(Vector3.Dot(purchaseRoot.localRotation * Vector3.up, Vector3.up) > .999f,
                        "The flutter never turns the card upside down or pitches it.");
                }
                Render(camera, preview, "02-Spinning");
                for (int sample = 0; sample < 25; sample++)
                {
                    Advance(presentation, (float)Get(presentation, "flightSeconds") / 50f + .00001f);
                    if (sample == 6) Render(camera, preview, "02-Inward-Flip"); if (sample == 14 && (float)Get(presentation, "elapsed") < .6f) { Check(purchaseRoot.Find("Card Back").gameObject.activeSelf, "The reverse face appears while the card turns away."); Render(camera, preview, "02-Reverse-Face"); }
                    if (sample == 10) { Render(camera, preview, "02b-Enlarged-Hover"); if ((float)Get(presentation, "elapsed") > .8f) Check(purchaseRoot.TransformVector(Vector3.up * ((RectTransform)purchaseRoot).rect.height).magnitude > canvas.transform.TransformVector(Vector3.up * ((RectTransform)canvas.transform).rect.height).magnitude * 1.25f, "The raised card grows beyond the screen before falling."); } if (sample == 21) Render(camera, preview, "02c-Shockwave");
                    Check(Mathf.Abs(purchaseRoot.localScale.x / sourceScale.x - purchaseRoot.localScale.y / sourceScale.y) < .001f, "The card never squashes or stretches on impact."); Check(Vector3.Dot(purchaseRoot.localRotation * Vector3.up, Vector3.up) > .999f,
                        "The flutter never turns the card upside down or pitches it.");
                }
                Check(Get(presentation, "phase").ToString() == "Settling", "Flight arrives at the intact landing hold.");
                var current = Get(presentation, "current");
                var moving = (RectTransform)current.GetType().GetProperty("Root").GetValue(current);
                var artworkOpacity = (CanvasGroup)current.GetType().GetProperty("ArtworkOpacity").GetValue(current);
                var absorption = (RectTransform)current.GetType().GetProperty("Absorption").GetValue(current);
                Check(Vector3.Distance(moving.position, canvas.transform.TransformPoint(((RectTransform)canvas.transform).rect.center)) < .01f &&
                    artworkOpacity.alpha == 1f, "The card lands intact at screen center.");
                Check(moving.localScale.y > sourceScale.y * 1.1f, "The landing card is visibly enlarged.");
                Check(moving.GetComponentsInChildren<PerkBurnGraphic>(true).Length == 0, "Purchases no longer create burn masks or flames.");
                Render(camera, preview, "03-Centered-Intact");
                Advance(presentation, .79f);
                Check(Get(presentation, "phase").ToString() == "Settling" && Get(presentation, "gathering") == null,
                    "The intact landing pause precedes energy gathering.");
                Advance(presentation, .07f);
                Check(Get(presentation, "phase").ToString() == "Gathering", "Energy gathering follows the landing hold.");
                var backdrop = (PerkReactionBackdropGraphic)Get(presentation, "backdrop");
                Check(backdrop != null && !backdrop.raycastTarget && backdrop.transform.GetSiblingIndex() < absorption.GetSiblingIndex(),
                    "The food-reaction backdrop sits behind the card without intercepting input.");
                Advance(presentation, .65f);
                Render(camera, preview, "04-Incoming-Energy");
                Advance(presentation, .8f);
                Check(artworkOpacity.alpha == 1f && absorption.localScale == Vector3.one,
                    "The card retains its full size and opacity during gathering.");
                Render(camera, preview, "05-Energy-Masses");
                Check(Get(presentation, "powerPulse") == null, "Energy must gather before the full-screen release.");
                Advance(presentation, (float)Get(presentation, "gatherSeconds") - (float)Get(presentation, "elapsed") + .01f);
                Check(Get(presentation, "phase").ToString() == "Charging" && artworkOpacity.alpha == 1f,
                    "The charged card remains visible until the explosion.");
                Advance(presentation, .4f);
                Check(Get(presentation, "phase").ToString() == "Charging" && Get(presentation, "powerPulse") == null,
                    "The central light visibly holds before releasing.");
                Check(absorption.localScale == Vector3.one && absorption.anchoredPosition.sqrMagnitude > .01f,
                    "The charged card shakes without changing size.");
                var goldFrame = moving.GetComponentInChildren<PerkChargedFrameGraphic>();
                Check(goldFrame != null && (float)Get(goldFrame, "strength") == 1f, "A gold ripple follows the charged frame.");
                Render(camera, preview, "06-Charged-Light");
                Advance(presentation, .41f);
                var power = (PerkPowerGraphic)Get(presentation, "powerPulse");
                Check(power != null && Get(presentation, "phase").ToString() == "Releasing", "The held light releases into a full-screen pulse.");
                float powerWidth = power.transform.TransformVector(Vector3.right * power.rectTransform.rect.width).magnitude;
                float wallWidth = canvas.transform.TransformVector(Vector3.right * ((RectTransform)canvas.transform).rect.width).magnitude;
                Check(Mathf.Abs(powerWidth - wallWidth) < .01f && !power.raycastTarget,
                    "The power effect covers the wall canvas without intercepting input.");
                var floorCanvas = (Canvas)Get(presentation, "floorCanvas");
                var floorBlast = (PerkFloorBlastGraphic)Get(presentation, "floorBlast");
                Check(floorCanvas != null && floorBlast != null && floorBlast.canvas.rootCanvas == floorCanvas && !floorBlast.raycastTarget,
                    "The release bursts through onto the floor canvas without intercepting input.");
                Advance(presentation, .52f);
                Check(artworkOpacity.alpha == 0f, "The card disappears with the explosion, not before it."); Render(camera, preview, "06b-Power-Expanding");
                Advance(presentation, .28f);
                Render(camera, preview, "06c-Power-Fullscreen");
                Check(presentation.IsPlaying, "The shop waits for the power confirmation to finish.");
                for (int step = 0; step < 160 && presentation.IsPlaying; step++) Advance(presentation, .1f);
                Check(!presentation.IsPlaying, "Multiple purchases drain the queue completely.");
                Check(floorCanvas.transform.Find("Perk Floor Blast") == null, "The floor blast is removed after the release.");
                Check(slots[0].transform.localPosition == sourcePosition && slots[0].transform.localScale == sourceScale &&
                    slots[0].transform.parent == sourceParent, "Authored card transform and hierarchy remain unchanged.");
                Render(camera, preview, "07-Finished");
                // Big purchases replay the same timeline in rainbow, on the Big card frame, and linger longer.
                var bigOffers = catalog.GetOffers(true);
                slots[1].SetOffer(bigOffers[0], catalog.GetPrefab(bigOffers[0].Id), true);
                presentation.Play(slots[1], true);
                AdvanceUntil(presentation, "Gathering", 5f);
                Check((bool)Get(Get(presentation, "backdrop"), "rainbow") && (bool)Get(Get(presentation, "gathering"), "rainbow"),
                    "Big purchases use the rainbow backdrop and gathering.");
                AdvanceUntil(presentation, "Releasing", 5f);
                object bigPower = Get(presentation, "powerPulse");
                Check(bigPower != null && (bool)Get(bigPower, "rainbow"), "Big purchases release in rainbow.");
                Advance(presentation, .3f);
                Render(camera, preview, "08-Big-Rainbow-Release");
                for (int step = 0; step < 15; step++) Advance(presentation, .1f);
                Check(presentation.IsPlaying, "Big releases linger longer than Small releases.");
                for (int step = 0; step < 160 && presentation.IsPlaying; step++) Advance(presentation, .1f);
                Check(!presentation.IsPlaying, "The Big purchase completes.");
                presentation.Play(slots[2]);
                presentation.Play(slots[3]);
                Advance(presentation, .4f);
                typeof(SmallPerkPurchasePresentation).GetMethod("OnDisable", Private).Invoke(presentation, null);
                Check(!presentation.IsPlaying, "Closing or disabling the shop clears current and queued effects.");
                Check(shop.transform.Find("Small Perk Purchase") == null, "All temporary visuals are disposed.");
                slots[0].SetOffer(offers[0], catalog.GetPrefab(offers[0].Id), false);
                Check(slots[0].gameObject.activeSelf, "The same slot can reveal an offer in the next shop.");
                File.WriteAllText(Output + "/Result.txt", "PASS: reproduced old visibility failure; purchase stays visible through break UI refresh. Verified queue, center position, 0.85 second intact landing hold, upright inward flip, enlarged landing, food-reaction backdrop behind the card, converging sparkles, rigid charged card, held central light and full-screen release, the longer rainbow Big release, completion, cancellation, cleanup and authored transform preservation.");
            }
            catch (Exception exception)
            {
                File.WriteAllText(Output + "/Result.txt", "FAIL: " + exception);
                Debug.LogException(exception);
            }
            finally
            {
                if (preview.IsValid()) EditorSceneManager.ClosePreviewScene(preview);
            }
        }

        private static void Advance(SmallPerkPurchasePresentation presentation, float seconds) =>
            typeof(SmallPerkPurchasePresentation).GetMethod("Advance", Private).Invoke(presentation, new object[] { seconds });

        // Steps in small increments because each Advance call performs at most one phase change.
        private static void AdvanceUntil(SmallPerkPurchasePresentation presentation, string phase, float limit)
        {
            for (float time = 0f; time < limit && Get(presentation, "phase").ToString() != phase; time += .05f)
                Advance(presentation, .05f);
            Check(Get(presentation, "phase").ToString() == phase, "The purchase reaches " + phase + ".");
        }

        private static object Get(object target, string field) => target.GetType().GetField(field, Private).GetValue(target);

        private static void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        private static T Find<T>(Scene scene) where T : Component
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                T component = root.GetComponentInChildren<T>(true);
                if (component != null) return component;
            }
            throw new InvalidOperationException(typeof(T).Name + " is missing.");
        }

        private static void Render(Camera camera, Scene scene, string name)
        {
            RenderTexture previous = RenderTexture.active;
            var target = new RenderTexture(1536, 405, 24);
            var image = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
            try
            {
                camera.targetTexture = target;
                camera.scene = scene;
                camera.aspect = target.width / (float)target.height;
                Canvas.ForceUpdateCanvases();
                camera.Render();
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
                image.Apply();
                File.WriteAllBytes(Output + "/" + name + ".png", image.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = null;
                RenderTexture.active = previous;
                UnityEngine.Object.DestroyImmediate(image);
                UnityEngine.Object.DestroyImmediate(target);
            }
        }
    }
}
