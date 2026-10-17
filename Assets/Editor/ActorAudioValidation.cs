using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using ZombieSurvival.Audio;

public static class ActorAudioValidation
{
    [MenuItem("Tools/Zombie Survival/Проверить звуки актёров")]
    public static void Run()
    {
        var errors=new List<string>();
        foreach(string guid in AssetDatabase.FindAssets("t:ActorAudioProfile"))
        {
            var profile=AssetDatabase.LoadAssetAtPath<ActorAudioProfile>(AssetDatabase.GUIDToAssetPath(guid));
            var cues=new SerializedObject(profile).FindProperty("cues");var sounds=new HashSet<int>();
            if(cues.arraySize==0)errors.Add(profile.name+": пустой профиль");
            for(int i=0;i<cues.arraySize;i++)
            {
                var cue=cues.GetArrayElementAtIndex(i);int sound=cue.FindPropertyRelative("sound").enumValueIndex;
                if(!sounds.Add(sound))errors.Add(profile.name+": повтор действия "+(ActorSound)sound);
                var clips=cue.FindPropertyRelative("clips");
                if(clips.arraySize==0)errors.Add(profile.name+": нет клипов для "+(ActorSound)sound);
                for(int j=0;j<clips.arraySize;j++)
                {
                    var clip=clips.GetArrayElementAtIndex(j).objectReferenceValue as AudioClip;
                    if(clip==null||clip.samples==0)errors.Add(profile.name+": потерян AudioClip для "+(ActorSound)sound);
                }
            }
        }
        foreach(var emitter in UnityEngine.Object.FindObjectsByType<ActorAudioEmitter>(FindObjectsInactive.Include,FindObjectsSortMode.None))
        {
            if(emitter.Profile==null)errors.Add(emitter.name+": не назначен профиль");
            if(emitter.MovementSource==null||emitter.VoiceSource==null||emitter.MovementSource==emitter.VoiceSource)
                errors.Add(emitter.name+": нужны два отдельных AudioSource");
        }
        int listeners=0;
        foreach(var listener in UnityEngine.Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None))if(listener.isActiveAndEnabled)listeners++;
        if(listeners!=1)errors.Add("В открытой сцене должен быть один активный AudioListener, найдено: "+listeners);
        if(errors.Count>0)throw new InvalidOperationException(string.Join("\n",errors));
        Debug.Log("ACTOR_AUDIO_VALIDATION_OK: audio profiles, clip references, independent sources and one listener.");
    }
}
