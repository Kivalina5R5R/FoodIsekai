using System.Collections.Generic;
using UnityEngine;

namespace FoodIsekaiZ.Minigames
{
    // The minigame pictures (kept in Assets/Art/Minigame/<game or Shared>), so the game can find
    // them by name at run time without the art living in a Resources folder. A picture's name is its
    // file name without the extension. The asset itself is Resources/MinigameArt.asset;
    // FoodIsekaiZ > Minigames > Refresh Prefab List refills it.
    [CreateAssetMenu(menuName = "FoodIsekaiZ/Minigame Art Library", fileName = "MinigameArt")]
    public sealed class MinigameArtLibrary : ScriptableObject
    {
        public const string ResourceName = "MinigameArt";

        [SerializeField] private List<Sprite> pictures = new List<Sprite>();

        // The picture whose name is name, or null.
        public Sprite Find(string name)
        {
            foreach (Sprite picture in pictures)
                if (picture != null && picture.name == name) return picture;
            return null;
        }

        // Replaces the list; used by the editor builder.
        public void SetPictures(IEnumerable<Sprite> found)
        {
            pictures = new List<Sprite>(found);
        }
    }
}
