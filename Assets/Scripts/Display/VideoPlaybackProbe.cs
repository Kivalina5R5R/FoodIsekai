using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Video;

namespace FoodIsekaiZ.Display
{
    // Temporary diagnostic: every five seconds of Play mode, logs for each playing video how fast it
    // really advanced compared with its clip's frame rate (100% is real time), the longest time its
    // picture stayed on one frame while playing, how many new pictures it showed and how many frames
    // it dropped. Starts by itself in the Editor; delete this file once the NPC video smoothness is settled.
    public sealed class VideoPlaybackProbe : MonoBehaviour
    {
        private const float Interval = 5f;

        private sealed class Track
        {
            public VideoClip Clip;
            public long LastFrame = -1;
            public long Advanced;
            public int Changes;
            public float PlayTime;
            public float StillTime;
            public float LongestStill;
            public int Drops;
        }

        private readonly Dictionary<VideoPlayer, Track> tracks = new Dictionary<VideoPlayer, Track>();
        private float elapsed;
        private int renderedFrames;

#if UNITY_EDITOR
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Begin()
        {
            GameObject probe = new GameObject(nameof(VideoPlaybackProbe));
            probe.hideFlags = HideFlags.HideAndDontSave;
            DontDestroyOnLoad(probe);
            probe.AddComponent<VideoPlaybackProbe>();
        }
#endif

        private void Update()
        {
            float delta = Time.unscaledDeltaTime;
            renderedFrames++;
            elapsed += delta;
            foreach (VideoPlayer player in FindObjectsByType<VideoPlayer>(FindObjectsSortMode.None))
            {
                Sample(player, delta);
            }
            if (elapsed >= Interval)
            {
                Report();
            }
        }

        private void Sample(VideoPlayer player, float delta)
        {
            if (!tracks.TryGetValue(player, out Track track))
            {
                track = new Track();
                tracks[player] = track;
                player.frameDropped += source => tracks[source].Drops++;
            }
            if (player.clip != track.Clip)
            {
                track.Clip = player.clip;
                track.LastFrame = -1;
            }
            if (!player.isPlaying || player.clip == null)
            {
                track.LastFrame = -1;
                return;
            }

            long frame = player.frame;
            track.PlayTime += delta;
            if (track.LastFrame >= 0 && frame != track.LastFrame)
            {
                long step = frame - track.LastFrame;
                if (step < 0)
                {
                    step += (long)player.frameCount;
                }
                track.Advanced += step;
                track.Changes++;
                track.StillTime = 0f;
            }
            else
            {
                track.StillTime += delta;
                track.LongestStill = Mathf.Max(track.LongestStill, track.StillTime);
            }
            track.LastFrame = frame;
        }

        private void Report()
        {
            StringBuilder report = new StringBuilder();
            report.Append($"[VideoProbe] game {renderedFrames / elapsed:0} fps");
            foreach (KeyValuePair<VideoPlayer, Track> pair in tracks)
            {
                VideoPlayer player = pair.Key;
                Track track = pair.Value;
                if (player != null && track.PlayTime > 0.5f && track.Clip != null)
                {
                    float speed = track.Advanced / (track.PlayTime * (float)track.Clip.frameRate) * 100f;
                    report.Append($"\n  {player.gameObject.name}/{track.Clip.name} @{track.Clip.frameRate:0}"
                        + $" speed {speed:0}% shown {track.Changes / track.PlayTime:0}/s longest freeze {track.LongestStill * 1000f:0} ms"
                        + $" dropped {track.Drops} over {track.PlayTime:0.0}s");
                }
                if (track != null)
                {
                    track.Advanced = 0;
                    track.Changes = 0;
                    track.PlayTime = 0f;
                    track.LongestStill = 0f;
                    track.Drops = 0;
                }
            }
            Debug.Log(report.ToString());
            elapsed = 0f;
            renderedFrames = 0;
        }
    }
}
