using FoodIsekaiZ.Gameplay;
using UnityEngine;
using UnityEngine.Sprites;
using UnityEngine.UI;

namespace FoodIsekaiZ.Display
{
    // Animates a temporary food illustration or an appearing reward without moving authored scene objects.
    public sealed class FloorAnimatedSprite : MaskableGraphic
    {
        private enum MotionKind { Pickup, Delivery, Reward }

        [SerializeField] private MotionKind motion;
        [SerializeField] private Sprite[] foodSprites;
        [SerializeField] private Sprite rewardSprite;
        [SerializeField, Min(0.1f)] private float duration = 0.85f;
        [SerializeField] private Vector2 spriteSize = new Vector2(108f, 108f);
        [SerializeField, Min(0f)] private float arcHeight = 24f;

        [Header("Uncollected Money Reminder")]
        [SerializeField, Min(1f)] private float reminderDelay = 6f;
        [SerializeField, Min(1f)] private float reminderInterval = 5f;
        [SerializeField, Min(0.1f)] private float reminderDuration = 0.95f;
        [SerializeField, Range(0f, 0.5f)] private float reminderGrowth = 0.25f;
        [SerializeField, Range(0f, 20f)] private float reminderShakeDegrees = 8f;
        private float rewardAge;
        private float ReminderPhase => (rewardAge - reminderDelay) % Mathf.Max(reminderInterval, reminderDuration + 0.1f);
        private int ReminderCycle => rewardAge < reminderDelay ? -1 :
            Mathf.FloorToInt((rewardAge - reminderDelay) / Mathf.Max(reminderInterval, reminderDuration + 0.1f));
        // Raised once when a visible reward starts a new reminder shake.
        public event System.Action ReminderStarted;
        public bool IsReminding => motion == MotionKind.Reward && visible && rewardAge >= reminderDelay && ReminderPhase < reminderDuration;

        private Sprite currentSprite;
        private Vector2 start;
        private Vector2 destination;
        private float elapsed;
        private bool visible;
        private bool animating;

        public bool IsAnimating => animating || IsReminding;
        public override Texture mainTexture => currentSprite != null ? currentSprite.texture : s_WhiteTexture;

        protected override void OnEnable()
        {
            base.OnEnable();
            if (motion == MotionKind.Reward && rewardSprite != null)
            {
                Begin(rewardSprite);
            }
        }

        protected override void OnDisable()
        {
            Stop();
            base.OnDisable();
        }

        // Projects the player's floor position into this fixed canvas and starts a bounded handoff arc.
        public void PlayFood(FoodType food, Vector3 playerPosition)
        {
            int index = (int)food - (int)FoodType.Food1;
            if (motion == MotionKind.Reward || foodSprites == null || index < 0 ||
                index >= foodSprites.Length || foodSprites[index] == null)
            {
                return;
            }

            Vector3 localPlayer = transform.InverseTransformPoint(playerPosition);
            Vector2 playerAnchor = Vector2.ClampMagnitude(new Vector2(localPlayer.x, localPlayer.y), 85f);
            if (playerAnchor.sqrMagnitude < 22f * 22f)
            {
                playerAnchor = new Vector2(0f, motion == MotionKind.Pickup ? 52f : -52f);
            }

            start = motion == MotionKind.Pickup ? Vector2.zero : playerAnchor;
            destination = motion == MotionKind.Pickup ? playerAnchor : Vector2.zero;
            Begin(foodSprites[index]);
        }

        // Clears transient geometry when feedback is disabled or a reward is collected.
        public void Stop()
        {
            visible = false;
            animating = false;
            elapsed = 0f;
            rewardAge = 0f;
            SetVerticesDirty();
        }

        private void Begin(Sprite sprite)
        {
            currentSprite = sprite;
            elapsed = 0f;
            rewardAge = 0f;
            visible = true;
            animating = true;
            SetMaterialDirty();
            SetVerticesDirty();
        }

        private void Update()
        {
            Advance(Time.deltaTime);
        }

