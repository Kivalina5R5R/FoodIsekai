using FoodIsekaiZ.Gameplay;
using Fortal.UWB;
using UnityEngine;

namespace FoodIsekaiZ.Players
{

    [RequireComponent(typeof(Rigidbody), typeof(SphereCollider), typeof(SpriteRenderer))]
    [RequireComponent(typeof(FoodIsekaiZPlayerState))]
    public sealed class UWBPlayerController : MonoBehaviour
    {
        [Header("Identity")]
        [SerializeField, Min(1)] private int playerId = 1;
        [SerializeField, Min(0)] private int tagId = 1;
        [SerializeField] private Color playerColor = Color.cyan;

        [Header("UWB")]
        [SerializeField] private UWBManager uwbManager;
        [SerializeField] private bool hideWhenOffline;

        [Header("Movement Smoothing")]
        [SerializeField, Range(0.02f, 0.5f)] private float smoothTime = 0.12f;
        [SerializeField, Min(0f)] private float positionDeadZone = 0.03f;
        [SerializeField, Min(0.1f)] private float snapDistance = 2f;
        [SerializeField, Min(0.1f)] private float maxSpeed = 12f;
        [SerializeField, Min(0f)] private float floorHeight = 0.12f;

        [Header("Editor Simulation")]
        [Tooltip("During Play Mode, dragging this Player in Scene View writes the position back to its simulated UWB tag.")]
        [SerializeField] private bool allowSceneViewDragInSimulation = true;
        [SerializeField, Min(0.0001f)] private float sceneViewDragThreshold = 0.002f;

        [Header("Floor Player Status")]
        [SerializeField] private bool showCarriedItems = true;
        [SerializeField, Range(0.02f, 0.12f)] private float statusCharacterSize = 0.055f;
        [SerializeField] private Color statusTextColor = Color.white;

        [Header("Runtime (Read Only)")]
        [SerializeField] private bool isTracking;
        [SerializeField] private float sampleAgeSeconds = 999f;

        private Rigidbody body;
        private SpriteRenderer circleRenderer;
        private FoodIsekaiZPlayerState playerState;
        private TextMesh carriedStatusText;
        private Vector3 targetPosition;
        private Vector3 smoothVelocity;
        private bool hasFirstPosition;
        private bool isRegistered;
        private bool hasControllerPosition;
        private Vector3 lastControllerPosition;
        private Texture2D generatedCircleTexture;
        private Sprite generatedCircleSprite;

        public int PlayerId => playerId;
        public int TagId => tagId;
        public bool IsTracking => isTracking;
        public float SampleAgeSeconds => sampleAgeSeconds;

        private void Awake()
        {
            CacheRequiredComponents();
            body.isKinematic = true;
            body.useGravity = false;
            body.constraints = RigidbodyConstraints.FreezeRotation;
            circleRenderer.color = playerColor;

            // Sprite ปกติอยู่บน XY; หมุนให้นอนบนพื้น XZ และหันขึ้นหากล้อง Floor
            transform.rotation = Quaternion.Euler(90f, 0f, 0f);

            if (circleRenderer.sprite == null)
            {
                circleRenderer.sprite = CreateCircleSprite(64);
            }

            CreateStatusLabel();
            RefreshStatusLabel();
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

        private void OnDestroy()
        {
            if (generatedCircleSprite != null)
            {
                Destroy(generatedCircleSprite);
            }

            if (generatedCircleTexture != null)
            {
                Destroy(generatedCircleTexture);
            }
        }

        private void Update()
        {
            RefreshStatusLabel();

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
                circleRenderer.enabled = !hideWhenOffline;
                return;
            }

            isTracking = true;
            circleRenderer.enabled = true;
            Vector3 measuredPosition = new Vector3(measuredPosition2D.x, floorHeight, measuredPosition2D.y);

            if (!hasFirstPosition || Vector3.Distance(body.position, measuredPosition) >= snapDistance)
            {
                targetPosition = measuredPosition;
                body.position = measuredPosition;
                smoothVelocity = Vector3.zero;
                hasFirstPosition = true;
                RecordControllerPosition(measuredPosition);
                return;
            }

            if (Vector3.Distance(targetPosition, measuredPosition) >= positionDeadZone)
            {
                targetPosition = measuredPosition;
            }
        }

