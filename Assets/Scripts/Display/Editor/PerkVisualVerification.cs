using System;
using System.Collections.Generic;
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
    // Renders saved scene assets in a disposable preview; the open scene is never saved or edited.
    [InitializeOnLoad]
    public static class PerkVisualVerification
    {
        static PerkVisualVerification() => EditorApplication.delayCall += RunRequested;

        private static void RunRequested()
        {
            const string request = "Temp/PerkVisualVerification.request";
            if (!File.Exists(request) || EditorApplication.isPlayingOrWillChangePlaymode) return;
            File.Delete(request);
            Verify();
        }

        [MenuItem("Food Isekai/Verify Perk Visuals")]
        public static void Verify()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            Scene preview = default;
            UnityEngine.Random.State randomState = UnityEngine.Random.state;
            try
            {
                preview = EditorSceneManager.OpenPreviewScene("Assets/Scenes/FoodIsekai.unity");
                FoodIsekaiZGameManager game = Find<FoodIsekaiZGameManager>(preview);
                FoodIsekaiZSideDisplayLayout display = Find<FoodIsekaiZSideDisplayLayout>(preview);
                MealFoodDisplay meals = Find<MealFoodDisplay>(preview);
                Camera wallCamera = FindCamera(preview, "SideCamera");
                Camera floorCamera = FindCamera(preview, "FloorCamera");
                Set(game, "mealWavePhase", MealWavePhase.Active);
                Set(game, "currentWaveIndex", 2);
                Set(game, "waitForStartup", false);
                game.Perks.RecordPurchase(new PerkPurchase(new PerkOffer("Perk_Big_Sky", 150), 1, 3));
                PerkManagerMonitor monitor = Find<PerkManagerMonitor>(preview);
                if (monitor.GameManager != game || monitor.ActivePerks.Count != 1)
                    throw new InvalidOperationException("The hierarchy monitor is not bound to the live game ownership.");
                game.Perks.RecordPurchase(new PerkPurchase(new PerkOffer(PerkDefinitions.Tasty, 80), 2, 2));
                if (monitor.ActivePerks.Count != 2 || monitor.ActivePerks[1].PlayerId != 2)
                    throw new InvalidOperationException("The hierarchy monitor did not update after a purchase.");
                Invoke(game, "SelectSpecialMenu");
                Canvas canvas = display.SideCanvas;
                var visibleBranches = new List<Transform>();
                foreach (Transform child in canvas.GetComponentsInChildren<Transform>(true))
                    if (child.name == "Background" || child.name.StartsWith("CustomerPanel")) visibleBranches.Add(child);
                foreach (Transform child in canvas.GetComponentsInChildren<Transform>(true))
                {
                    bool show = child == canvas.transform;
                    bool ancestor = show;
                    foreach (Transform branch in visibleBranches)
                    {
                        show |= child.IsChildOf(branch) || branch.IsChildOf(child);
                        ancestor |= branch.IsChildOf(child);
                    }
                    // Keep authored child visibility, including the disabled legacy timer graphics.
                    if (!show || ancestor) child.gameObject.SetActive(show);
                }
                foreach (Transform branch in visibleBranches)
                {
                    for (Transform ancestor = branch; ancestor != null; ancestor = ancestor.parent)
                        ancestor.gameObject.SetActive(true);
                    foreach (CanvasGroup group in branch.GetComponentsInChildren<CanvasGroup>(true)) group.alpha = 1;
                }
                for (int i = 0; i < 6; i++)
                {
                    ArenaSlot2D slot = game.GetCustomerSlot(i);
                    slot.ConfigureCustomer((FoodType)(i % 5 + 1), 20, 20,
                        i == 1 ? FoodType.Food4 : i == 2 ? FoodType.Food3 : FoodType.None, i == 3, i == 5);
                    slot.StartCustomerTimer();
                }
                Invoke(display, "CacheManualDisplay");
                Invoke(display, "UpdateRealtimeText");
                foreach (GameObject root in preview.GetRootGameObjects())
                {
                    foreach (MealMenuTransition transition in root.GetComponentsInChildren<MealMenuTransition>(true))
                        transition.gameObject.SetActive(false);
                    foreach (Transform item in root.GetComponentsInChildren<Transform>(true))
                        if (item.name == "Wall Intro Overlay") item.gameObject.SetActive(false);
                }
                Canvas.ForceUpdateCanvases();
                Render(wallCamera, preview, 2048, 540, "Temp/PerkOrdersPreview.png");
                VerifyPairedTransitions(game, display, wallCamera, preview);

                Invoke(meals, "RefreshStations");
                var stationTransitions = (MealFoodSwapEffect[])Get(meals, "stationTransitions");
                for (int i = 0; i < stationTransitions.Length; i++)
                    stationTransitions[i].ShowImmediately(meals.GetSprite((FoodType)(i + 1)));
                foreach (GameObject root in preview.GetRootGameObjects())
                    foreach (Transform item in root.GetComponentsInChildren<Transform>(true))
                        if (item.name == "ReadyPhase") item.gameObject.SetActive(false);
                foreach (GameObject root in preview.GetRootGameObjects())
                    foreach (SpecialMenuFrame frame in root.GetComponentsInChildren<SpecialMenuFrame>(true))
                    {
                        for (Transform ancestor = frame.transform; ancestor != null; ancestor = ancestor.parent)
                            ancestor.gameObject.SetActive(true);
                        Invoke(frame, "Update");
                    }
                GameObject platePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/Charactor/PlayerPlate.prefab");
                for (int count = 1; count <= 3; count++)
                {
                    GameObject plate = (GameObject)PrefabUtility.InstantiatePrefab(platePrefab, preview);
                    FoodIsekaiZPlayerState player = plate.GetComponent<FoodIsekaiZPlayerState>();
                    Set(player, "trackedPlayer", null);
                    Set(player, "fallbackPlayerId", count);
                    player.SetFoodCapacity(3);
                    player.TryPickFood(FoodType.Food1);
                    if (count >= 2) player.TryPickFood(FoodType.Food3);
                    if (count >= 3) player.TryPickFood(FoodType.Food4);
                    PlayerPlateDisplay plateDisplay = plate.GetComponent<PlayerPlateDisplay>();
                    plateDisplay.BindMealDisplay(meals);
                    plate.transform.position = new Vector3((count - 2) * 3.5f, .12f, 0);
                    plate.transform.Find("Plate Canvas").gameObject.SetActive(true);
                    Invoke(plateDisplay, "LateUpdate");
                    var compact = (Image[])Get(plateDisplay, "multiFoodImages");
                    if (compact == null || compact.Length != 3)
                        throw new InvalidOperationException("The compact plate needs left, right and bottom images.");
                    var variants = (Image[])Get(plateDisplay, "authoredMultiFoodImages");
                    if (variants == null || variants.Length != 45)
                        throw new InvalidOperationException("All 15 menus need an authored image in each compact slot.");
                    for (int i = 0; i < compact.Length; i++)
                    {
                        int visible = 0;
                        foreach (Image variant in variants)
                            if (variant.transform.parent == compact[i].transform && variant.enabled) visible++;
                        if (compact[i].enabled || visible != (count > 1 && i < count ? 1 : 0))
                            throw new InvalidOperationException("Incorrect compact slot visibility for " + count + " dishes.");
                    }
                    if (count == 3)
                    {
                        player.TryConsumeFood(FoodType.Food3);
                        player.TryConsumeFood(FoodType.Food4);
                        Invoke(plateDisplay, "LateUpdate");
                        foreach (Image image in variants)
                            if (image.enabled) throw new InvalidOperationException("Compact food remained visible after returning to one dish.");
                        player.TryPickFood(FoodType.Food3);
                        player.TryPickFood(FoodType.Food4);
                        Invoke(plateDisplay, "LateUpdate");
                    }
                    foreach (InventoryAppearEffect effect in plate.GetComponentsInChildren<InventoryAppearEffect>(true))
                    {
                        Set(effect, "playing", false);
                        effect.GetComponent<Graphic>()?.SetVerticesDirty();
                    }
                }
                Canvas.ForceUpdateCanvases();
                Render(floorCamera, preview, 1472, 704, "Temp/PerkFloorPreview.png");
                File.WriteAllText("Temp/PerkVisualVerification.txt",
                    "PASS: live perk monitor reflects purchases; paired-order left/right/repeated-food deliveries, transition meshes, particles, interruption and reset verified; one/two/three-dish inventory visibility verified; authored layouts rendered. Live scene unchanged.");
            }
            catch (Exception exception)
            {
                File.WriteAllText("Temp/PerkVisualVerification.txt", "FAIL: " + exception);
                Debug.LogException(exception);
            }
            finally
            {
                UnityEngine.Random.state = randomState;
                if (preview.IsValid()) EditorSceneManager.ClosePreviewScene(preview);
            }
        }

        private static void VerifyPairedTransitions(FoodIsekaiZGameManager game,
            FoodIsekaiZSideDisplayLayout display, Camera camera, Scene preview)
        {
            var presentations = (PairedOrderPresentation[])Get(display, "pairedPresentations");
            var firstImages = (Image[])Get(display, "pairedFirstImages");
            var secondImages = (Image[])Get(display, "pairedSecondImages");
            var singleImages = (Image[])Get(display, "customerStatusImages");
            for (int i = 0; i < presentations.Length; i++)
            {
                if (presentations[i] == null || presentations[i].GetComponentsInChildren<OrderCardMeshEffect>(true).Length != 15)
                    throw new InvalidOperationException("Every customer panel needs its paired transition and all card mesh effects.");
                var first = (RectTransform)Get(presentations[i], "firstCard");
                var second = (RectTransform)Get(presentations[i], "secondCard");
                if (second.anchoredPosition.x - first.anchoredPosition.x < 104f)
                    throw new InvalidOperationException("Paired menu cards are too close together.");
            }

            // Exercise left-first, right-first and identical-dish orders together.
            for (int i = 0; i < 3; i++)
            {
                game.GetCustomerSlot(i).ConfigureCustomer(FoodType.Food1, 20, 40,
                    i == 2 ? FoodType.Food1 : FoodType.Food4);
                game.GetCustomerSlot(i).StartCustomerTimer();
            }
            Invoke(display, "UpdateRealtimeText");
            Render(camera, preview, 2048, 540, "Temp/PairedOrdersBefore.png");
            for (int i = 0; i < 3; i++)
            {
                ArenaSlot2D slot = game.GetCustomerSlot(i);
                float timer = slot.StateTimeNormalized;
                if (!slot.TryServeFood(i == 1 ? FoodType.Food4 : FoodType.Food1, 3) || slot.StateTimeNormalized != timer)
                    throw new InvalidOperationException("Partial delivery must preserve the waiting timer.");
            }
            Invoke(display, "UpdateRealtimeText");
            for (int i = 0; i < 3; i++)
            {
                if (!presentations[i].IsTransitioning || !firstImages[i].enabled || !secondImages[i].enabled)
                    throw new InvalidOperationException("Partial delivery must retain both outgoing cards during the transition.");
                int served = (int)Get(presentations[i], "servedCard");
                if (served != (i == 1 ? 1 : 0)) throw new InvalidOperationException("The wrong dish was animated away.");
                var burst = (CustomerPanelSuccessParticles)Get(presentations[i], served == 0 ? "firstParticles" : "secondParticles");
                if (!burst.IsPlaying) throw new InvalidOperationException("Partial delivery did not play its sparkle burst.");
            }

            foreach (float time in new[] { 0.12f, 0.3f, 0.5f })
            {
                for (int i = 0; i < 3; i++)
                {
                    Set(presentations[i], "elapsed", time);
                    Invoke(presentations[i], "RefreshMeshes");
                    foreach (CustomerPanelSuccessParticles burst in presentations[i].GetComponentsInChildren<CustomerPanelSuccessParticles>(true))
                    {
                        Set(burst, "elapsed", time);
                        burst.SetVerticesDirty();
                    }
                }
                Render(camera, preview, 2048, 540, $"Temp/PairedOrdersDuring{Mathf.RoundToInt(time * 100):00}.png");
            }

            for (int i = 0; i < 3; i++) Set(presentations[i], "elapsed", 0.6f);
            Invoke(display, "UpdateRealtimeText");
            for (int i = 0; i < 3; i++)
                if (presentations[i].IsTransitioning || firstImages[i].enabled || secondImages[i].enabled || !singleImages[i].enabled)
                    throw new InvalidOperationException("The finished transition must show only the remaining single dish.");
            Render(camera, preview, 2048, 540, "Temp/PairedOrdersAfter.png");

            ArenaSlot2D fastSlot = game.GetCustomerSlot(0);
            fastSlot.ConfigureCustomer(FoodType.Food1, 20, 40, FoodType.Food4);
            fastSlot.StartCustomerTimer();
            Invoke(display, "UpdateRealtimeText");
            fastSlot.TryServeFood(FoodType.Food1, 3);
            Invoke(display, "UpdateRealtimeText");
            fastSlot.TryServeFood(FoodType.Food4, 3);
            Invoke(display, "UpdateRealtimeText");
            if (presentations[0].IsTransitioning || firstImages[0].enabled || secondImages[0].enabled || !singleImages[0].enabled)
                throw new InvalidOperationException("A rapid second delivery must cancel the partial transition cleanly.");

            fastSlot.ConfigureCustomer(FoodType.Food1, 20, 40, FoodType.Food4);
            fastSlot.StartCustomerTimer();
            Invoke(display, "UpdateRealtimeText");
            fastSlot.TryServeFood(FoodType.Food1, 3);
            Invoke(display, "UpdateRealtimeText");
            presentations[0].gameObject.SetActive(false);
            // Preview scenes do not dispatch Play Mode lifecycle messages for ordinary MonoBehaviours.
            Invoke(presentations[0], "OnDisable");
            if (presentations[0].IsTransitioning) throw new InvalidOperationException("Hiding a panel must clear the transition.");
            presentations[0].gameObject.SetActive(true);
            Invoke(display, "UpdateRealtimeText");
            if (presentations[0].IsTransitioning) throw new InvalidOperationException("A reused panel must not replay a stale delivery.");
        }

        private static T Find<T>(Scene scene) where T : Component
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                T component = root.GetComponentInChildren<T>(true);
                if (component != null) return component;
            }
            throw new InvalidOperationException(typeof(T).Name + " is missing from saved scene.");
        }

        private static Camera FindCamera(Scene scene, string name)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
                foreach (Camera camera in root.GetComponentsInChildren<Camera>(true))
                    if (camera.name == name) return camera;
            throw new InvalidOperationException(name + " is missing.");
        }

        private static void Render(Camera camera, Scene scene, int width, int height, string path)
        {
            RenderTexture previous = RenderTexture.active;
            var target = new RenderTexture(width, height, 24);
            var image = new Texture2D(width, height, TextureFormat.RGB24, false);
            try
            {
                camera.targetTexture = target;
                camera.scene = scene;
                camera.aspect = width / (float)height;
                Canvas.ForceUpdateCanvases();
                camera.Render();
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                image.Apply();
                File.WriteAllBytes(path, image.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = null;
                RenderTexture.active = previous;
                UnityEngine.Object.DestroyImmediate(image);
                UnityEngine.Object.DestroyImmediate(target);
            }
        }

        private static void Set(object target, string field, object value) =>
            target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
        private static object Get(object target, string field) =>
            target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
        private static void Invoke(object target, string method) =>
            target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, null);
    }
}
