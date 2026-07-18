using System;
using CardsUnity.Data;
using UnityEngine;

namespace CardsUnity.Runtime
{
    [Serializable]
    public sealed class CharacterBodySegment
    {
        public CharacterRigBone bone;
        public Transform transform;
        public Rigidbody body;
        public Collider primaryCollider;
        public ConfigurableJoint joint;

        public bool IsConfigured => bone != CharacterRigBone.None && transform != null;
        public bool HasPhysicsBody => body != null;
        public bool HasCollider => primaryCollider != null;
        public bool HasJoint => joint != null;
    }
}
