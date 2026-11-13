using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FoodIsekaiZ.Minigames.Views
{
    // One entry of the recipe on the Mix the Drink wall: a syrup jug and how many parts of it go in,
    // written in the syrup's colour.
    public sealed class MixDrinkRecipeJug : MonoBehaviour
    {
        [SerializeField] private Image jug;
        [SerializeField] private TMP_Text parts;

        public void Setup(Sprite jugPicture, int count, Color syrupColour)
        {
            jug.sprite = jugPicture;
            parts.text = $"×{count}";
            parts.color = syrupColour;
        }
    }
}
