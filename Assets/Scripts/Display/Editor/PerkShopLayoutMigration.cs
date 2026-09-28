using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

namespace FoodIsekaiZ.Display.Editor
{
    // Updates a stale open scene once, without reloading or saving unrelated edits.
    [InitializeOnLoad]
    public static class PerkShopLayoutMigration
    {
        static PerkShopLayoutMigration()
        {
            EditorApplication.delayCall += ApplyToOpenScenes;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            EditorSceneManager.sceneOpened += OnSceneOpened;
        }

        [MenuItem("Food Isekai/Update Open Perk Shop Layout")]
        public static void ApplyToOpenScenes()
        {
            if (EditorApplication.isPlaying) return;
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (!scene.isLoaded || scene.path != "Assets/Scenes/FoodIsekai.unity") continue;
                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    UpdateReadyGauges(root);
                    foreach (PerkShopPresentation shop in root.GetComponentsInChildren<PerkShopPresentation>(true))
                        Apply(shop);
                }
            }
        }

        private static void OnSceneOpened(Scene scene, OpenSceneMode mode)
        {
            EditorApplication.delayCall += ApplyToOpenScenes;
        }

        private static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredEditMode || state == PlayModeStateChange.ExitingEditMode)
                ApplyToOpenScenes();
        }

        private static void Apply(PerkShopPresentation shop)
        {
            RemoveLegacyBrake(shop);
            RemoveUpperCountdown(shop);
            AddCardAnimations(shop);
            RefineFloorLabels(shop);
            if (shop.LayoutRevision >= 3)
            {
                PolishShop(shop);
                UpdatePurchaseGauges(shop);
                RestorePrefabDialogue(shop);
                AddVanishParticles(shop);
                return;
            }
            Transform offers = shop.transform.Find("Perk Offers");
            Transform rule = offers != null ? offers.Find("Shop Rule") : null;
            var settings = new SerializedObject(shop);
            var start = settings.FindProperty("guideStart").objectReferenceValue as RectTransform;
            var destination = settings.FindProperty("guideDestination").objectReferenceValue as RectTransform;
            var guide = settings.FindProperty("guide").objectReferenceValue as NpcGuidePresentation;
            var slots = settings.FindProperty("slots");
            if (start == null || destination == null || guide == null || slots.arraySize != 4) return;
            var cards = new RectTransform[4];
            for (int i = 0; i < cards.Length; i++)
            {
                var card = slots.GetArrayElementAtIndex(i).objectReferenceValue as PerkCardSlot;
                if (card == null) return;
                cards[i] = card.transform as RectTransform;
                if (cards[i] == null) return;
            }
            bool compactLayout = rule != null || Mathf.Approximately(cards[0].localScale.x, .68f);
            var floorZones = settings.FindProperty("zones");
            if (floorZones.arraySize != cards.Length) return;
            var zones = new RectTransform[4];
            for (int i = 0; i < zones.Length; i++)
            {
                var zone = floorZones.GetArrayElementAtIndex(i).objectReferenceValue as PerkFloorZone;
                if (zone == null) return;
                zones[i] = zone.transform as RectTransform;
                if (zones[i] == null) return;
            }
            DisplayManager displays = null;
            foreach (GameObject root in shop.gameObject.scene.GetRootGameObjects())
            {
                displays = root.GetComponentInChildren<DisplayManager>(true);
                if (displays != null) break;
            }
            if (displays == null) return;
            var displaySettings = new SerializedObject(displays);
            var wallCamera = displaySettings.FindProperty("sideCamera").objectReferenceValue as Camera;
            var floorCamera = displaySettings.FindProperty("floorCamera").objectReferenceValue as Camera;
            var manager = settings.FindProperty("gameManager").objectReferenceValue;
            if (wallCamera == null || floorCamera == null || manager == null ||
                !wallCamera.orthographic || !floorCamera.orthographic) return;

            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Update perk shop wall layout");
            if (rule != null) Undo.DestroyObjectImmediate(rule.gameObject);
            float[] positions = { -645f, -410f, 410f, 645f };
            for (int i = 0; i < cards.Length; i++)
            {
                RemoveChild(cards[i], "Purchase Status");
                RemoveChild(cards[i], "Hold Progress");
                if (compactLayout)
                {
                    Undo.RecordObject(cards[i], "Position perk card");
                    cards[i].anchoredPosition = new Vector2(positions[i], 0f);
                    cards[i].localScale = Vector3.one * .95f;
                }
                Undo.RecordObject(zones[i], "Align floor purchase zone with card");
                AlignHorizontalCenter(cards[i], zones[i], wallCamera, floorCamera,
                    displays.SideDisplayResolution, displays.FloorDisplayResolution);
            }
            if (compactLayout)
            {
                Undo.RecordObjects(new Object[] { start, destination, guide.transform }, "Match intro Lunar placement");
                start.anchoredPosition = new Vector2(1536f, -217.5f);
                destination.anchoredPosition = new Vector2(-40f, -217.5f);
                var guideRect = (RectTransform)guide.transform;
                guideRect.anchoredPosition = start.anchoredPosition;
                guideRect.localScale = Vector3.one;
                PrefabUtility.RecordPrefabInstancePropertyModifications(guideRect);
            }
            var managerSettings = new SerializedObject(manager);
            managerSettings.FindProperty("intermissionDurationSeconds").floatValue = 30f;
            managerSettings.ApplyModifiedProperties();
            settings.FindProperty("layoutRevision").intValue = 3;
            settings.ApplyModifiedProperties();
            Undo.CollapseUndoOperations(group);
            PolishShop(shop);
            UpdatePurchaseGauges(shop);
            RestorePrefabDialogue(shop);
            AddVanishParticles(shop);
            EditorSceneManager.MarkSceneDirty(shop.gameObject.scene);
            Debug.Log("[PerkShopLayout] Matched floor zones to wall camera framing and set the shop to 30 seconds after Lunar finishes speaking. Other unsaved edits preserved; save the scene to retain this layout.", shop);
        }

        // Bake matching screen fractions using each output's resolution and camera framing.
        // This runs only in the editor; gameplay never overwrites authored transforms.
        private static void AlignHorizontalCenter(RectTransform card, RectTransform zone,
            Camera wallCamera, Camera floorCamera, Vector2Int wallResolution, Vector2Int floorResolution)
        {
            float wallWidth = 2f * wallCamera.orthographicSize * wallResolution.x / wallResolution.y;
            float floorWidth = 2f * floorCamera.orthographicSize * floorResolution.x / floorResolution.y;
            Vector3 cardCenter = card.TransformPoint(card.rect.center);
            float wallX = wallCamera.transform.InverseTransformPoint(cardCenter).x;
            float outputX = wallCamera.rect.x + (0.5f + wallX / wallWidth) * wallCamera.rect.width;
            float floorX = ((outputX - floorCamera.rect.x) / floorCamera.rect.width - 0.5f) * floorWidth;
            Vector3 zoneCenter = zone.TransformPoint(zone.rect.center);
            float currentFloorX = floorCamera.transform.InverseTransformPoint(zoneCenter).x;
            zone.position += floorCamera.transform.right * (floorX - currentFloorX);
        }

        // Remove the obsolete sibling from a scene still open when the old asset was deleted.
        private static void RemoveLegacyBrake(PerkShopPresentation shop)
        {
            Transform legacy = shop.transform.parent != null ? shop.transform.parent.Find("Brake") : null;
            if (legacy == null || legacy.GetComponent<PerkShopPresentation>() != null) return;
            string path = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(legacy.gameObject);
            bool missingPrefab = PrefabUtility.GetPrefabInstanceStatus(legacy.gameObject) == PrefabInstanceStatus.MissingAsset;
            if (path != "Assets/Prefab/UI/Brake.prefab" && !missingPrefab) return;
            Undo.DestroyObjectImmediate(legacy.gameObject);
            EditorSceneManager.MarkSceneDirty(shop.gameObject.scene);
        }

        // Author missing effects into an already-open scene without replacing its layout.
        private static void AddCardAnimations(PerkShopPresentation shop)
        {
            foreach (PerkCardSlot slot in shop.GetComponentsInChildren<PerkCardSlot>(true))
            {
                if (slot.GetComponent<PerkCardAnimation>() != null) continue;
                var starsObject = new GameObject("Frame Stars", typeof(RectTransform), typeof(CanvasRenderer));
                Undo.RegisterCreatedObjectUndo(starsObject, "Add perk frame stars");
                starsObject.layer = slot.gameObject.layer;
                starsObject.transform.SetParent(slot.transform, false);
                var rect = (RectTransform)starsObject.transform;
                rect.sizeDelta = new Vector2(232f, 345f);
                var stars = Undo.AddComponent<PerkCardSparkles>(starsObject);
                stars.raycastTarget = false;
                var animation = Undo.AddComponent<PerkCardAnimation>(slot.gameObject);
                var settings = new SerializedObject(animation);
                settings.FindProperty("sparkles").objectReferenceValue = stars;
                settings.ApplyModifiedProperties();
                EditorSceneManager.MarkSceneDirty(shop.gameObject.scene);
            }
        }

        // Presence of the old number marks the previous floor layout, preserving later edits.
        private static void RefineFloorLabels(PerkShopPresentation shop)
        {
            var settings = new SerializedObject(shop);
            var zones = settings.FindProperty("zones");
            for (int i = 0; i < zones.arraySize; i++)
            {
                var zone = zones.GetArrayElementAtIndex(i).objectReferenceValue as PerkFloorZone;
                if (zone == null) continue;
                Transform number = zone.transform.Find("Choice Number");
                if (number == null) continue;
                var rect = (RectTransform)zone.transform;
                var label = zone.transform.Find("Purchase Status")?.GetComponent<TMP_Text>();
                if (label == null) continue;
                Undo.DestroyObjectImmediate(number.gameObject);
                Undo.RecordObjects(new Object[] { rect, label, label.rectTransform }, "Refine perk floor labels");
                rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, 380f);
                label.rectTransform.anchoredPosition = Vector2.zero;
                label.rectTransform.sizeDelta = new Vector2(430f, 150f);
                label.text = "NOT PURCHASED";
                label.enableAutoSizing = false;
                label.fontSize = 44f;
                label.fontSizeMin = 44f;
                label.fontSizeMax = 44f;
                EditorSceneManager.MarkSceneDirty(shop.gameObject.scene);
            }
        }

        private static void PolishShop(PerkShopPresentation shop)
        {
            if (shop.LayoutRevision >= 4) return;
            var settings = new SerializedObject(shop);
            var destination = settings.FindProperty("guideDestination").objectReferenceValue as RectTransform;
            if (destination == null) return;
            Undo.RecordObject(destination, "Balance Lunar spacing");
            destination.anchoredPosition = new Vector2(-8f, destination.anchoredPosition.y);
            var zones = settings.FindProperty("zones");
            for (int i = 0; i < zones.arraySize; i++)
            {
                var zone = zones.GetArrayElementAtIndex(i).objectReferenceValue as PerkFloorZone;
                if (zone == null) continue;
                var animation = zone.GetComponent<PerkCardAnimation>();
                if (animation == null) animation = Undo.AddComponent<PerkCardAnimation>(zone.gameObject);
                var animationSettings = new SerializedObject(animation);
                animationSettings.FindProperty("idleMotion").boolValue = false;
                animationSettings.ApplyModifiedProperties();
                var label = zone.transform.Find("Purchase Status")?.GetComponent<TMP_Text>();
                if (label != null)
                {
                    Undo.RecordObjects(new Object[] { label, label.rectTransform }, "Inset purchase label");
                    label.rectTransform.sizeDelta = new Vector2(360f, 130f);
                    label.fontSize = label.fontSizeMin = label.fontSizeMax = 34f;
                    label.margin = new Vector4(12f, 8f, 12f, 8f);
                }
                var gauge = zone.transform.Find("Hold Progress") as RectTransform;
                if (gauge != null)
                {
                    Undo.RecordObjects(new Object[] { gauge, gauge.gameObject }, "Match Ready gauge layout");
                    gauge.anchoredPosition = new Vector2(0f, -175f);
                    gauge.sizeDelta = new Vector2(346.2f, 51.105713f);
                    gauge.gameObject.SetActive(false);
                }
            }
            settings.FindProperty("layoutRevision").intValue = 4;
            settings.ApplyModifiedProperties();
            EditorSceneManager.MarkSceneDirty(shop.gameObject.scene);
        }

        private static void UpdatePurchaseGauges(PerkShopPresentation shop)
        {
            if (shop.LayoutRevision >= 5) return;
            DisplayManager displays = null;
            foreach (GameObject root in shop.gameObject.scene.GetRootGameObjects())
            {
                displays = root.GetComponentInChildren<DisplayManager>(true);
                if (displays != null) break;
            }
            if (displays == null) return;
            var outputs = new SerializedObject(displays);
            var wallCamera = outputs.FindProperty("sideCamera").objectReferenceValue as Camera;
            var floorCamera = outputs.FindProperty("floorCamera").objectReferenceValue as Camera;
            if (wallCamera == null || floorCamera == null) return;
            var settings = new SerializedObject(shop);
            var slots = settings.FindProperty("slots");
            var zones = settings.FindProperty("zones");
            for (int i = 0; i < zones.arraySize; i++)
            {
                var zone = zones.GetArrayElementAtIndex(i).objectReferenceValue as PerkFloorZone;
                if (zone == null) continue;
                var zoneSettings = new SerializedObject(zone);
                var gauge = zoneSettings.FindProperty("progress").objectReferenceValue;
                if (gauge != null)
                {
                    var colors = new SerializedObject(gauge);
                    colors.FindProperty("amber").colorValue = new Color(.22f, .66f, .3f, 1f);
                    colors.FindProperty("magic").colorValue = new Color(.55f, .9f, .55f, 1f);
                    colors.FindProperty("confirmed").colorValue = new Color(.22f, .66f, .3f, 1f);
                    colors.ApplyModifiedProperties();
                }
                if (i != 1 && i != 2) continue;
                var slot = slots.GetArrayElementAtIndex(i).objectReferenceValue as PerkCardSlot;
                if (slot == null) continue;
                var rect = (RectTransform)slot.transform;
                Undo.RecordObjects(new Object[] { rect, zone.transform }, "Move inner perks inward");
                rect.anchoredPosition = new Vector2(i == 1 ? -394f : 394f, rect.anchoredPosition.y);
                AlignHorizontalCenter(rect, (RectTransform)zone.transform, wallCamera, floorCamera,
                    displays.SideDisplayResolution, displays.FloorDisplayResolution);
            }
            settings.FindProperty("layoutRevision").intValue = 5;
            settings.ApplyModifiedProperties();
            EditorSceneManager.MarkSceneDirty(shop.gameObject.scene);
        }

        private static void RestorePrefabDialogue(PerkShopPresentation shop)
        {
            if (shop.LayoutRevision >= 6) return;
            var settings = new SerializedObject(shop);
            var guide = settings.FindProperty("guide").objectReferenceValue as NpcGuidePresentation;
            var bubble = guide != null ? guide.transform.Find("TextPerk") as RectTransform : null;
            if (bubble == null) return;
            // Remove only the old shop X override; inherit future prefab edits unchanged.
            var bubbleSettings = new SerializedObject(bubble);
            var horizontalPosition = bubbleSettings.FindProperty("m_AnchoredPosition.x");
            if (horizontalPosition.prefabOverride)
                PrefabUtility.RevertPropertyOverride(horizontalPosition, InteractionMode.AutomatedAction);
            var destination = settings.FindProperty("guideDestination").objectReferenceValue as RectTransform;
            if (destination != null)
            {
                Undo.RecordObject(destination, "Fit Lunar with authored dialogue");
                destination.anchoredPosition = new Vector2(4f, destination.anchoredPosition.y);
            }
            settings.FindProperty("layoutRevision").intValue = 6;
            settings.ApplyModifiedProperties();
            EditorSceneManager.MarkSceneDirty(shop.gameObject.scene);
        }

        private static void UpdateReadyGauges(GameObject root)
        {
            foreach (PlayerReadyZone zone in root.GetComponentsInChildren<PlayerReadyZone>(true))
            {
                var zoneSettings = new SerializedObject(zone);
                var gauge = zoneSettings.FindProperty("confirmationGauge").objectReferenceValue;
                if (gauge == null) continue;
                var colors = new SerializedObject(gauge);
                // Only update the previous authored palette, leaving later custom colors intact.
                if (colors.FindProperty("amber").colorValue != new Color(.76f, .56f, .29f, 1f)) continue;
                colors.FindProperty("amber").colorValue = new Color(.22f, .66f, .3f, 1f);
                colors.FindProperty("magic").colorValue = new Color(.55f, .9f, .55f, 1f);
                colors.FindProperty("confirmed").colorValue = new Color(.22f, .66f, .3f, 1f);
                colors.ApplyModifiedProperties();
                EditorSceneManager.MarkSceneDirty(zone.gameObject.scene);
            }
        }

        private static void AddVanishParticles(PerkShopPresentation shop)
        {
            CustomerPanelSuccessParticles source = null;
            foreach (GameObject root in shop.gameObject.scene.GetRootGameObjects())
            {
                foreach (MealFoodSwapEffect menu in root.GetComponentsInChildren<MealFoodSwapEffect>(true))
                {
                    source = new SerializedObject(menu).FindProperty("revealParticles").objectReferenceValue as CustomerPanelSuccessParticles;
                    if (source != null) break;
                }
                if (source != null) break;
            }
            if (source == null) return;
            var shopSettings = new SerializedObject(shop);
            var floor = shopSettings.FindProperty("floorRoot").objectReferenceValue as GameObject;
            foreach (GameObject root in new[] { shop.gameObject, floor })
            {
                if (root == null) continue;
                foreach (PerkCardAnimation animation in root.GetComponentsInChildren<PerkCardAnimation>(true))
                {
                    var settings = new SerializedObject(animation);
                    if (settings.FindProperty("vanishParticles").objectReferenceValue != null) continue;
                    // A sibling survives the card shrinking to zero, exactly like the menu particles.
                    GameObject effect = Object.Instantiate(source.gameObject, animation.transform.parent, false);
                    Undo.RegisterCreatedObjectUndo(effect, "Add perk disappearance particles");
                    effect.name = animation.name + " Vanish Sparkles";
                    var rect = (RectTransform)effect.transform;
                    rect.anchoredPosition = ((RectTransform)animation.transform).anchoredPosition;
                    rect.localScale = animation.transform.localScale;
                    effect.SetActive(true);
                    settings.FindProperty("vanishParticles").objectReferenceValue = effect.GetComponent<CustomerPanelSuccessParticles>();
                    settings.ApplyModifiedProperties();
                    EditorSceneManager.MarkSceneDirty(shop.gameObject.scene);
                }
            }
        }

        private static void RemoveUpperCountdown(PerkShopPresentation shop)
        {
            Transform oldTimer = shop.transform.Find("Perk Offers/Selection Countdown");
            if (oldTimer == null) return;
            var settings = new SerializedObject(shop);
            var guide = settings.FindProperty("guide").objectReferenceValue as NpcGuidePresentation;
            if (guide == null || guide.transform.Find("TextPerk/Time/TextTime") == null) return;
            Undo.DestroyObjectImmediate(oldTimer.gameObject);
            EditorSceneManager.MarkSceneDirty(shop.gameObject.scene);
        }

        private static void RemoveChild(Transform parent, string name)
        {
            Transform child = parent.Find(name);
            if (child != null) Undo.DestroyObjectImmediate(child.gameObject);
        }
    }
}
