using UnityEngine;

namespace FoodIsekaiZ.Minigames
{
    // Scene switch for the minigame rounds, on the MinigameManager object under System.
    // Turned off, no Minigame Runner is created and every meal goes straight to its break
    // (or the results after the last meal). The runner also sits under this object while playing.
    public sealed class MinigameManager : MonoBehaviour
    {
        [Tooltip("ปิดไว้ = ไม่เล่นมินิเกมเลย จบมื้อแล้วไปช่วงพักหรือหน้าผลลัพธ์ทันที")]
        [InspectorName("Play Minigames")]
        [SerializeField] private bool playMinigames = true;

        public bool PlayMinigames => playMinigames;
    }
}
