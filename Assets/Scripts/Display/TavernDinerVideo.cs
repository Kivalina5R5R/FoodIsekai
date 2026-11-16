using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

namespace FoodIsekaiZ.Display
{
    // Plays one looping table clip into its RawImage and fades the table group in or out.
    [RequireComponent(typeof(VideoPlayer))]
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class TavernDinerVideo : MonoBehaviour
    {
        [SerializeField] private RawImage target;
        [SerializeField] private float fadeSeconds = 0.8f;

        private VideoPlayer player;
        private CanvasGroup group;
        private RenderTexture texture;
        private bool shown;

        public bool IsShown => shown;

        private void Awake()
        {
            CacheComponents();
            group.alpha = 0f;
            if (target != null) target.enabled = false;
        }

        // The parent display can call in from its own OnEnable, before this Awake has run.
        private void CacheComponents()
        {
            if (player != null) return;
            player = GetComponent<VideoPlayer>();
            group = GetComponent<CanvasGroup>();
        }

        private void OnDestroy()
        {
            if (texture == null) return;
            texture.Release();
            Destroy(texture);
        }

        // Starts the clip and fades in once its first frame is ready.
        public void Show()
        {
            if (shown) return;
            CacheComponents();
            shown = true;
            EnsureTexture();
            player.Play();
        }

        // Fades out, then pauses decoding while hidden.
        public void Hide()
        {
            shown = false;
        }

        // Swaps in the given meal's table clip. Only call it while the table is hidden; a clip of
        // another size gets a new render texture the next time the table is shown.
        public void UseClip(VideoClip clip)
        {
            CacheComponents();
            if (clip == null || player.clip == clip) return;
            player.Stop();
            player.clip = clip;
            if (texture == null || (texture.width == (int)clip.width && texture.height == (int)clip.height)) return;
            player.targetTexture = null;
            texture.Release();
            Destroy(texture);
            texture = null;
        }

        // Hides immediately without a fade, used when a new meal starts behind a transition.
        public void HideInstantly()
        {
            CacheComponents();
            shown = false;
            group.alpha = 0f;
            if (target != null) target.enabled = false;
            if (player.isPlaying) player.Pause();
        }

        private void Update()
        {
            bool frameReady = player.isPrepared && player.frame >= 0;
            float goal = shown && frameReady ? 1f : 0f;
            float step = fadeSeconds > 0f ? Time.deltaTime / fadeSeconds : 1f;
            group.alpha = Mathf.MoveTowards(group.alpha, goal, step);

            bool visible = group.alpha > 0f;
            if (target != null && target.enabled != visible) target.enabled = visible;
            if (!shown && !visible && player.isPlaying) player.Pause();
        }

        private void EnsureTexture()
        {
            if (texture != null || player.clip == null) return;
            texture = new RenderTexture((int)player.clip.width, (int)player.clip.height, 0)
            {
                name = name + " Video"
            };
            player.renderMode = VideoRenderMode.RenderTexture;
            player.targetTexture = texture;
            if (target != null) target.texture = texture;
        }
    }
}
