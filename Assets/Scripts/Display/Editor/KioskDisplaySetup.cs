using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FoodIsekaiZ.Display.Editor
{
    // Keeps the serialized target displays of the DisplayManager outputs in the role order from
    // DisplayOutput (Display 1 floor, Display 2 wall), so edit mode previews match a build.
    // The touch kiosk is retired and switched off; DisplayManager points it away at run time, so its
    // camera and canvases are left alone here (rewriting them marked the scene dirty on every reload).
    [InitializeOnLoad]
    public static class KioskDisplaySetup
    {
        private const string ScenePath = "Assets/Scenes/FoodIsekai.unity";

        static KioskDisplaySetup()
        {
            EditorApplication.delayCall += ApplyDisplayOrder;
            EditorSceneManager.sceneOpened += (scene, mode) => EditorApplication.delayCall += ApplyDisplayOrder;
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.EnteredEditMode) EditorApplication.delayCall += ApplyDisplayOrder;
            };
        }

        [MenuItem("Food Isekai/Apply Display Order")]
        public static void ApplyDisplayOrder()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.isLoaded || scene.path != ScenePath) return;
            DisplayManager manager = Object.FindFirstObjectByType<DisplayManager>(FindObjectsInactive.Include);
            if (manager == null) return;

            var managerData = new SerializedObject(manager);
            int changed = 0;
            changed += SetCameraDisplay(managerData, "floorCamera", DisplayOutput.FloorDisplayIndex);
            changed += SetCameraDisplay(managerData, "sideCamera", DisplayOutput.WallDisplayIndex);
            changed += SetCanvasDisplays(managerData, "floorCanvases", DisplayOutput.FloorDisplayIndex);
            changed += SetCanvasDisplays(managerData, "sideCanvases", DisplayOutput.WallDisplayIndex);
            if (changed == 0) return;

            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log($"[KioskDisplaySetup] Updated {changed} target display(s): Display 1 floor, Display 2 wall, " +
                "kiosk retired. Save the scene to keep it.");
        }

        private static int SetCameraDisplay(SerializedObject managerData, string property, int displayIndex)
        {
            var camera = managerData.FindProperty(property)?.objectReferenceValue as Camera;
            if (camera == null || camera.targetDisplay == displayIndex) return 0;
            Undo.RecordObject(camera, "Apply Display Order");
            camera.targetDisplay = displayIndex;
            return 1;
        }

        private static int SetCanvasDisplays(SerializedObject managerData, string property, int displayIndex)
        {
            SerializedProperty canvases = managerData.FindProperty(property);
            if (canvases == null) return 0;
            int changed = 0;
            for (int i = 0; i < canvases.arraySize; i++)
            {
                var canvas = canvases.GetArrayElementAtIndex(i).objectReferenceValue as Canvas;
                if (canvas == null || canvas.targetDisplay == displayIndex) continue;
                Undo.RecordObject(canvas, "Apply Display Order");
                canvas.targetDisplay = displayIndex;
                changed++;
            }
            return changed;
        }
    }
}
