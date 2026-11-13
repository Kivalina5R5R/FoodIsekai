using FoodIsekaiZ.Players;
using UnityEngine;

namespace FoodIsekaiZ.Minigames
{
    // One player as the minigames see them: where they stand in arena metres, how fast they move
    // (measured over a window of time, so a jittering UWB tag standing still does not count as
    // moving), and whether they are still in the game. Sample is called once per frame by the runner.
    public sealed class MinigamePlayer
    {
        // Kept long enough for SpeedOverWindow's 0.4 s at 20 Hz tags and 60 fps games.
        private const int HistoryLength = 120;

        private readonly UWBPlayerController controller;
        private readonly MinigameStage stage;
        private readonly Vector2[] positions = new Vector2[HistoryLength];
        private readonly float[] times = new float[HistoryLength];
        private int newest = -1;
        private int count;

        public MinigamePlayer(UWBPlayerController controller, MinigameStage stage)
        {
            this.controller = controller;
            this.stage = stage;
            PlayerId = controller.PlayerId;
            Colour = MinigameArt.PlayerColour(PlayerId);
        }

        public int PlayerId { get; }

        public Color Colour { get; }

        // "P1", "P2", ...
        public string Name => $"P{PlayerId}";

        // False while the player's tag is offline (their marker is hidden).
        public bool IsPresent => controller != null && controller.gameObject.activeInHierarchy;

        // Where the player's marker is in the scene, for authored zones that test world positions.
        public Vector3 WorldPosition => controller != null ? controller.transform.position : Vector3.zero;

        // Arena metres: x from the left, z from the wall.
        public Vector2 Position { get; private set; }

        public void Sample(float time)
        {
            if (controller == null) return;
            Position = stage.ToArena(controller.transform.position);
            newest = (newest + 1) % HistoryLength;
            positions[newest] = Position;
            times[newest] = time;
            count = Mathf.Min(count + 1, HistoryLength);
        }

        // Metres per second: the distance moved over the last window seconds, divided by window.
        public float SpeedOverWindow(float window = 0.4f)
        {
            if (count < 2) return 0f;
            float now = times[newest];
            int index = newest;
            for (int i = 1; i < count; i++)
            {
                int older = (newest - i + HistoryLength) % HistoryLength;
                index = older;
                if (now - times[older] >= window) break;
            }

            float elapsed = Mathf.Max(0.0001f, now - times[index]);
            return Vector2.Distance(positions[newest], positions[index]) / Mathf.Max(window, elapsed);
        }
    }
}
