using UnityEngine;

namespace Fortal.UWB
{
    /// <summary>
    /// Attach to a follow-target GameObject. Registers itself with a <see cref="UWBManager"/> by
    /// <see cref="tagId"/> and applies drift-free smoothing (low-pass filter + deadzone + SmoothDamp)
    /// to the positions the manager resolves each frame.
    /// </summary>
    public sealed class UWBTracker : MonoBehaviour
    {
        [Header("UWB Tag")]
        [Tooltip("NoopLoop UWB tag ID this target follows.")]
        public int tagId = 1;

        [Header("Manager")]
        [Tooltip("Left empty, the tracker finds the first UWBManager in the scene.")]
        [SerializeField] private UWBManager manager;

        [Header("Smoothing")]
        [Tooltip("SmoothDamp time (higher = smoother but slower response)")]
        [Range(0.05f, 0.5f)] public float smoothTime = 0.15f;
        [Tooltip("Ignore movement smaller than this many meters (prevents jitter when standing still)")]
        [Range(0.01f, 0.2f)] public float deadzone = 0.03f;
        [Tooltip("Low-pass filter strength (0 = no filter, 0.95 = very smooth). Filters raw UWB noise.")]
        [Range(0f, 0.95f)] public float filterStrength = 0.6f;
        [Tooltip("If position jumps more than this many meters, snap instantly instead of smoothing")]
        [Range(0.5f, 5f)] public float snapDistance = 2f;

        [Header("Status (Read Only)")]
        public bool isTracking;
        public float ageSeconds = 999f;
        public Vector3 velocity;

        private Vector3 filteredPosition;
        private Vector3 targetPosition;
        private Vector3 smoothVelocity;
        private Vector3 lastAppliedPosition;
        private bool hasFirstData;
        private float metersToWorldScale = 1f;

        private void OnEnable()
        {
            if (manager == null)
            {
                manager = FindAnyObjectByType<UWBManager>();
            }

            FoodIsekaiZ.Configuration.UWBConfigData config = FoodIsekaiZ.Configuration.UWBConfigManager.GetConfig();
            if (config != null)
            {
                metersToWorldScale = config.metersToWorldScale;
            }

            manager?.AddTag(this);
        }

        private void OnDisable()
        {
            manager?.RemoveTag(this);
        }

        /// <summary>Changes tagId and re-registers with the manager under the new key.</summary>
        public void SetTagId(int newTagId)
        {
            if (tagId == newTagId)
            {
                return;
            }

            if (isActiveAndEnabled)
            {
                manager?.RemoveTag(this);
            }

            tagId = newTagId;

            if (isActiveAndEnabled)
            {
                if (manager == null)
                {
                    manager = FindAnyObjectByType<UWBManager>();
                }

                manager?.AddTag(this);
            }
        }

        private void Update()
        {
            if (manager == null)
            {
                manager = FindAnyObjectByType<UWBManager>();
                manager?.AddTag(this);
            }

            if (!hasFirstData)
            {
                return;
            }

            // SmoothDamp gives natural acceleration/deceleration (much better than Lerp).
            transform.position = Vector3.SmoothDamp(transform.position, targetPosition, ref smoothVelocity, smoothTime);
        }

        /// <summary>Called by the owning UWBManager whenever it resolves a fresh or predicted position for tagId.</summary>
        public void ApplyTrackedPosition(Vector3 positionMeters, float sampleAgeSeconds)
        {
            isTracking = true;
            ageSeconds = sampleAgeSeconds;

            // Convert real-world meters into game-world units before any smoothing/deadzone logic.
            positionMeters *= metersToWorldScale;

            if (!hasFirstData)
            {
                filteredPosition = positionMeters;
                targetPosition = positionMeters;
                transform.position = positionMeters;
                lastAppliedPosition = positionMeters;
                hasFirstData = true;
                return;
            }

            // Low-pass filter to smooth out raw UWB noise before the deadzone check.
            filteredPosition = Vector3.Lerp(positionMeters, filteredPosition, filterStrength);

            float distToTarget = Vector3.Distance(filteredPosition, targetPosition);
            if (distToTarget > snapDistance)
            {
                targetPosition = filteredPosition;
                smoothVelocity = Vector3.zero;
            }
            else if (distToTarget > deadzone)
            {
                targetPosition = filteredPosition;
            }
            // else: within deadzone, don't update target -> object stays still.

            float dt = Mathf.Max(Time.deltaTime, 0.0001f);
            velocity = (positionMeters - lastAppliedPosition) / dt;
            lastAppliedPosition = positionMeters;
        }

        /// <summary>Called by the owning UWBManager when this tag's data has gone stale.</summary>
        public void SetOffline()
        {
            isTracking = false;
        }
    }
}
