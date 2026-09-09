using UnityEngine;
using UnityEngine.UI;

namespace FoodIsekaiZ.Display
{
    // A live bank balance selects the purse stage; deposits deform only its rendered vertices.
    public sealed class BankPurseImage : Image
    {
        [SerializeField] private Sprite[] moneyStages;
        [SerializeField] private int[] stageThresholds = { 0, 50, 150, 300 };
        [SerializeField, Min(0.1f)] private float pulseDuration = 0.8f;
        [SerializeField, Range(0f, 0.5f)] private float pulseScale = 0.28f;
        [SerializeField, Range(0f, 20f)] private float shakeDegrees = 10f;
        private float elapsed = float.PositiveInfinity;
        private int currentStage = -1;
        public int CurrentStage => currentStage;
        public bool IsPulsing => elapsed < pulseDuration;

        public void SetBalance(int balance)
        {
            if (moneyStages == null || moneyStages.Length == 0) return;
            int stage = 0;
            for (int i = 1; i < Mathf.Min(moneyStages.Length, stageThresholds.Length); i++)
                if (balance >= stageThresholds[i]) stage = i;
            if (stage == currentStage) return;
            currentStage = stage;
            sprite = moneyStages[stage];
        }

        public void Pulse()
        {
            elapsed = 0f;
            SetVerticesDirty();
        }

        protected override void OnDisable()
        {
            elapsed = float.PositiveInfinity;
            base.OnDisable();
        }

        private void Update() => Advance(Time.deltaTime);

        private void Advance(float deltaTime)
        {
            if (!IsPulsing) return;
            elapsed += Mathf.Max(0f, deltaTime);
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            base.OnPopulateMesh(mesh);
            if (!IsPulsing) return;
            float age = Mathf.Clamp01(elapsed / pulseDuration);
            float envelope = Mathf.Sin(age * Mathf.PI);
            float scale = 1f + pulseScale * envelope;
            float rotation = Mathf.Sin(age * Mathf.PI * 6f) * shakeDegrees * Mathf.Deg2Rad * envelope * (1f - age);
            Vector2 center = rectTransform.rect.center;
            UIVertex vertex = default;
            for (int i = 0; i < mesh.currentVertCount; i++)
            {
                mesh.PopulateUIVertex(ref vertex, i);
                Vector2 position = ((Vector2)vertex.position - center) * scale;
                vertex.position = center + new Vector2(position.x * Mathf.Cos(rotation) - position.y * Mathf.Sin(rotation),
                    position.x * Mathf.Sin(rotation) + position.y * Mathf.Cos(rotation) + envelope * 5f);
                mesh.SetUIVertex(vertex, i);
            }
        }
    }
}
