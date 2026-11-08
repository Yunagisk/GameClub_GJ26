using System;
using System.Collections;
using System.Collections.Generic;
using Cinemachine;
using GameClub.Audio;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GameClub.Player
{
    [RequireComponent(typeof(TransitionOverlay))]
    public sealed class LevelSession : MonoBehaviour
    {
        public static LevelSession Instance { get; private set; }

        [Min(0f)] public float fadeOutDuration = 0.15f;
        [Min(0f)] public float fadeInDuration = 0.15f;
        public string CurrentCheckpointId => HasRespawnHandler ? respawnHandler.CurrentCheckpointId : snapshot == null ? null : snapshot.checkpointId;
        public bool IsRestoring { get; private set; } = true;
        public bool CanSave => !busy && !pendingDeath && snapshot != null && player && player.CanControl;
        public bool CanTravel => !busy && !pendingDeath && string.IsNullOrEmpty(pendingDestination);

        private ILevelRespawnHandler respawnHandler;
        private MonoBehaviour respawnHandlerBehaviour;
        private bool HasRespawnHandler => respawnHandlerBehaviour && respawnHandler != null;
        private LevelSnapshot snapshot;
        private Dictionary<string, SaveableEntity> entities;
        private PlayerLifecycle player;
        private PlayerLifecycle pendingDeath;
        private string pendingDestination;
        private bool pendingStartGame;
        private float startFadeOut, startFadeIn, startMinimumBlack;
        private bool busy;
        private bool internalLoad;
        private TransitionOverlay overlay;
        private SimulationMode2D normalSimulation;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance) return;
            new GameObject("Level Session").AddComponent<LevelSession>();
        }

        private void Awake()
        {
            if (Instance && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            overlay = GetComponent<TransitionOverlay>();
            normalSimulation = Physics2D.simulationMode;
            SceneManager.sceneLoaded += OnSceneLoaded;
            Physics2D.simulationMode = SimulationMode2D.Script;
        }

        private void OnDestroy()
        {
            if (Instance != this) return;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            Physics2D.simulationMode = normalSimulation;
            Instance = null;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!internalLoad) StartCoroutine(EnterLoadedScene(scene));
        }

        private IEnumerator EnterLoadedScene(Scene scene)
        {
            busy = true;
            overlay.BlocksInput = true;
            IsRestoring = true;
            Physics2D.simulationMode = SimulationMode2D.Script;
            yield return null; // Allow scene Start methods to finish while simulation is suspended.
            if (!TryInitialize(scene, null))
            {
                FinishFailure();
                yield break;
            }
            yield return Reveal();
        }

        private void Update()
        {
            if (busy) return;
            if (pendingDeath)
            {
                var dead = pendingDeath;
                pendingDeath = null;
                pendingDestination = null;
                pendingStartGame = false;
                StartCoroutine(Reload(dead));
            }
            else if (!string.IsNullOrEmpty(pendingDestination))
            {
                string destination = pendingDestination;
                bool startingGame = pendingStartGame;
                pendingDestination = null;
                pendingStartGame = false;
                StartCoroutine(Travel(destination, startingGame));
            }
        }

        public bool RequestDeath(PlayerLifecycle dyingPlayer)
        {
            if (busy || pendingDeath || dyingPlayer != player || (snapshot == null && !HasRespawnHandler)) return false;
            pendingDeath = dyingPlayer;
            return true;
        }

        public void RequestTravel(string destination) => TryRequestTravel(destination);

        public bool TryRequestTravel(string destination)
        {
            if (!CanTravel) return false;
            if (string.IsNullOrWhiteSpace(destination) || !Application.CanStreamedLevelBeLoaded(destination))
            {
                Debug.LogError("Destination must be enabled in Build Settings: " + destination);
                return false;
            }
            pendingDestination = destination; // Resolve next Update so death in this physics step wins.
            overlay.BlocksInput = true;
            return true;
        }

        public bool TryStartGame(string destination, float fadeOut, float fadeIn, float minimumBlack)
        {
            if (!TryRequestTravel(destination)) return false;
            pendingStartGame = true;
            startFadeOut = Mathf.Max(0f, fadeOut);
            startFadeIn = Mathf.Max(0f, fadeIn);
            startMinimumBlack = Mathf.Max(0f, minimumBlack);
            return true;
        }

        public bool SaveCheckpoint(string id, Vector3 spawn)
        {
            if (!CanSave) return false;
            try
            {
                ValidateSpawn(spawn);
                var next = Capture(SceneManager.GetActiveScene().path, id, spawn);
                snapshot = next;
                RefreshEntities();
                return true;
            }
            catch (Exception error)
            {
                Debug.LogError("Checkpoint was not saved. Previous snapshot retained. " + error.Message);
                return false;
            }
        }

        private LevelSnapshot Capture(string path, string checkpoint, Vector3 spawn)
        {
            foreach (var entity in FindInScene<SaveableEntity>(SceneManager.GetActiveScene()))
                if (!entities.TryGetValue(entity.Id, out var original) || original != entity)
                    throw new InvalidOperationException("Runtime-created save objects require a spawn adapter: " + entity.name);
            var result = new LevelSnapshot { scenePath = path, checkpointId = checkpoint, spawnPosition = spawn };
            foreach (var pair in entities)
                if (pair.Value) result.entities.Add(pair.Value.Capture());
            return result;
        }

        private IEnumerator Reload(PlayerLifecycle dead)
        {
            busy = true;
            overlay.BlocksInput = true;
            yield return new WaitForSecondsRealtime(dead.deathDuration);
            yield return Fade(1f, fadeOutDuration);
            dead.SetState(PlayerLifeState.Restoring);
            if (HasRespawnHandler)
            {
                IsRestoring = true;
                Physics2D.simulationMode = SimulationMode2D.Script;
                bool restored = false;
                try
                {
                    respawnHandler.RestoreCheckpoint(dead);
                    Physics2D.SyncTransforms();
                    restored = true;
                }
                catch (Exception error)
                {
                    Debug.LogError("Room restoration stopped: " + error);
                }
                if (!restored)
                {
                    FinishFailure();
                    yield break;
                }
                yield return Reveal();
            }
            else yield return Load(snapshot.scenePath, snapshot);
        }

        private IEnumerator Travel(string destination, bool startingGame)
        {
            busy = true;
            if (player) player.SetState(PlayerLifeState.Restoring);
            var music = MusicManager.Instance;
            Coroutine musicFade = music ? music.FadeOutForTravel() : null;
            yield return Fade(1f, startingGame ? startFadeOut : fadeOutDuration);
            float blackUntil = Time.realtimeSinceStartup + (startingGame ? startMinimumBlack : 0f);
            yield return null;
            if (musicFade != null) yield return musicFade;
            yield return Load(destination, null, startingGame ? startFadeIn : -1f, blackUntil);
        }

        private IEnumerator Load(string destination, LevelSnapshot restore, float revealDuration = -1f, float blackUntil = 0f)
        {
            IsRestoring = true;
            Physics2D.simulationMode = SimulationMode2D.Script;
            internalLoad = true;
            if (MusicManager.Instance) MusicManager.Instance.PrepareSceneLoad(destination, restore != null);
            var operation = SceneManager.LoadSceneAsync(destination, LoadSceneMode.Single);
            yield return operation;
            internalLoad = false;
            yield return null;
            if (!TryInitialize(SceneManager.GetActiveScene(), restore))
            {
                FinishFailure();
                yield break;
            }
            float remainingBlack = blackUntil - Time.realtimeSinceStartup;
            if (remainingBlack > 0f) yield return new WaitForSecondsRealtime(remainingBlack);
            yield return Reveal(revealDuration);
        }

        private bool TryInitialize(Scene scene, LevelSnapshot restore)
        {
            try
            {
                respawnHandler = null;
                respawnHandlerBehaviour = null;
                var definitions = FindInScene<LevelDefinition>(scene);
                if (definitions.Count == 0)
                {
                    // Optional room restoration uses the same lifecycle, without snapshot registration.
                    var handlers = new List<MonoBehaviour>();
                    foreach (var component in FindInScene<MonoBehaviour>(scene))
                        if (component is ILevelRespawnHandler) handlers.Add(component);
                    player = null;
                    snapshot = null;
                    entities = null;
                    if (handlers.Count > 0)
                    {
                        var roomPlayers = FindInScene<PlayerLifecycle>(scene);
                        if (handlers.Count != 1 || roomPlayers.Count != 1)
                            throw new InvalidOperationException("Room level needs one respawn handler and one PlayerLifecycle.");
                        respawnHandlerBehaviour = handlers[0];
                        respawnHandler = (ILevelRespawnHandler)handlers[0];
                        player = roomPlayers[0];
                        // Preserve the landing position on a fresh playthrough.
                        player.PlaceForSceneEntry(player.transform.position);
                        Physics2D.SyncTransforms();
                    }
                    return true;
                }
                var players = FindInScene<PlayerLifecycle>(scene);
                if (definitions.Count != 1 || !definitions[0].HasSpawn || players.Count != 1)
                    throw new InvalidOperationException("Level needs exactly one LevelDefinition with DefaultSpawn and one PlayerLifecycle.");
                player = players[0];
                fadeOutDuration = definitions[0].fadeOutDuration;
                fadeInDuration = definitions[0].fadeInDuration;
                entities = new Dictionary<string, SaveableEntity>();
                foreach (var entity in FindInScene<SaveableEntity>(scene))
                {
                    if (string.IsNullOrEmpty(entity.Id) || entities.ContainsKey(entity.Id))
                        throw new InvalidOperationException("Missing/duplicate save ID on " + entity.name);
                    if (entity.GetComponent<PlayerLifecycle>())
                        throw new InvalidOperationException("Player must not have SaveableEntity.");
                    entities.Add(entity.Id, entity);
                }

                Vector3 spawn = restore == null ? definitions[0].DefaultSpawn : restore.spawnPosition;
                if (restore != null)
                {
                    var records = new Dictionary<string, EntitySnapshot>();
                    foreach (var record in restore.entities) records.Add(record.id, record);
                    foreach (var record in restore.entities)
                    {
                        if (!entities.TryGetValue(record.id, out var entity))
                            throw new InvalidOperationException("Scene is missing saved object " + record.id);
                        entity.Restore(record);
                    }
                    foreach (var pair in entities)
                        pair.Value.gameObject.SetActive(records.TryGetValue(pair.Key, out var record) && record.active);
                    snapshot = restore;
                }
                RefreshEntities();
                Physics2D.SyncTransforms();
                ValidateSpawn(spawn);
                Vector3 warp = spawn - player.transform.position;
                if (restore == null) player.PlaceForSceneEntry(spawn);
                else player.PlaceForRevival(spawn);
                Physics2D.SyncTransforms();
                if (restore == null)
                {
                    snapshot = Capture(scene.path, null, spawn);
                    RefreshEntities();
                }
                foreach (var camera in FindInScene<CinemachineVirtualCameraBase>(scene))
                {
                    camera.OnTargetObjectWarped(player.transform, warp);
                    camera.PreviousStateIsValid = false;
                }
                return true;
            }
            catch (Exception error)
            {
                Debug.LogError("Level restoration stopped: " + error);
                return false;
            }
        }

        private void RefreshEntities()
        {
            if (entities == null) return;
            foreach (var entity in entities.Values)
                if (entity) entity.RefreshState();
        }

        private void ValidateSpawn(Vector3 position)
        {
            var collider = player.GetComponent<Collider2D>();
            if (!collider) throw new InvalidOperationException("Player requires a root Collider2D.");
            Bounds bounds = collider.bounds;
            Vector2 center = position + (bounds.center - player.transform.position);
            Vector2 size = (Vector2)bounds.size - Vector2.one * 0.04f;
            foreach (var hit in Physics2D.OverlapBoxAll(center, size, 0f))
            {
                if (hit.attachedRigidbody == player.GetComponent<Rigidbody2D>()) continue;
                if (!hit.isTrigger || hit.GetComponentInParent<DeathZone>())
                    throw new InvalidOperationException("Spawn blocked by " + hit.name + ". Move RespawnPoint to clear supported ground.");
            }
        }

        private IEnumerator Reveal(float durationOverride = -1f)
        {
            IsRestoring = false;
            Physics2D.simulationMode = normalSimulation;
            yield return null; // Camera aligns before the black overlay is removed.
            float revivalTime = player && player.State == PlayerLifeState.Reviving ? player.reviveDuration : 0f;
            float duration = durationOverride >= 0f ? durationOverride : fadeInDuration;
            yield return Fade(0f, duration);
            if (revivalTime > duration) yield return new WaitForSecondsRealtime(revivalTime - duration);
            if (player) player.SetState(PlayerLifeState.Playing);
            busy = false;
            overlay.BlocksInput = false;
        }

        private IEnumerator Fade(float target, float duration)
        {
            float start = overlay.Alpha;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                overlay.Alpha = Mathf.Lerp(start, target, duration > 0f ? elapsed / duration : 1f);
                yield return null;
            }
            overlay.Alpha = target;
        }

        private void FinishFailure()
        {
            Physics2D.simulationMode = normalSimulation;
            overlay.Alpha = 0f;
            overlay.BlocksInput = false;
            // Keep control blocked on invalid restoration rather than playing a partially restored level.
            busy = true;
        }

        private static List<T> FindInScene<T>(Scene scene) where T : Component
        {
            var result = new List<T>();
            foreach (var root in scene.GetRootGameObjects())
                result.AddRange(root.GetComponentsInChildren<T>(true));
            return result;
        }
    }
}
