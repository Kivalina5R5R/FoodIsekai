using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

namespace FoodIsekaiZ.Display
{
    // How an NPC is drawn: its animated video clips, or its original still body image.
    public enum NpcLook
    {
        Animated,
        StillImage
    }

    // Plays an NPC's walk, idle and angry clips (transparent WebM videos) on an authored child RawImage.
    // Walking takes priority over angry, and angry over idle. An angry NPC that walks uses the angry
    // walk clip when it has one. When it stops walking it can play its arrive clip (such as ordering)
    // once before settling into idle. The RawImage and its placement over the body are authored in
    // the prefab; this component only creates the video players and render textures at runtime.
    // Two players take turns: the shown one keeps playing while the other loads the next clip, so a
    // change of clip never freezes on the last frame. The clip that usually comes next (the arrival
    // clip while walking, idle while arriving) is loaded ahead so those changes are immediate.
    // With Look set to Still Image the NPC shows its original body sprite instead, exactly as before
    // the animations: the emoji presentation then swaps to the angry sprite as it always did.
    [DisallowMultipleComponent]
    public sealed class NpcVideoAnimation : MonoBehaviour
    {
        // Chosen for all NPCs by the spawner's NPC Look setting (Map/SideDisplay), which applies it on spawn.
        [HideInInspector, SerializeField] private NpcLook look = NpcLook.Animated;
        [SerializeField] private RawImage frameImage;
        [SerializeField] private VideoClip walkClip;
        [SerializeField] private VideoClip idleClip;
        [SerializeField] private VideoClip angryClip;
        [SerializeField] private VideoClip angryWalkClip;
        [SerializeField] private VideoClip arriveClip;
        // How far the character rises in each idle frame, as a fraction of the frame height,
        // so attached UI such as the emoji bubble can float along. Empty when the idle stays grounded.
        [SerializeField] private float[] idleLift = System.Array.Empty<float>();
        // After each idle loop, hold the idle's first frame (the resting pose) for a random time
        // between these values, so a big idle gesture only plays now and then. Zero keeps looping.
        [SerializeField, Min(0f)] private float idleRestMinSeconds;
        [SerializeField, Min(0f)] private float idleRestMaxSeconds;
        // How long the old picture fades out when the clip changes or the idle returns to its
        // resting pose. Zero switches pictures at once.
        [SerializeField, Min(0f)] private float crossfadeSeconds = 0.3f;
        // A longer fade for stopping (walk to arrive or idle), where the whole pose changes.
        [SerializeField, Min(0f)] private float stopCrossfadeSeconds = 0.5f;
        // The slowest the walk clip plays while the NPC brakes, as a share of normal speed.
        [SerializeField, Range(0.05f, 1f)] private float slowestWalkPace = 0.35f;
        // On arrival the original still picture (the body image) shows for at least this long before
        // the idle clip takes over, so the NPC lands exactly on its authored pose. Zero skips it.
        [SerializeField, Min(0f)] private float arrivalStillSeconds = 0.05f;

        // Seeking back to the resting pose takes a moment, so the old picture stays this long before fading.
        private const float RestSeekHoldSeconds = 0.08f;

        private readonly VideoPlayer[] players = new VideoPlayer[2];
        private VideoFrameCrossfade crossfade;
        private readonly RenderTexture[] textures = new RenderTexture[2];
        private int shown;
        private int switching = -1;
        private VideoClip currentClip;
        private float restRemaining;
        private bool walking;
        private bool angry;
        private bool arrivePending;
        private bool holdingStill;
        private float stillRemaining;
        private float stillFadeRemaining;

        private bool Animated => look == NpcLook.Animated && frameImage != null;

        // The child image that shows the video, for effects that mirror the NPC's look.
        // Null when the NPC shows its still image.
        public RawImage FrameImage => Animated ? frameImage : null;

        // How far the character is raised right now, in the frame image's parent space (the body).
        public float CurrentLift
        {
            get
            {
                VideoPlayer player = players[shown];
                if (!Animated || player == null || player.clip != idleClip || idleLift.Length == 0)
                {
                    return 0f;
                }
                long frame = restRemaining > 0f ? 0 : player.frame;
                if (frame < 0 || frame >= idleLift.Length)
                {
                    return 0f;
                }
                return idleLift[frame] * frameImage.rectTransform.rect.height;
            }
        }

        // True when this NPC has an angry clip, so the emoji presentation can skip its sprite swap.
        public bool HasAngryClip => Animated && angryClip != null;

        // Switches between the video clips and the original still body image.
        public void SetLook(NpcLook value)
        {
            if (look == value)
            {
                return;
            }

            look = value;
            ApplyLook();
            if (Animated)
            {
                EnsurePlayers();
                currentClip = null;
                SelectClip();
                return;
            }

            foreach (VideoPlayer player in players)
            {
                if (player != null)
                {
                    player.Stop();
                }
            }
            currentClip = null;
            restRemaining = 0f;
            crossfade?.Stop();
        }

