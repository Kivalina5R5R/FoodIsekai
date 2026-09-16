using FoodIsekaiZ.Gameplay;
using Fortal.UWB;
using UnityEngine;

namespace FoodIsekaiZ.Players
{

    [RequireComponent(typeof(Rigidbody), typeof(SphereCollider))]
    [RequireComponent(typeof(FoodIsekaiZPlayerState))]
    public sealed class UWBPlayerController : MonoBehaviour
    {
        [Header("Identity")]
        [SerializeField, Min(0)] private int playerId = 1;
        [SerializeField, Min(0)] private int tagId = 1;

        [Header("UWB")]
        [SerializeField] private UWBManager uwbManager;
        [SerializeField] private bool hideWhenOffline;

        [SerializeField, Min(0f)] private float floorHeight = 0.12f;

        [Header("Player Size")]
        [Tooltip("Uniform scale applied to the Player root, including its plate and collider.")]
        [SerializeField, Min(0.05f)] private float playerScale = 1f;

        [Header("Editor Simulation")]
        [Tooltip("During Play Mode, dragging this Player in Scene View moves its standalone position, or writes it back to the simulated UWB tag when using UWB Simulation.")]
        [SerializeField] private bool allowSceneViewDragInSimulation = true;
        [SerializeField, Min(0.0001f)] private float sceneViewDragThreshold = 0.002f;

        [Header("Player Plate")]
        [SerializeField] private GameObject authoredMarker;
        [SerializeField] private GameObject selectionMarker;

        [Header("Runtime (Read Only)")]
        [SerializeField] private bool isTracking;
        [SerializeField] private float sampleAgeSeconds = 999f;

        private Rigidbody body;
        private bool isRegistered;
        private bool useUwbTracking = true;
        private bool hasControllerPosition;
        private Vector3 lastControllerPosition;
        private Vector3 baseLocalScale = Vector3.one;
        private bool baseScaleCaptured;
        private bool selectingPlayerNumber;
        private bool selectionMarkerVisible;

        public int PlayerId => playerId;
        public int TagId => tagId;
        public bool IsTracking => isTracking;
        public float SampleAgeSeconds => sampleAgeSeconds;
        public bool IsAvailableForSelection => isActiveAndEnabled && (!useUwbTracking || isTracking);

        // A tag keeps its tracking controller but has no gameplay identity until it chooses a number.
        public void ConfigureUnassignedTag(int newTagId)
        {
            playerId = 0;
            selectingPlayerNumber = true;
            selectionMarkerVisible = false;
            SetTagId(newTagId);
            gameObject.name = $"Unassigned_Tag{tagId}";
            SetPlayerMarkerVisible(true);
        }

        public void ShowSelectionMarker(bool visible)
        {
            selectionMarkerVisible = visible;
            SetPlayerMarkerVisible(!useUwbTracking || isTracking);
        }

        public void EnterGameplay()
        {
            selectingPlayerNumber = false;
            SetPlayerMarkerVisible(!useUwbTracking || isTracking);
        }

        private void Awake()
        {
            if (body == null) body = GetComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            body.constraints = RigidbodyConstraints.FreezeRotation;
            ApplyPlayerScale();

            // Keep the authored plate facing the floor camera.
            transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            RecordControllerPosition(transform.position);
        }

        private void OnEnable()
        {
            FindAndRegisterManager();
        }

        private void OnDisable()
        {
            UnregisterManager();
        }

        private void Update()
        {
            if (!useUwbTracking)
            {
                if (CaptureStandaloneSimulationDrag())
                {
                    return;
                }

                isTracking = false;
                sampleAgeSeconds = 0f;
                SetPlayerMarkerVisible(true);
                return;
            }

            if (!isRegistered)
            {
                FindAndRegisterManager();
            }

            if (CaptureSceneViewSimulationDrag())
            {
                return;
            }

            if (uwbManager == null ||
                !uwbManager.TryGetArenaPosition2D(tagId, out Vector2 measuredPosition2D, out sampleAgeSeconds))
            {
                isTracking = false;
                SetPlayerMarkerVisible(!hideWhenOffline);
                return;
            }

            isTracking = true;
            SetPlayerMarkerVisible(true);
            Vector3 measuredPosition = new Vector3(measuredPosition2D.x, floorHeight, measuredPosition2D.y);

            // UWBManager already applies the smoothing configured in UWBConfig.json.
            body.position = measuredPosition;
            RecordControllerPosition(measuredPosition);
        }

