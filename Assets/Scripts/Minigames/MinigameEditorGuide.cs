using UnityEngine;

namespace FoodIsekaiZ.Minigames
{
    // Marks a backdrop that only helps while editing a minigame prefab (the outline of the wall or
    // of the floor). It is removed when the game starts.
    [DisallowMultipleComponent]
    public sealed class MinigameEditorGuide : MonoBehaviour
    {
    }
}
