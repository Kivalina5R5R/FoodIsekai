using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace FoodIsekaiZ.Display
{
    // Routes the installation outputs: Display 1 (the main window) is the LED floor and Display 2 is
    // the wall. The touch kiosk is retired: its camera and canvases stay wired for their settings but
    // are switched off and never drawn, and no window opens on its monitor.
    [DefaultExecutionOrder(-200)]
    public sealed class DisplayManager : MonoBehaviour
    {
        [Header("Retired - Kiosk (kept for its settings, never shown)")]
        [SerializeField] private Camera kioskCamera;
        [SerializeField] private Canvas[] kioskCanvases;
        [SerializeField] private Vector2Int kioskDisplayResolution = new Vector2Int(1080, 1920);
        [SerializeField] private string kioskMonitorName = "";

        [Header("Display 2 - Wall")]
        [SerializeField] private Camera sideCamera;
        [SerializeField] private Canvas[] sideCanvases;

        [Header("Display 1 - LED Floor (main window)")]
        [SerializeField] private Camera floorCamera;
        [SerializeField] private Canvas[] floorCanvases;

        [Header("Options")]
        // Activates the wall output on startup.
        [SerializeField] private bool activateSecondDisplayOnAwake = true;
        [SerializeField] private bool applyPaperArenaResolutionsInStandalone = true;
        // In a build, move the main window to the floor monitor and send the wall to the monitor
        // whose shape matches its resolution.
        [SerializeField] private bool placeOutputsOnMatchingMonitors = true;
        // Optional part of the floor monitor name as printed in Player.log. Leave empty to pick the
        // monitor whose shape matches floorDisplayResolution.
        [SerializeField] private string floorMonitorName = "";
        [SerializeField] private Vector2Int sideDisplayResolution = new Vector2Int(8192, 2160);
        [SerializeField] private Vector2Int floorDisplayResolution = new Vector2Int(2944, 1408);
        [SerializeField, Min(30)] private int refreshRate = 60;
        [SerializeField] private Color sideDisplayBackground = new Color(0.025f, 0.035f, 0.055f, 1f);

        public Vector2Int SideDisplayResolution => sideDisplayResolution;
        // The camera that draws the wall; wall overlays such as the HOW TO PLAY board find their canvas by it.
        public Camera WallCamera => sideCamera;
        // The camera that draws the LED floor.
        public Camera FloorCamera => floorCamera;

        // The world-space canvas drawn by the wall camera (the largest one when there are several),
        // or null when the scene has no DisplayManager or no such canvas.
        public static RectTransform FindWallCanvas()
        {
            DisplayManager displays = FindFirstObjectByType<DisplayManager>(FindObjectsInactive.Include);
            Camera wallCamera = displays != null ? displays.WallCamera : null;
            if (wallCamera == null) return null;

            RectTransform best = null;
            float bestArea = 0f;
            foreach (Canvas canvas in FindObjectsByType<Canvas>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (!canvas.isRootCanvas || canvas.worldCamera != wallCamera) continue;
                // Overlays nested inside the wall (such as the intro overlay, which sorts on its own and
                // is slightly wider) are not the wall itself; anything drawn into them hides with them.
                Transform parent = canvas.transform.parent;
                if (parent != null && parent.GetComponentInParent<Canvas>(true) != null) continue;
                var rect = (RectTransform)canvas.transform;
                float area = rect.rect.width * rect.rect.height;
                if (area <= bestArea) continue;
                bestArea = area;
                best = rect;
            }

            return best;
        }
        public Vector2Int FloorDisplayResolution => floorDisplayResolution;

        private int floorOutputWidth;
        private int floorOutputHeight;
        private int wallDisplayIndex = DisplayOutput.WallDisplayIndex;

        private void Awake()
        {
            RetireKiosk();
            ConfigureOutputs();

            if (placeOutputsOnMatchingMonitors && !Application.isEditor)
            {
                StartCoroutine(PlaceOutputsOnMatchingMonitors());
                return;
            }

            ApplyFloorResolution();

            if (activateSecondDisplayOnAwake)
            {
                ActivateDisplays();
            }
        }

        // The kiosk's camera and canvases stay in the scene with their settings but draw nothing.
        private void RetireKiosk()
        {
            if (kioskCamera != null) kioskCamera.enabled = false;
            if (kioskCanvases == null) return;

            foreach (Canvas canvas in kioskCanvases)
            {
                if (canvas != null) canvas.enabled = false;
            }
        }

        private IEnumerator PlaceOutputsOnMatchingMonitors()
        {
            // The window is not ready to move during the first Awake.
            yield return null;

            var layout = new List<DisplayInfo>();
            Screen.GetDisplayLayout(layout);
            for (int i = 0; i < layout.Count; i++)
            {
                Debug.Log($"[DisplayManager] Monitor {i}: \"{layout[i].name}\" {layout[i].width}x{layout[i].height}, " +
                    $"workArea={layout[i].workArea}", this);
            }

            // Unity reopens on the monitor it last used (or the Windows main display, which may be the
            // old kiosk), so bring the main window to the floor every launch.
            int floorMonitor = FindFloorMonitor(layout);
            if (floorMonitor >= 0 && !layout[floorMonitor].Equals(Screen.mainWindowDisplayInfo))
            {
                DisplayInfo target = layout[floorMonitor];
                Debug.Log($"[DisplayManager] Moving main window to floor monitor {floorMonitor} \"{target.name}\"", this);

                // Moving a fullscreen window is unreliable, so move it as a window and go fullscreen afterwards.
                if (Screen.fullScreenMode != FullScreenMode.Windowed)
                {
                    Screen.fullScreenMode = FullScreenMode.Windowed;
                    yield return null;
                }
                yield return Screen.MoveMainWindowTo(target, Vector2Int.zero);
                yield return null;
            }

            ApplyFloorResolution();
            yield return null;
            Debug.Log($"[DisplayManager] Floor (main window) is on \"{Screen.mainWindowDisplayInfo.name}\"", this);

            FindWallOutput();
            ConfigureOutputs();

            if (activateSecondDisplayOnAwake)
            {
                ActivateDisplays();
            }

            StartCoroutine(KeepGameWindowsOnTop());
        }

        private void ApplyFloorResolution()
        {
            if (!applyPaperArenaResolutionsInStandalone || Application.isEditor) return;
            Screen.SetResolution(floorDisplayResolution.x, floorDisplayResolution.y,
                FullScreenMode.FullScreenWindow, TargetRefreshRate);
        }

        private RefreshRate TargetRefreshRate => new RefreshRate
        {
            numerator = (uint)refreshRate,
            denominator = 1u
        };

        // New display windows appear a few frames after activation, and the launcher may still hold focus.
        private IEnumerator KeepGameWindowsOnTop()
        {
            for (int i = 0; i < 5; i++)
            {
                yield return new WaitForSecondsRealtime(1f);
                GameWindowsOnTop.Apply();
            }
        }

        // By name when floorMonitorName is set, otherwise the monitor shaped most like the floor.
        private int FindFloorMonitor(List<DisplayInfo> layout)
        {
            if (!string.IsNullOrWhiteSpace(floorMonitorName))
            {
                for (int i = 0; i < layout.Count; i++)
                {
                    if (layout[i].name != null &&
                        layout[i].name.IndexOf(floorMonitorName.Trim(), System.StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return i;
                    }
                }
                Debug.LogWarning($"[DisplayManager] No monitor name contains \"{floorMonitorName}\"; matching by shape", this);
            }

            int best = -1;
            float bestScore = float.MaxValue;
            for (int i = 0; i < layout.Count; i++)
            {
                float score = MatchScore(layout[i].width, layout[i].height, floorDisplayResolution);
                if (score < bestScore)
                {
                    bestScore = score;
                    best = i;
                }
            }
            return best;
        }

        // Display.displays[0] is the main window (floor). The wall takes the other output closest to
        // its shape, so a still-connected kiosk monitor is passed over.
        private void FindWallOutput()
        {
            var displays = UnityEngine.Display.displays;
            for (int i = 0; i < displays.Length; i++)
            {
                Debug.Log($"[DisplayManager] Unity display {i}: {displays[i].systemWidth}x{displays[i].systemHeight}", this);
            }

            wallDisplayIndex = FindOutput(sideDisplayResolution);
        }

        private static int FindOutput(Vector2Int resolution)
        {
            var displays = UnityEngine.Display.displays;
            int best = -1;
            float bestScore = float.MaxValue;
            for (int i = 1; i < displays.Length; i++)
            {
                float score = MatchScore(displays[i].systemWidth, displays[i].systemHeight, resolution);
                if (score < bestScore)
                {
                    bestScore = score;
                    best = i;
                }
            }
            return best;
        }

        // Lower is better. Aspect ratio decides the role; exact size only breaks ties.
        private static float MatchScore(int width, int height, Vector2Int resolution)
        {
            if (width <= 0 || height <= 0 || resolution.x <= 0 || resolution.y <= 0) return float.MaxValue;
            float aspectDifference = Mathf.Abs((float)width / height - (float)resolution.x / resolution.y);
            float sizeDifference = Mathf.Abs(width - resolution.x) + Mathf.Abs(height - resolution.y);
            return aspectDifference * 100000f + sizeDifference;
        }

        // Resolution changes complete after the current frame. Fit again when the floor output changes.
        private void LateUpdate()
        {
            if (Application.isEditor || !applyPaperArenaResolutionsInStandalone || floorCamera == null) return;
            UnityEngine.Display floor = UnityEngine.Display.displays[DisplayOutput.FloorDisplayIndex];
            int width = floor.renderingWidth;
            int height = floor.renderingHeight;
            if (width <= 0 || height <= 0 || floorDisplayResolution.x <= 0 || floorDisplayResolution.y <= 0) return;
            if (width == floorOutputWidth && height == floorOutputHeight) return;
            floorOutputWidth = width;
            floorOutputHeight = height;

            float referenceAspect = (float)floorDisplayResolution.x / floorDisplayResolution.y;
            float outputAspect = (float)width / height;
            Rect viewport = new Rect(0f, 0f, 1f, 1f);
            if (outputAspect > referenceAspect)
            {
                viewport.width = referenceAspect / outputAspect;
                viewport.x = (1f - viewport.width) * 0.5f;
            }
            else
            {
                viewport.height = outputAspect / referenceAspect;
                viewport.y = (1f - viewport.height) * 0.5f;
            }
            floorCamera.rect = viewport;
            floorCamera.aspect = referenceAspect;
            Debug.Log($"[DisplayManager] Floor requested={floorDisplayResolution.x}x{floorDisplayResolution.y}, " +
                $"rendering={width}x{height}, monitor={floor.systemWidth}x{floor.systemHeight}, " +
                $"display={DisplayOutput.FloorDisplayIndex}, displays={UnityEngine.Display.displays.Length}, viewport={viewport}", this);
        }

        [ContextMenu("Configure Camera And Canvas Outputs")]
        public void ConfigureOutputs()
        {
            // The floor is always the main window; a build may move the wall to whichever Unity
            // display matches its monitor.
            if (floorCamera != null)
            {
                floorCamera.targetDisplay = DisplayOutput.FloorDisplayIndex;
            }

            if (sideCamera != null)
            {
                sideCamera.targetDisplay = WallIndex;
                sideCamera.clearFlags = CameraClearFlags.SolidColor;
                sideCamera.backgroundColor = sideDisplayBackground;
            }

            // The retired kiosk is pointed at a display that is never activated, in case it is ever
            // switched back on by hand.
            if (kioskCamera != null)
            {
                kioskCamera.targetDisplay = DisplayOutput.RetiredKioskDisplayIndex;
            }

            // The kiosk's canvases are switched off, so no camera layer is hidden for them: they share
            // the UI layer with the wall's intro video and the floor's canvases.
            SetCanvasDisplay(kioskCanvases, DisplayOutput.RetiredKioskDisplayIndex);
            SetCanvasDisplay(floorCanvases, DisplayOutput.FloorDisplayIndex);
            SetCanvasDisplay(sideCanvases, WallIndex);
        }

        private int WallIndex => wallDisplayIndex >= 1 ? wallDisplayIndex : DisplayOutput.WallDisplayIndex;

        [ContextMenu("Activate Displays")]
        public void ActivateDisplays()
        {
            if (UnityEngine.Display.displays.Length < 2)
            {
                Debug.LogWarning("[DisplayManager] ระบบปฏิบัติการรายงานจอเพียง 1 จอ", this);
                return;
            }

            ActivateOutput(wallDisplayIndex, sideDisplayResolution, "Wall");
        }

        private void ActivateOutput(int displayIndex, Vector2Int resolution, string role)
        {
            var displays = UnityEngine.Display.displays;
            if (displayIndex < 1 || displayIndex >= displays.Length)
            {
                Debug.LogWarning($"[DisplayManager] {role} output has no monitor (displays={displays.Length})", this);
                return;
            }

            if (applyPaperArenaResolutionsInStandalone && !Application.isEditor)
            {
                displays[displayIndex].Activate(resolution.x, resolution.y, TargetRefreshRate);
            }
            else
            {
                displays[displayIndex].Activate();
            }
            Debug.Log($"[DisplayManager] {role} output on Unity display {displayIndex} " +
                $"({displays[displayIndex].systemWidth}x{displays[displayIndex].systemHeight})", this);
        }

        private static void SetCanvasDisplay(Canvas[] canvases, int displayIndex)
        {
            if (canvases == null)
            {
                return;
            }

            for (int i = 0; i < canvases.Length; i++)
            {
                if (canvases[i] != null)
                {
                    canvases[i].targetDisplay = displayIndex;
                }
            }
        }
    }
}