        private void FixedUpdate()
        {
            if (CaptureSceneViewSimulationDrag())
            {
                return;
            }

            if (!hasFirstPosition)
            {
                return;
            }

            Vector3 next = Vector3.SmoothDamp(
                body.position,
                targetPosition,
                ref smoothVelocity,
                smoothTime,
                maxSpeed,
                Time.fixedDeltaTime);
            body.MovePosition(next);
            RecordControllerPosition(next);
        }

        private bool CaptureSceneViewSimulationDrag()
        {
#if UNITY_EDITOR
            if (!allowSceneViewDragInSimulation || !Application.isPlaying ||
                uwbManager == null || !uwbManager.IsSimulationMode || body == null ||
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
            if (!uwbManager.SetSimulatedArenaPosition2D(
                    tagId,
                    new Vector2(committedPosition.x, committedPosition.z)))
            {
                return false;
            }

            body.position = committedPosition;
            targetPosition = committedPosition;
            smoothVelocity = Vector3.zero;
            hasFirstPosition = true;
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
            hasFirstPosition = false;
            FindAndRegisterManager();
        }

        public void Configure(int newPlayerId, int newTagId, Color newColor)
        {

            CacheRequiredComponents();
            playerId = Mathf.Max(1, newPlayerId);
            playerColor = newColor;
            SetTagId(newTagId);

            if (circleRenderer != null)
            {
                circleRenderer.color = playerColor;
            }

            gameObject.name = $"Player{playerId:00}_Tag{tagId}";
            RefreshStatusLabel();
        }

        private void CreateStatusLabel()
        {
            if (!showCarriedItems || carriedStatusText != null)
            {
                return;
            }

            GameObject labelObject = new GameObject("PlayerStatus");
            labelObject.transform.SetParent(transform, false);
            // Player's local -Z points up from the floor after its 90-degree X rotation.
            labelObject.transform.localPosition = new Vector3(0f, 0f, -0.035f);

            carriedStatusText = labelObject.AddComponent<TextMesh>();
            carriedStatusText.anchor = TextAnchor.MiddleCenter;
            carriedStatusText.alignment = TextAlignment.Center;
            carriedStatusText.fontSize = 64;
            carriedStatusText.characterSize = statusCharacterSize;
            carriedStatusText.fontStyle = FontStyle.Bold;
            carriedStatusText.color = statusTextColor;

            MeshRenderer labelRenderer = labelObject.GetComponent<MeshRenderer>();
            if (labelRenderer != null && circleRenderer != null)
            {
                labelRenderer.sortingOrder = circleRenderer.sortingOrder + 1;
            }
        }

        private void CacheRequiredComponents()
        {
            if (body == null)
            {
                body = GetComponent<Rigidbody>();
            }

            if (circleRenderer == null)
            {
                circleRenderer = GetComponent<SpriteRenderer>();
            }

            if (playerState == null)
            {
                playerState = GetComponent<FoodIsekaiZPlayerState>();
            }
        }

        private void RefreshStatusLabel()
        {
            if (!showCarriedItems)
            {
                if (carriedStatusText != null)
                {
                    carriedStatusText.gameObject.SetActive(false);
                }

                return;
            }

            if (carriedStatusText == null)
            {
                CreateStatusLabel();
            }

            if (carriedStatusText == null)
            {
                return;
            }

            if (playerState == null)
            {
                playerState = GetComponent<FoodIsekaiZPlayerState>();
            }

            string food = playerState != null && playerState.HeldFood != FoodType.None
                ? $"F{(int)playerState.HeldFood}"
                : "--";
            int money = playerState != null ? playerState.CarriedMoney : 0;
            carriedStatusText.text = $"P{playerId}\n{food}  ${money}";
            carriedStatusText.color = statusTextColor;
            carriedStatusText.characterSize = statusCharacterSize;
            carriedStatusText.gameObject.SetActive(true);
        }

        private void FindAndRegisterManager()
        {
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

        private Sprite CreateCircleSprite(int size)
        {
            generatedCircleTexture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = $"Runtime Circle P{playerId}",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            Color[] pixels = new Color[size * size];
            float center = (size - 1) * 0.5f;
            float radius = center - 1f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float distance = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                    float alpha = Mathf.Clamp01(radius + 1f - distance);
                    pixels[(y * size) + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            generatedCircleTexture.SetPixels(pixels);
            generatedCircleTexture.Apply();
            generatedCircleSprite = Sprite.Create(
                generatedCircleTexture,
                new Rect(0f, 0f, size, size),
                new Vector2(0.5f, 0.5f),
                size);
            return generatedCircleSprite;
        }
    }
}