        public void SetWalking(bool value)
        {
            // Stopping after a walk plays the arrive clip once; walking again cancels it.
            bool stopping = walking && !value;
            arrivePending = stopping && arriveClip != null;
            walking = value;
            if (stopping)
            {
                BeginArrivalStill();
            }
            else if (value && (holdingStill || stillFadeRemaining > 0f))
            {
                // Walking off again before the idle took over: back to the video at once.
                ApplyLook();
            }
            SelectClip();
        }

        public void SetAngry(bool value)
        {
            angry = value;
            SelectClip();
        }

        // Slows the walk clip while the NPC brakes, so the steps shorten with the body's speed.
        // Pace is the NPC's speed as a share of its walking speed (1 walks normally).
        public void SetWalkPace(float pace)
        {
            VideoPlayer player = players[shown];
            if (player == null || !IsWalkClip(currentClip))
            {
                return;
            }
            player.playbackSpeed = Mathf.Lerp(slowestWalkPace, 1f, Mathf.Clamp01(pace));
        }

        private bool IsWalkClip(VideoClip clip)
        {
            return clip != null && (clip == walkClip || clip == angryWalkClip);
        }

        private void OnEnable()
        {
            ApplyLook();
            if (!Animated)
            {
                return;
            }
            EnsurePlayers();
            SelectClip();
        }

        // Shows either the video image or the original body image under it.
        private void ApplyLook()
        {
            if (frameImage == null)
            {
                return;
            }
            holdingStill = false;
            stillFadeRemaining = 0f;
            Image body = BodyImage();
            frameImage.gameObject.SetActive(Animated);
            if (body != null)
            {
                body.canvasRenderer.SetAlpha(1f);
                body.enabled = !Animated;
            }
        }

        private Image BodyImage()
        {
            Transform parent = frameImage != null ? frameImage.transform.parent : null;
            return parent != null ? parent.GetComponent<Image>() : null;
        }

        // Arriving: the last walking picture fades over the original still picture, which stays
        // until the idle clip is ready (and at least the still time).
        private void BeginArrivalStill()
        {
            Image body = BodyImage();
            if (!Animated || arrivalStillSeconds <= 0f || body == null)
            {
                return;
            }
            crossfade?.Begin(0f, stopCrossfadeSeconds);
            frameImage.gameObject.SetActive(false);
            body.canvasRenderer.SetAlpha(1f);
            body.enabled = true;
            holdingStill = true;
            stillRemaining = arrivalStillSeconds;
            stillFadeRemaining = 0f;
        }

        // The idle clip takes over: the video shows again and the still picture under it fades away.
        private void EndArrivalStill()
        {
            holdingStill = false;
            frameImage.gameObject.SetActive(true);
            stillFadeRemaining = crossfadeSeconds;
            if (stillFadeRemaining <= 0f)
            {
                HideStill();
            }
        }

        private void TickArrivalStill(float deltaTime)
        {
            if (holdingStill)
            {
                stillRemaining -= deltaTime;
                // Wait for the incoming clip's first frame, so the video never shows the old clip.
                if (stillRemaining <= 0f && switching < 0)
                {
                    EndArrivalStill();
                }
                return;
            }
            if (stillFadeRemaining <= 0f)
            {
                return;
            }
            stillFadeRemaining -= deltaTime;
            Image body = BodyImage();
            if (stillFadeRemaining <= 0f || body == null)
            {
                HideStill();
                return;
            }
            body.canvasRenderer.SetAlpha(Mathf.SmoothStep(0f, 1f, stillFadeRemaining / crossfadeSeconds));
        }

        private void HideStill()
        {
            stillFadeRemaining = 0f;
            Image body = BodyImage();
            if (body != null && Animated)
            {
                body.enabled = false;
                body.canvasRenderer.SetAlpha(1f);
            }
        }

        private void OnDestroy()
        {
            crossfade?.Dispose();
            foreach (RenderTexture texture in textures)
            {
                if (texture == null)
                {
                    continue;
                }
                texture.Release();
                Destroy(texture);
            }
        }

