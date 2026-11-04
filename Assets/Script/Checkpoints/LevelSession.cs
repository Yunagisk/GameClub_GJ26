using System;
using System.Collections;
using System.Collections.Generic;
using Cinemachine;
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
        public string CurrentCheckpointId => snapshot == null ? null : snapshot.checkpointId;
        public bool IsRestoring { get; private set; } = true;
        public bool CanSave => !busy && !pendingDeath && snapshot != null && player && player.CanControl;

        private LevelSnapshot snapshot;
        private Dictionary<string, SaveableEntity> entities;
        private PlayerLifecycle player;
        private PlayerLifecycle pendingDeath;
        private string pendingDestination;
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
                StartCoroutine(Reload(dead));
            }
            else if (!string.IsNullOrEmpty(pendingDestination))
            {
                string destination = pendingDestination;
                pendingDestination = null;
                StartCoroutine(Travel(destination));
            }
        }

        public bool RequestDeath(PlayerLifecycle dyingPlayer)
        {
            if (busy || pendingDeath || dyingPlayer != player || snapshot == null) return false;
            pendingDeath = dyingPlayer;
            return true;
        }

        public void RequestTravel(string destination)
        {
            if (busy || pendingDeath || !string.IsNullOrEmpty(pendingDestination)) return;
            if (!Application.CanStreamedLevelBeLoaded(destination))
            {
                Debug.LogError("Destination must be enabled in Build Settings: " + destination);
                return;
            }
            pendingDestination = destination; // Resolve next Update so death in this physics step wins.
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
            yield return new WaitForSecondsRealtime(dead.deathDuration);
            yield return Fade(1f, fadeOutDuration);
            dead.SetState(PlayerLifeState.Restoring);
            yield return Load(snapshot.scenePath, snapshot);
        }

        private IEnumerator Travel(string destination)
        {
            busy = true;
            if (player) player.SetState(PlayerLifeState.Restoring);
            yield return Fade(1f, fadeOutDuration);
            yield return Load(destination, null);
        }

        private IEnumerator Load(string destination, LevelSnapshot restore)
        {
            IsRestoring = true;
            Physics2D.simulationMode = SimulationMode2D.Script;
            internalLoad = true;
            var operation = SceneManager.LoadSceneAsync(destination, LoadSceneMode.Single);
            yield return operation;
            internalLoad = false;
            yield return null;
            if (!TryInitialize(SceneManager.GetActiveScene(), restore))
            {
                FinishFailure();
                yield break;
            }
            yield return Reveal();
        }

        private bool TryInitialize(Scene scene, LevelSnapshot restore)
        {
            try
            {
                var definitions = FindInScene<LevelDefinition>(scene);
                if (definitions.Count == 0)
                {
                    // Existing scenes without checkpoint configuration retain their original gameplay.
                    player = null;
                    snapshot = null;
                    entities = null;
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
                player.PlaceForRevival(spawn);
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

        private IEnumerator Reveal()
        {
            IsRestoring = false;
            Physics2D.simulationMode = normalSimulation;
            yield return null; // Camera aligns before the black overlay is removed.
            float revivalTime = player ? player.reviveDuration : 0f;
            yield return Fade(0f, fadeInDuration);
            yield return new WaitForSecondsRealtime(Mathf.Max(0f, revivalTime - fadeInDuration));
            if (player) player.SetState(PlayerLifeState.Playing);
            busy = false;
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
