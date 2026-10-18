using System;
using UnityEngine;
using ZombieSurvival.Combat;

namespace ZombieSurvival.Persistence
{
    [DisallowMultipleComponent, RequireComponent(typeof(ActorIdentity))]
    public sealed class WorldIdentitySaveAdapter : WorldSaveAdapter
    {
        [Serializable] private sealed class State { public int faction, hostility; public bool legacyTargets; }
        public override string Key => "identity";
        public override string Capture()
        {
            var identity=GetComponent<ActorIdentity>();
            return JsonUtility.ToJson(new State{faction=(int)identity.Faction,hostility=(int)identity.SavedHostility,legacyTargets=identity.SavedLegacyTargets});
        }
        public override void Validate(string json,SavedWorldObject owner)
        {
            var state=Read<State>(json);
            if(!Enum.IsDefined(typeof(ActorFaction),state.faction)||state.hostility!=-1&&(state.hostility&~15)!=0)
                throw new InvalidOperationException("Некорректные отношения актёра: "+owner.id);
        }
        public override void ApplySilent(string json)
        {var state=Read<State>(json);GetComponent<ActorIdentity>().RestoreRelations((ActorFaction)state.faction,(FactionMask)state.hostility,state.legacyTargets);}
    }
}
