using UnityEngine;

namespace GameClub.Player
{
    [DisallowMultipleComponent]
    public sealed class LevelDefinition : MonoBehaviour
    {
        [SerializeField] private Transform defaultSpawn;
        [Min(0f)] public float fadeOutDuration = 0.15f;
        [Min(0f)] public float fadeInDuration = 0.15f;
        public Vector3 DefaultSpawn => defaultSpawn ? defaultSpawn.position : transform.position;
        public bool HasSpawn => defaultSpawn;

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireCube(DefaultSpawn, new Vector3(1f, 2f, 0f));
        }

    }
}
