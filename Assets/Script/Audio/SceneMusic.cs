using UnityEngine;

namespace GameClub.Audio
{
    // Scene configuration only. Playback belongs to the persistent MusicManager.
    [DisallowMultipleComponent]
    public sealed class SceneMusic : MonoBehaviour
    {
        [SerializeField] private AudioClip music;
        [SerializeField, Range(0f, 1f)] private float volume = 0.5f;
        [SerializeField] private bool loop = true;
        [SerializeField, Min(0f)] private float fadeInDuration = 0.5f;
        [SerializeField, Min(0f)] private float fadeOutDuration = 0.3f;
        [SerializeField] private bool continueOnDeathReload = true;

        public AudioClip Music => music;
        public float Volume => Mathf.Clamp01(volume);
        public bool Loop => loop;
        public float FadeInDuration => Mathf.Max(0f, fadeInDuration);
        public float FadeOutDuration => Mathf.Max(0f, fadeOutDuration);
        public bool ContinueOnDeathReload => continueOnDeathReload;
    }
}
