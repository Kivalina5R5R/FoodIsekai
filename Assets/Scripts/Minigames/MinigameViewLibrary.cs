using System.Collections.Generic;
using UnityEngine;

namespace FoodIsekaiZ.Minigames
{
    // The list of minigame view prefabs (kept in Assets/Prefab/Minigame), so the game can find them by
    // name without the prefabs living in a Resources folder. The asset itself is
    // Resources/MinigameViews.asset; FoodIsekaiZ > Minigames > Build Missing Prefabs refills it.
    [CreateAssetMenu(menuName = "FoodIsekaiZ/Minigame View Library", fileName = "MinigameViews")]
    public sealed class MinigameViewLibrary : ScriptableObject
    {
        public const string ResourceName = "MinigameViews";

        [SerializeField] private List<MinigameView> views = new List<MinigameView>();

        // The prefab whose name is name, or null.
        public MinigameView Find(string name)
        {
            foreach (MinigameView view in views)
                if (view != null && view.name == name) return view;
            return null;
        }

        // Replaces the list; used by the editor builder.
        public void SetViews(IEnumerable<MinigameView> found)
        {
            views = new List<MinigameView>(found);
        }
    }
}
