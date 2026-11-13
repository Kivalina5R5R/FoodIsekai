using System;
using UnityEngine;

namespace FoodIsekaiZ.Minigames.Views
{
    // How one dish of What's Missing? looks, found by its key (the dish's English name): its picture
    // in white, or a plain shape in the dish's colour until the picture is added.
    [Serializable]
    public sealed class MissingTrayDishArt
    {
        [SerializeField] private string key;
        [SerializeField] private Sprite sprite;
        [SerializeField] private Color colour = Color.white;

        public MissingTrayDishArt(string key, Sprite sprite, Color colour)
        {
            this.key = key;
            this.sprite = sprite;
            this.colour = colour;
        }

        public string Key => key;
        public Sprite Sprite => sprite;
        public Color Colour => colour;
    }
}