        private bool CaptureSceneViewSimulationDrag()
        {
#if UNITY_EDITOR
            if (!allowSceneViewDragInSimulation || !Application.isPlaying || body == null ||
                !hasControllerPosition)
            {
                return false;
            }

            if (uwbManager == null || !uwbManager.IsSimulationMode)
            {
                return false;
            }

            Vector3 currentPosition = transform.position;
            Vector2 planarDelta = new Vector2(
                currentPosition.x - lastControllerPosition.x,
                currentPosition.z - lastControllerPosition.z);
            if (planarDelta.sqrMagnitude <= sceneViewDragThreshold * sceneViewDragThreshold)
            {
                return false;
            }

            Vector3 committedPosition = new Vector3(currentPosition.x, floorHeight, currentPosition.z);
            if (!uwbManager.SetSimulatedArenaPosition2D(
                    tagId,
                    new Vector2(committedPosition.x, committedPosition.z)))
            {
                return false;
            }

            body.position = committedPosition;
            RecordControllerPosition(committedPosition);
            return true;
#else
            return false;
#endif
        }

        private bool CaptureStandaloneSimulationDrag()
        {
#if UNITY_EDITOR
            if (!allowSceneViewDragInSimulation || !Application.isPlaying || body == null ||
                !hasControllerPosition)
            {
                return false;
            }

            Vector3 currentPosition = transform.position;
            Vector2 planarDelta = new Vector2(
                currentPosition.x - lastControllerPosition.x,
                currentPosition.z - lastControllerPosition.z);
            if (planarDelta.sqrMagnitude <= sceneViewDragThreshold * sceneViewDragThreshold)
            {
                return false;
            }

            Vector3 committedPosition = new Vector3(currentPosition.x, floorHeight, currentPosition.z);
            body.position = committedPosition;
            RecordControllerPosition(committedPosition);
            return true;
#else
            return false;
#endif
        }

        private void RecordControllerPosition(Vector3 position)
        {
            lastControllerPosition = position;
            hasControllerPosition = true;
            transform.hasChanged = false;
        }

        public void SetTagId(int newTagId)
        {
            newTagId = Mathf.Max(0, newTagId);
            if (tagId == newTagId)
            {
                return;
            }

            UnregisterManager();
            tagId = newTagId;
            FindAndRegisterManager();
        }

        // กำหนดว่าจะให้ Controller อ่านตำแหน่งจาก UWB หรือไม่
        public void SetUwbTrackingEnabled(bool enabled)
        {
            if (useUwbTracking == enabled)
            {
                return;
            }

            useUwbTracking = enabled;
            if (useUwbTracking)
            {
                FindAndRegisterManager();
                return;
            }

            UnregisterManager();
            isTracking = false;
            sampleAgeSeconds = 0f;
            SetPlayerMarkerVisible(true);
        }

        // ตั้งตำแหน่งโลกของ Player สำหรับ standalone Simulation mode
        public void SetStandaloneWorldPosition(Vector2 worldPosition)
        {
            Vector3 targetPosition = new Vector3(worldPosition.x, floorHeight, worldPosition.y);
            transform.position = targetPosition;
            if (body != null)
            {
                body.position = targetPosition;
            }

            RecordControllerPosition(targetPosition);
        }

        // Applies the player identity and UWB tag without modifying the authored plate.
        public void Configure(int newPlayerId, int newTagId)
        {
            if (body == null) body = GetComponent<Rigidbody>();
            playerId = Mathf.Max(1, newPlayerId);
            SetTagId(newTagId);

            gameObject.name = $"Player{playerId:00}_Tag{tagId}";
        }

        public void SetPlayerScale(float newScale)
        {
            playerScale = Mathf.Max(0.05f, newScale);
            ApplyPlayerScale();
        }

        private void ApplyPlayerScale()
        {
            if (!baseScaleCaptured)
            {
                baseLocalScale = transform.localScale;
                baseScaleCaptured = true;
            }

            transform.localScale = baseLocalScale * Mathf.Max(0.05f, playerScale);
        }

        private void SetPlayerMarkerVisible(bool visible)
        {
            bool showPlate = visible && !selectingPlayerNumber;
            if (authoredMarker != null && authoredMarker.activeSelf != showPlate)
            {
                authoredMarker.SetActive(showPlate);
            }
            bool showCircle = visible && selectingPlayerNumber && selectionMarkerVisible;
            if (selectionMarker != null && selectionMarker.activeSelf != showCircle)
            {
                selectionMarker.SetActive(showCircle);
            }
        }

        private void FindAndRegisterManager()
        {
            if (!useUwbTracking)
            {
                return;
            }

            if (uwbManager == null)
            {
                uwbManager = FindAnyObjectByType<UWBManager>();
            }

            if (uwbManager == null || isRegistered)
            {
                return;
            }

            uwbManager.RegisterTag(tagId);
            isRegistered = true;
        }

        private void UnregisterManager()
        {
            if (uwbManager != null && isRegistered)
            {
                uwbManager.UnregisterTag(tagId);
            }

            isRegistered = false;
        }

    }
}
