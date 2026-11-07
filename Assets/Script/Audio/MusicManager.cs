using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GameClub.Audio
{
    [DisallowMultipleComponent, RequireComponent(typeof(AudioSource))]
    public sealed class MusicManager : MonoBehaviour
    {
        public static MusicManager Instance { get; private set; }
        public AudioClip CurrentClip => source ? source.clip : null;
        public bool IsPlaying => source && source.isPlaying;

        private AudioSource source;
        private Coroutine fade;
        private float fadeOutDuration = 0.3f;
        private string currentScenePath;
        private string deathReloadPath;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            if (!Instance) new GameObject("Music Manager").AddComponent<MusicManager>();
        }

        private void Awake()
        {
            if (Instance && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            source = GetComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.dopplerLevel = 0f;
            source.Stop();
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDestroy()
        {
            if (Instance != this) return;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            Instance = null;
        }

        // Only the death-reload coordinator may request continuation for this exact scene.
        public void PrepareSceneLoad(string scenePath, bool isDeathReload)
        {
            deathReloadPath = isDeathReload ? scenePath : null;
        }

        // Starts alongside the visual fade. The coordinator can wait on the returned routine.
        public Coroutine FadeOutForTravel()
        {
            deathReloadPath = null;
            CancelFade();
            if (!source.clip) return null;
            fade = StartCoroutine(FadeVolume(0f, fadeOutDuration, true));
            return fade;
        }

        public void StopMusic()
        {
            CancelFade();
            source.Stop();
            source.clip = null;
            source.volume = 0f;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            // Initial Play Mode entry can use an internal load mode. Accept the first active
            // scene as well as Single loads; later additive content must not replace its music.
            bool initialActiveScene = currentScenePath == null && scene == SceneManager.GetActiveScene();
            if (mode != LoadSceneMode.Single && !initialActiveScene) return;
            bool deathReload = deathReloadPath == scene.path && currentScenePath == scene.path;
            deathReloadPath = null;
            currentScenePath = scene.path;

            SceneMusic settings = null;
            foreach (var root in scene.GetRootGameObjects())
                foreach (var candidate in root.GetComponentsInChildren<SceneMusic>(true))
                {
                    if (!candidate.isActiveAndEnabled) continue;
                    if (settings)
                    {
                        StopMusic();
                        Debug.LogError("Only one enabled SceneMusic is allowed in scene " + scene.name);
                        return;
                    }
                    settings = candidate;
                }

            if (!settings || !settings.Music)
            {
                StopMusic();
                return;
            }

            fadeOutDuration = settings.FadeOutDuration;
            if (deathReload && settings.ContinueOnDeathReload && source.clip == settings.Music)
            {
                // Do not call Play: preserve playback position, including a finished non-looping track.
                source.loop = settings.Loop;
                return;
            }

            StopMusic();
            source.clip = settings.Music;
            source.loop = settings.Loop;
            source.Play();
            fade = StartCoroutine(FadeVolume(settings.Volume, settings.FadeInDuration, false));
        }

        private void CancelFade()
        {
            if (fade != null) StopCoroutine(fade);
            fade = null;
        }

        private IEnumerator FadeVolume(float target, float duration, bool stopAfterFade)
        {
            float start = source.volume;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                source.volume = Mathf.Lerp(start, target, elapsed / duration);
                yield return null;
            }
            source.volume = target;
            if (stopAfterFade)
            {
                source.Stop();
                source.clip = null;
            }
            fade = null;
        }
    }
}
