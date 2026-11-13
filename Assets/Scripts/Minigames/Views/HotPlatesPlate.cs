using UnityEngine;
using UnityEngine.UI;

namespace FoodIsekaiZ.Minigames.Views
{
    // One plate on the Hot Plates floor, placed in the prefab: the plate picture is both the look
    // and the zone (a circle filling its box). colourIndex picks its colour from the view's list and
    // says which call keeps it safe.
    public sealed class HotPlatesPlate : MonoBehaviour
    {
        [SerializeField] private Image dish;
        [SerializeField] private int colourIndex;

        public int ColourIndex => colourIndex;

        public RectTransform Area => dish.rectTransform;

        public void Paint(Color colour)
        {
            dish.color = colour;
        }
    }
}
