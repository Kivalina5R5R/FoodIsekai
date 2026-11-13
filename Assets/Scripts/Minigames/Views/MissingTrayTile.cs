using UnityEngine;
using UnityEngine.UI;

namespace FoodIsekaiZ.Minigames.Views
{
    // One answer tile on the What's Missing? floor: a plate with a name chip and the dish on it.
    // The plate is faint while the tray is on show and bright once players may answer.
    public sealed class MissingTrayTile : MonoBehaviour
    {
        [SerializeField] private Image plate;
        [SerializeField] private MissingTrayDishSlot dish;
        [SerializeField] private Color dimColour = new Color(1f, 1f, 1f, 0.4f);
        [SerializeField] private Color brightColour = Color.white;

        public void Show(MissingTrayDishArt art, string name)
        {
            dish.Show(art, name);
            plate.color = dimColour;
        }

        public void Brighten()
        {
            plate.color = brightColour;
        }
    }
}
