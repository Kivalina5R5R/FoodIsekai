using UnityEngine;

namespace FoodIsekaiZ.Minigames.Views
{
    // One Order Up! crate on the floor: the crate picture with its number, and the dish inside,
    // which shows on top of the crate only while it is open.
    public sealed class OrderUpCrate : MonoBehaviour
    {
        [SerializeField] private OrderUpDish dish;

        public OrderUpDish Dish => dish;

        public void SetOpen(bool open)
        {
            dish.gameObject.SetActive(open);
        }
    }
}
