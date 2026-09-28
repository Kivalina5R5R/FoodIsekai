using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

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
                    foreach (PerkShopPresentation shop in root.GetComponentsInChildren<PerkShopPresentation>(true))
                        Apply(shop);
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
            if (shop.LayoutRevision >= 3) return;
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
                var bubble = guide.transform.Find("TextPerk") as RectTransform;
                if (bubble != null)
                {
                    Undo.RecordObject(bubble, "Fit perk dialogue between cards");
                    bubble.anchoredPosition = new Vector2(-100f, bubble.anchoredPosition.y);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(bubble);
                }
            }
            var managerSettings = new SerializedObject(manager);
            managerSettings.FindProperty("intermissionDurationSeconds").floatValue = 30f;
            managerSettings.ApplyModifiedProperties();
            settings.FindProperty("layoutRevision").intValue = 3;
            settings.ApplyModifiedProperties();
            Undo.CollapseUndoOperations(group);
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

        private static void RemoveChild(Transform parent, string name)
        {
            Transform child = parent.Find(name);
            if (child != null) Undo.DestroyObjectImmediate(child.gameObject);
        }
    }
}
