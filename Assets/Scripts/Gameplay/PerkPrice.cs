using UnityEngine;

namespace FoodIsekaiZ.Gameplay
{
    // The prefab owns the purchase price; UI text is only a view of this value.
    [DisallowMultipleComponent]
    public sealed class PerkPrice : MonoBehaviour
    {
        [SerializeField, Min(1)] private int price = 80;

        public int Price => price;
    }
}
