using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FoodIsekaiZ.Minigames.Views
{
    // One dish drawn on the What's Missing? tray or floor tile: its picture (or plain coloured shape)
    // with its name below.
    public sealed class MissingTrayDishSlot : MonoBehaviour
    {
        [SerializeField] private Image food;
        [SerializeField] private TMP_Text dishName;

        public void Show(MissingTrayDishArt art, string name)
        {
            food.sprite = art.Sprite;
            food.color = art.Colour;
            // Pictures keep their shape; the plain square fallback fills its box.
            food.preserveAspect = art.Sprite != null;
            dishName.text = name;
        }
    }
}
