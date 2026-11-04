using System;
using System.Collections.Generic;
using UnityEngine;

namespace GameClub.Player
{
    [Serializable]
    public sealed class LevelSnapshot
    {
        public string scenePath;
        public string checkpointId;
        public Vector3 spawnPosition;
        public List<EntitySnapshot> entities = new List<EntitySnapshot>();
    }

    [Serializable]
    public sealed class EntitySnapshot
    {
        public string id;
        public bool active;
        public Vector3 position;
        public Quaternion rotation;
        public Vector3 scale;
        public Vector2 velocity;
        public float angularVelocity;
        public bool simulated;
        public List<string> componentTypes = new List<string>();
        public List<string> componentStates = new List<string>();
    }
}
