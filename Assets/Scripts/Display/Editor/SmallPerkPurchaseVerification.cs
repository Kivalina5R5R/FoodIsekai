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
                var masks = (PerkBurnGraphic[])current.GetType().GetProperty("Masks").GetValue(current);
                var fires = (PerkBurnGraphic[])current.GetType().GetProperty("Fires").GetValue(current);
                Check(masks.Length >= 2 && masks.Length <= 3 && fires.Length == masks.Length,
                    "Every purchase has two or three ignition points.");
                Check((Vector2)Get(masks[0], "ignition") == Vector2.one * .5f, "Exactly one ignition starts at the center.");
                for (int edgeIndex = 1; edgeIndex < masks.Length; edgeIndex++)
                {
                    Vector2 edgePoint = (Vector2)Get(masks[edgeIndex], "ignition") - Vector2.one * .5f;
                    Check(Mathf.Max(Mathf.Abs(edgePoint.x), Mathf.Abs(edgePoint.y)) > .4f, "All remaining ignition points lie on the card border.");
                }
                var mask = masks[0];
                Check(Vector3.Distance(moving.position, canvas.transform.TransformPoint(((RectTransform)canvas.transform).rect.center)) < .01f &&
                    (float)Get(mask, "progress") == 0f, "The centered card remains fully intact on its arrival frame.");
                Check(moving.localScale.y > sourceScale.y * 1.1f, "The landing card is visibly enlarged.");
                Render(camera, preview, "03-Centered-Intact");
                for (int i = 0; i < masks.Length; i++)
                {
                    Check((Vector2)Get(masks[i], "ignition") == (Vector2)Get(fires[i], "ignition"),
                        "Each flame follows its own stencil hole.");
                    for (int j = 0; j < i; j++)
                        Check(Vector2.Distance((Vector2)Get(masks[i], "ignition"), (Vector2)Get(masks[j], "ignition")) > .3f,
                            "Ignition points are separated so each hole can be seen.");
                }
                Advance(presentation, 1f / 60f);
                Check(Get(presentation, "phase").ToString() == "Settling" && (float)Get(mask, "progress") == 0f,
                    "The burn cannot start on the frame after impact.");
                Advance(presentation, .18f);
                Render(camera, preview, "03b-Face-On-Impact");
                Advance(presentation, .6f);
                Check(Get(presentation, "phase").ToString() == "Settling" && (float)Get(mask, "progress") == 0f, "Paper remains intact for at least .79 seconds after settling.");
                Render(camera, preview, "03c-Pause-Before-Burn");
                Advance(presentation, .06f);
                Check(Get(presentation, "phase").ToString() == "Burning", "The burn starts after the .85 second hold.");
                Advance(presentation, .9f);
                Render(camera, preview, "04-Multiple-Ignitions");
                Advance(presentation, .8f);
                Render(camera, preview, "05-Burning-With-Ash");
                Check(Get(presentation, "phase").ToString() == "Burning" && (float)Get(mask, "progress") < .5f,
                    "The burn is still in its first half after 1.7 seconds.");
                Advance(presentation, 1.2f);
                Render(camera, preview, "06-Merging-Holes");
                Check(Get(presentation, "powerPulse") == null, "Power confirmation cannot appear before the burn finishes.");
                Advance(presentation, (float)Get(presentation, "burnSeconds") - (float)Get(presentation, "elapsed") + .01f);
                var power = (PerkPowerGraphic)Get(presentation, "powerPulse");
                Check(power != null && Get(presentation, "phase").ToString() == "Embers" &&
                    (float)Get(mask, "progress") == 1f, "Power pulse starts only after the paper has fully burned.");
                float powerWidth = power.transform.TransformVector(Vector3.right * power.rectTransform.rect.width).magnitude;
                float wallWidth = canvas.transform.TransformVector(Vector3.right * ((RectTransform)canvas.transform).rect.width).magnitude;
                Check(Mathf.Abs(powerWidth - wallWidth) < .01f && !power.raycastTarget,
                    "The power effect covers the wall canvas without intercepting input.");
                Advance(presentation, .35f);
                Render(camera, preview, "06b-Power-Expanding");
                Advance(presentation, .45f);
                Render(camera, preview, "06c-Power-Fullscreen");
                Check(presentation.IsPlaying, "The shop waits for the power confirmation to finish.");
                for (int step = 0; step < 160 && presentation.IsPlaying; step++) Advance(presentation, .1f);
                Check(!presentation.IsPlaying, "Multiple purchases drain the queue completely.");
                Check(slots[0].transform.localPosition == sourcePosition && slots[0].transform.localScale == sourceScale &&
                    slots[0].transform.parent == sourceParent, "Authored card transform and hierarchy remain unchanged.");
                Render(camera, preview, "07-Finished");
                presentation.Play(slots[2]);
                presentation.Play(slots[3]);
                Advance(presentation, .4f);
                typeof(SmallPerkPurchasePresentation).GetMethod("OnDisable", Private).Invoke(presentation, null);
                Check(!presentation.IsPlaying, "Closing or disabling the shop clears current and queued effects.");
                Check(shop.transform.Find("Small Perk Purchase") == null, "All temporary visuals are disposed.");
                slots[0].SetOffer(offers[0], catalog.GetPrefab(offers[0].Id), false);
                Check(slots[0].gameObject.activeSelf, "The same slot can reveal an offer in the next shop.");
                File.WriteAllText(Output + "/Result.txt", "PASS: reproduced old visibility failure; purchase stays visible through break UI refresh. Verified queue, center position, 0.85 second intact landing hold, upright inward flip, enlarged landing, two/three separated ignition holes, slower burn and floating ash renders, completion, cancellation, cleanup and authored transform preservation.");
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
