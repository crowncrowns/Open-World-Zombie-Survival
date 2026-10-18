using System;
using UnityEngine;

namespace ZombieSurvival.Persistence
{
    [Serializable] public sealed class RigidbodyWorldState
    {
        public Vector3 velocity, angularVelocity;
        public bool kinematic, sleeping, gravity, collisions;
        public float mass, damping, angularDamping, maxAngularVelocity;
        public int constraints, interpolation, collisionMode;
        internal static RigidbodyWorldState Capture(Rigidbody body) => new RigidbodyWorldState {
            velocity=body.linearVelocity,angularVelocity=body.angularVelocity,kinematic=body.isKinematic,sleeping=body.IsSleeping(),gravity=body.useGravity,collisions=body.detectCollisions,
            mass=body.mass,damping=body.linearDamping,angularDamping=body.angularDamping,maxAngularVelocity=body.maxAngularVelocity,
            constraints=(int)body.constraints,interpolation=(int)body.interpolation,collisionMode=(int)body.collisionDetectionMode };
        internal void Validate()
        {
            if(!WorldSaveAdapter.ValidPosition(velocity)||!WorldSaveAdapter.ValidPosition(angularVelocity)||!WorldSaveAdapter.Finite(mass)||mass<=0||mass>1e9
                ||!WorldSaveAdapter.Finite(damping)||damping<0||!WorldSaveAdapter.Finite(angularDamping)||angularDamping<0||!WorldSaveAdapter.Finite(maxAngularVelocity)||maxAngularVelocity<0
                ||(constraints&~(int)RigidbodyConstraints.FreezeAll)!=0||!Enum.IsDefined(typeof(RigidbodyInterpolation),interpolation)||!Enum.IsDefined(typeof(CollisionDetectionMode),collisionMode))
                throw new InvalidOperationException("Некорректные параметры физического тела.");
        }
        internal void Prepare(Rigidbody body)
        {
            body.isKinematic=true;body.mass=mass;body.linearDamping=damping;body.angularDamping=angularDamping;body.maxAngularVelocity=maxAngularVelocity;
            body.useGravity=gravity;body.detectCollisions=collisions;body.constraints=(RigidbodyConstraints)constraints;body.interpolation=(RigidbodyInterpolation)interpolation;
        }
        internal void Finish(Rigidbody body)
        {
            body.isKinematic=kinematic;body.collisionDetectionMode=(CollisionDetectionMode)collisionMode;
            if(!kinematic){body.linearVelocity=velocity;body.angularVelocity=angularVelocity;if(sleeping)body.Sleep();else body.WakeUp();}
        }
    }
    /// <summary>Один физический корень. Составной ragdoll требует своего блока с состоянием каждого тела.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(Rigidbody))]
    public sealed class WorldRigidbodySaveAdapter : WorldSaveAdapter
    {
        private RigidbodyWorldState applied;
        public override string Key => "rigidbody";
        public override string Capture()
        {
            var body = GetComponent<Rigidbody>();
            return JsonUtility.ToJson(RigidbodyWorldState.Capture(body));
        }
        public override void Validate(string json, SavedWorldObject owner)
        {
            var state = Read<RigidbodyWorldState>(json);state.Validate();
            if (GetComponent<UnityEngine.AI.NavMeshAgent>() != null && !state.kinematic)
                throw new InvalidOperationException("Некорректная физика корня или конфликт Rigidbody/NavMeshAgent: " + owner.id);
        }
        public override void ApplySilent(string json) { applied = Read<RigidbodyWorldState>(json); applied.Prepare(GetComponent<Rigidbody>()); }
        public override void Publish()
        {
            if (applied != null) applied.Finish(GetComponent<Rigidbody>());
        }
    }
}
