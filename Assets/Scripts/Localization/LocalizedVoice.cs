using UnityEngine;

namespace FoodIsekaiZ.Localization
{
    // Picks the English or Thai voice clip for a dialogue AudioSource.
    // A line that is already playing restarts in the new language so voice and bubble text match.
    public sealed class LocalizedVoice : MonoBehaviour
    {
        [SerializeField] private AudioSource target;
        [SerializeField] private AudioClip english;
        [SerializeField] private AudioClip thai;

        private void Awake()
        {
            if (target == null) target = GetComponent<AudioSource>();
        }

        private void OnEnable()
        {
            GameLanguage.Changed += Apply;
            Apply(GameLanguage.Current);
        }

        private void OnDisable()
        {
            GameLanguage.Changed -= Apply;
        }

        private void Apply(GameLanguageId language)
        {
            if (target == null) return;
            AudioClip clip = language == GameLanguageId.Thai && thai != null ? thai : english;
            if (clip == null || target.resource == clip) return;
            bool wasPlaying = target.isPlaying;
            target.Stop();
            target.resource = clip;
            if (wasPlaying) target.Play();
        }
    }
}