        private void Advance(float deltaTime)
        {
            if (deltaTime <= 0f) return;
            if (motion == MotionKind.Reward && visible)
            {
                bool wasReminding = IsReminding;
                int previousCycle = ReminderCycle;
                rewardAge += deltaTime;
                if (IsReminding && ReminderCycle != previousCycle) ReminderStarted?.Invoke();
                if (wasReminding || IsReminding) SetVerticesDirty();
            }
            if (!animating) return;

            elapsed = Mathf.Min(elapsed + deltaTime, Mathf.Max(0.1f, duration));
            if (elapsed >= Mathf.Max(0.1f, duration))
            {
                animating = false;
                visible = motion == MotionKind.Reward;
            }

            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper helper)
        {
            helper.Clear();
            if (!visible || currentSprite == null)
            {
                return;
            }

            float age = Mathf.Clamp01(elapsed / Mathf.Max(0.1f, duration));
            float scale;
            float alpha;
            float tilt;
            Vector2 center;
            Vector2 shadow;
            if (motion == MotionKind.Reward)
            {
                float grow = Mathf.Clamp01(age / 0.65f);
                float back = grow - 1f;
                scale = 0.4f + 0.6f * (1f + 2.7f * back * back * back + 1.7f * back * back);
                center = new Vector2(0f, Mathf.Sin(age * Mathf.PI) * 9f);
                shadow = new Vector2(2f, -3f);
                alpha = Mathf.Clamp01(age / 0.12f);
                tilt = Mathf.Sin(age * Mathf.PI * 2f) * 0.065f * (1f - age);
                if (IsReminding)
                {
                    float reminder = Mathf.Clamp01(ReminderPhase / Mathf.Max(0.1f, reminderDuration));
                    float envelope = Mathf.Sin(reminder * Mathf.PI);
                    scale *= 1f + reminderGrowth * envelope;
                    tilt += Mathf.Sin(reminder * Mathf.PI * 6f) * reminderShakeDegrees * Mathf.Deg2Rad * envelope;
                    center.y += envelope * 5f;
                }
            }
            else
            {
                bool pickup = motion == MotionKind.Pickup;
                float travel = pickup ? Mathf.InverseLerp(0.14f, 0.94f, age) : Mathf.Clamp01(age / 0.7f);
                float ease = 1f - Mathf.Pow(1f - travel, 3f);
                Vector2 ground = Vector2.Lerp(start, destination, ease);
                center = ground + Vector2.up * (Mathf.Sin(travel * Mathf.PI) * arcHeight);
                shadow = ground + new Vector2(3f, -4f);
                float pop = Mathf.Sin(Mathf.Clamp01(age / 0.32f) * Mathf.PI);
                scale = pickup ? Mathf.Lerp(0.82f + pop * 0.38f, 0.5f, travel * travel)
                    : 0.82f + 0.22f * Mathf.Sin(travel * Mathf.PI * 0.5f)
                        + Mathf.Sin(Mathf.InverseLerp(0.7f, 1f, age) * Mathf.PI) * 0.1f;
                alpha = Mathf.Clamp01(age / 0.07f) * (1f - Mathf.SmoothStep(0f, 1f,
                    Mathf.InverseLerp(pickup ? 0.66f : 0.76f, 1f, age)));
                tilt = Mathf.Sin(travel * Mathf.PI) * (pickup ? -0.13f : 0.11f);
            }

            float fit = Mathf.Min(spriteSize.x / currentSprite.rect.width, spriteSize.y / currentSprite.rect.height);
            Vector2 size = currentSprite.rect.size * fit * scale;
            DrawSprite(helper, shadow, size * 0.94f, tilt, new Color(0.15f, 0.07f, 0.025f, alpha * 0.2f));
            Color tint = color;
            tint.a *= alpha;
            DrawSprite(helper, center, size, tilt, tint);
        }

        private void DrawSprite(VertexHelper helper, Vector2 center, Vector2 size, float rotation, Color tint)
        {
            Vector4 uv = DataUtility.GetOuterUV(currentSprite);
            Vector4 padding = DataUtility.GetPadding(currentSprite);
            Vector2 rectSize = currentSprite.rect.size;
            float left = padding.x / rectSize.x - 0.5f;
            float bottom = padding.y / rectSize.y - 0.5f;
            float rightEdge = 0.5f - padding.z / rectSize.x;
            float top = 0.5f - padding.w / rectSize.y;
            int first = helper.currentVertCount;
            Vector2 right = new Vector2(Mathf.Cos(rotation), Mathf.Sin(rotation)) * size.x;
            Vector2 up = new Vector2(-Mathf.Sin(rotation), Mathf.Cos(rotation)) * size.y;
            // Unity may trim the sprite's texture region; retain its original padding like a UI Image.
            AddVertex(helper, center + right * left + up * bottom, new Vector2(uv.x, uv.y), tint);
            AddVertex(helper, center + right * left + up * top, new Vector2(uv.x, uv.w), tint);
            AddVertex(helper, center + right * rightEdge + up * top, new Vector2(uv.z, uv.w), tint);
            AddVertex(helper, center + right * rightEdge + up * bottom, new Vector2(uv.z, uv.y), tint);
            helper.AddTriangle(first, first + 1, first + 2);
            helper.AddTriangle(first, first + 2, first + 3);
        }

        private static void AddVertex(VertexHelper helper, Vector2 position, Vector2 uv, Color tint)
        {
            UIVertex vertex = UIVertex.simpleVert;
            vertex.position = position;
            vertex.uv0 = uv;
            vertex.color = tint;
            helper.AddVert(vertex);
        }
    }
}
