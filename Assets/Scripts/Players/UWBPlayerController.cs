using Fortal.UWB;
using UnityEngine;

namespace FoodIsekaiZ.Players
{
    /// <summary>
    /// ผูกผู้เล่นหนึ่งคนกับ UWB Tag และเคลื่อนวงกลมบนพื้นแนวนอน XZ
    /// UWBManager ทำ calibration ส่วนคลาสนี้ทำ visual smoothing เท่านั้น
    /// </summary>
    [RequireComponent(typeof(Rigidbody), typeof(SphereCollider), typeof(SpriteRenderer))]
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

        [Header("Runtime (Read Only)")]
        [SerializeField] private bool isTracking;
        [SerializeField] private float sampleAgeSeconds = 999f;

        private Rigidbody body;
        private SpriteRenderer circleRenderer;
        private Vector3 targetPosition;
        private Vector3 smoothVelocity;
        private bool hasFirstPosition;
        private bool isRegistered;
        private Texture2D generatedCircleTexture;
        private Sprite generatedCircleSprite;

        public int PlayerId => playerId;
        public int TagId => tagId;
        public bool IsTracking => isTracking;
        public float SampleAgeSeconds => sampleAgeSeconds;

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            circleRenderer = GetComponent<SpriteRenderer>();
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
            if (!isRegistered)
            {
                FindAndRegisterManager();
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
                return;
            }

            if (Vector3.Distance(targetPosition, measuredPosition) >= positionDeadZone)
            {
                targetPosition = measuredPosition;
            }
        }

        private void FixedUpdate()
        {
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
        }

        /// <summary>เปลี่ยน Tag ตอน runtime และลงทะเบียน key ใหม่อย่างปลอดภัย</summary>
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

        /// <summary>
        /// ใช้โดย UWBPlayerSpawner เพื่อกำหนดผู้เล่นจาก array ใน Inspector
        /// เรียกได้ทั้งก่อนและหลัง Awake/OnEnable
        /// </summary>
        public void Configure(int newPlayerId, int newTagId, Color newColor)
        {
            playerId = Mathf.Max(1, newPlayerId);
            playerColor = newColor;
            SetTagId(newTagId);

            if (circleRenderer != null)
            {
                circleRenderer.color = playerColor;
            }

            gameObject.name = $"Player{playerId:00}_Tag{tagId}";
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
