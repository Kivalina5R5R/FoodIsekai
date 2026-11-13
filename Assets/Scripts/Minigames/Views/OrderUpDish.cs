using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FoodIsekaiZ.Minigames.Views
{
    // A dish drawn in Order Up!: its picture with its name under it.
    public sealed class OrderUpDish : MonoBehaviour
    {
        [SerializeField] private Image food;
        [SerializeField] private TMP_Text dishName;

        public void Show(Sprite picture, Color tint, string name)
        {
            food.sprite = picture;
            food.color = tint;
            dishName.text = name;
        }
    }
}
