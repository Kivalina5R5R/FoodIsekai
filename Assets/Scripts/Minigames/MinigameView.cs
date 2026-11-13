using UnityEngine;

namespace FoodIsekaiZ.Minigames
{
    // The authored look of one minigame, kept as a prefab in Assets/Prefab/Minigame/<name>.prefab
    // and listed in Resources/MinigameViews so the game can find it by name.
    // The prefab has two children: Wall (laid out on the 1536 x 435 wall) and Floor (laid out on the
    // 1100 x 500 floor canvas, 100 units per metre, canvas up = towards the wall). When the game
    // begins, Wall goes on the wall layer and Floor moves to the floor layer.
    // Everything design-related (positions, sizes, sprites, colours, fonts) lives in the prefab or in
    // the view's serialized fields; the game only tells the view what is happening. Things made per
    // player or per item are cloned from inactive templates inside the prefab.
    public abstract class MinigameView : MonoBehaviour
    {
        [SerializeField] private RectTransform wall;
        [SerializeField] private RectTransform floor;

        protected MinigameStage Stage { get; private set; }

        public RectTransform Wall => wall;
        public RectTransform Floor => floor;

        // Called once the view sits in the stage, before the game uses it.
        // Parts marked MinigameHiddenAtStart are switched off and editing guides removed first, so
        // the view starts from the same state whatever was left showing in the prefab.
        public void Bind(MinigameStage stage)
        {
            Stage = stage;
            PrepareForPlay(transform);
            if (floor != null && !floor.IsChildOf(transform)) PrepareForPlay(floor);
            OnBound();
        }

        protected virtual void OnBound()
        {
        }

        private static void PrepareForPlay(Transform root)
        {
            foreach (MinigameEditorGuide guide in root.GetComponentsInChildren<MinigameEditorGuide>(true))
            {
                guide.gameObject.SetActive(false);
                Destroy(guide.gameObject);
            }

            foreach (MinigameHiddenAtStart hidden in root.GetComponentsInChildren<MinigameHiddenAtStart>(true))
                hidden.gameObject.SetActive(false);
        }

        // An active copy of an inactive template, under the template's own parent unless parent is given.
        protected T Spawn<T>(T template, Transform parent = null) where T : Component
        {
            T copy = Instantiate(template, parent != null ? parent : template.transform.parent, false);
            copy.gameObject.SetActive(true);
            return copy;
        }

        // Moves a floor drawing so its centre sits on this arena point (metres; x from the left, z from the wall).
        protected void PlaceOnFloor(RectTransform rect, Vector2 arena)
        {
            rect.anchoredPosition = Stage.FloorPoint(arena);
        }
    }
}
