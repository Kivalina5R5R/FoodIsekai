using System;
using UnityEngine;
using UnityEngine.UI;

namespace FoodIsekaiZ.Display
{
    // Applies the scene's per-menu artwork sizes before card transition effects.
    // The image's authored transform and the shared food sprite remain unchanged.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Image))]
    public sealed class MenuFoodSizeEffect : BaseMeshEffect
    {
        [Serializable]
        private sealed class MenuSize
        {
            [SerializeField] private Sprite sprite;
            [SerializeField, Range(0.1f, 1f)] private float scale = 1f;

            public Sprite Sprite => sprite;
            public float Scale => scale;
        }

        [SerializeField] private MenuSize[] menuSizes = Array.Empty<MenuSize>();

        public override void ModifyMesh(VertexHelper vertices)
        {
            if (!IsActive() || !(graphic is Image image)) return;
            Sprite sprite = image.overrideSprite;
            float scale = 1f;
            foreach (MenuSize size in menuSizes)
            {
                if (size == null || size.Sprite != sprite) continue;
                scale = Mathf.Clamp(size.Scale, 0.1f, 1f);
                break;
            }
            if (Mathf.Approximately(scale, 1f)) return;

            Vector3 center = image.GetPixelAdjustedRect().center;
            UIVertex vertex = default;
            for (int i = 0; i < vertices.currentVertCount; i++)
            {
                vertices.PopulateUIVertex(ref vertex, i);
                vertex.position = center + (vertex.position - center) * scale;
                vertices.SetUIVertex(vertex, i);
            }
        }
    }
}