        private void EnsurePlayers()
        {
            if (players[0] != null || frameImage == null)
            {
                return;
            }

            VideoClip sizeSource = idleClip != null ? idleClip : walkClip;
            if (sizeSource == null)
            {
                return;
            }

            for (int i = 0; i < players.Length; i++)
            {
                textures[i] = new RenderTexture((int)sizeSource.width, (int)sizeSource.height, 0,
                    RenderTextureFormat.ARGB32);
                // The NPC shows several times smaller than the video, so smaller copies (mipmaps) keep
                // the outline smooth instead of sparkling and breaking up.
                textures[i].useMipMap = true;
                textures[i].autoGenerateMips = true;
                textures[i].filterMode = FilterMode.Trilinear;
                textures[i].Create();
                // Start transparent so nothing shows before the first video frame arrives.
                RenderTexture previous = RenderTexture.active;
                RenderTexture.active = textures[i];
                GL.Clear(false, true, Color.clear);
                RenderTexture.active = previous;

                VideoPlayer player = gameObject.AddComponent<VideoPlayer>();
                player.playOnAwake = false;
                player.renderMode = VideoRenderMode.RenderTexture;
                player.targetTexture = textures[i];
                player.audioOutputMode = VideoAudioOutputMode.None;
                player.waitForFirstFrame = true;
                // Keep real time: when decoding falls behind, drop a frame instead of slowing the clip down.
                player.skipOnDrop = true;
                player.loopPointReached += HandleClipEnded;
                player.frameReady += HandleFrameReady;
                players[i] = player;
            }
            frameImage.texture = textures[shown];
            crossfade = new VideoFrameCrossfade(frameImage, crossfadeSeconds);
        }

        private void SelectClip()
        {
            if (players[0] == null || !Animated)
            {
                return;
            }

            VideoClip next = PickClip();
            if (next == null || next == currentClip)
            {
                return;
            }

            currentClip = next;
            restRemaining = 0f;
            int target = 1 - shown;
            VideoPlayer player = players[target];
            if (player.clip != next)
            {
                player.clip = next;
            }
            player.isLooping = LoopsOnItsOwn(next);
            player.playbackSpeed = 1f;
            player.frame = 0;
            // Swap once the new clip has drawn its first frame; until then the shown clip keeps playing.
            switching = target;
            player.sendFrameReadyEvents = true;
            player.Play();
        }

        // The arrive clip plays once; idle with rest times stops at its end to rest; the rest loop.
        private bool LoopsOnItsOwn(VideoClip clip)
        {
            return clip != arriveClip && !(clip == idleClip && idleRestMaxSeconds > 0f);
        }

        private void HandleFrameReady(VideoPlayer source, long frame)
        {
            int index = source == players[0] ? 0 : 1;
            if (index == switching)
            {
                CompleteSwitch(index);
            }
        }

        private void CompleteSwitch(int index)
        {
            players[index].sendFrameReadyEvents = false;
            switching = -1;
            int old = shown;
            shown = index;
            // The outgoing clip's last picture fades out over the new clip, more slowly when stopping.
            bool stopping = IsWalkClip(players[old].clip) && !IsWalkClip(currentClip);
            crossfade?.Begin(0f, stopping ? stopCrossfadeSeconds : crossfadeSeconds);
            frameImage.texture = textures[index];
            players[old].Stop();
            PreloadNext();
        }

        // Loads the clip that usually follows into the spare player: the arrival clip while walking,
        // and idle while the arrive clip plays.
        private void PreloadNext()
        {
            VideoClip upcoming = null;
            if (walking)
            {
                upcoming = arriveClip != null ? arriveClip : idleClip;
            }
            else if (currentClip == arriveClip)
            {
                upcoming = idleClip;
            }
            VideoPlayer spare = players[1 - shown];
            if (upcoming == null || (spare.clip == upcoming && spare.isPrepared))
            {
                return;
            }
            spare.clip = upcoming;
            spare.Prepare();
        }

        private VideoClip PickClip()
        {
            if (walking && angry && angryWalkClip != null)
            {
                return angryWalkClip;
            }
            if (walking && walkClip != null)
            {
                return walkClip;
            }
            if (angry && angryClip != null)
            {
                return angryClip;
            }
            if (arrivePending)
            {
                return arriveClip;
            }
            return idleClip != null ? idleClip : walkClip;
        }

        private void HandleClipEnded(VideoPlayer source)
        {
            if (source != players[shown])
            {
                return;
            }

            if (currentClip == arriveClip)
            {
                arrivePending = false;
                SelectClip();
                return;
            }

            if (currentClip == idleClip && idleRestMaxSeconds > 0f)
            {
                // Hold the resting pose (the first frame) before the next idle gesture.
                restRemaining = Random.Range(idleRestMinSeconds, Mathf.Max(idleRestMinSeconds, idleRestMaxSeconds));
                // The gesture's last picture fades into the resting pose instead of jumping to it.
                crossfade?.Begin(RestSeekHoldSeconds);
                source.Pause();
                source.frame = 0;
            }
        }

        private void Update()
        {
            crossfade?.Tick(Time.deltaTime);
            TickArrivalStill(Time.deltaTime);
            if (restRemaining <= 0f || players[shown] == null)
            {
                return;
            }

            restRemaining -= Time.deltaTime;
            if (restRemaining <= 0f)
            {
                players[shown].Play();
            }
        }
    }
}
