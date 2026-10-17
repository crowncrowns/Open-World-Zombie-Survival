using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using ZombieSurvival.Audio;
using ZombieSurvival.Interaction;

/// <summary>Подключает новые WAV к собственным профилям, не изменяя пакеты Asset Store.</summary>
public static class ActorAudioSetup
{
    private const string Profiles="Assets/Audio/Profiles";
    private const string ZombieVoices="Assets/Tybug Studios/Zombie Voice Pack - Free/";
    private struct Cue
    {
        public ActorSound sound; public AudioClip[] clips; public float volume,pitch;
        public Cue(ActorSound sound,AudioClip[] clips,float volume,float pitch=1f){this.sound=sound;this.clips=clips;this.volume=volume;this.pitch=pitch;}
    }
    [MenuItem("Tools/Character Lab/Set Up Actor Audio")]
    public static void Run()
    {
        if(Application.isPlaying)throw new System.InvalidOperationException("Сначала остановите Play.");
        if(!AssetDatabase.IsValidFolder("Assets/Audio"))AssetDatabase.CreateFolder("Assets","Audio");
        if(!AssetDatabase.IsValidFolder(Profiles))AssetDatabase.CreateFolder("Assets/Audio","Profiles");
        var walk=Clips("Assets/Footsteps - Essentials/Footsteps_Rock/Footsteps_Rock_Walk");
        var run=Clips("Assets/Footsteps - Essentials/Footsteps_Rock/Footsteps_Rock_Run");
        var jump=Clips("Assets/Footsteps - Essentials/Footsteps_Rock/Footsteps_Rock_Jump","Start");
        var land=Clips("Assets/Footsteps - Essentials/Footsteps_Rock/Footsteps_Rock_Jump","Land");
        var soft=Clips("Assets/Audio/Generated","SoftStep");var crawl=Clips("Assets/Audio/Generated","CrawlRustle");
        var player=Profile("Player",new[]{
            new Cue(ActorSound.Walk,walk,.6f),new Cue(ActorSound.Run,run,.85f),new Cue(ActorSound.Quiet,soft,.18f,1.05f),
            new Cue(ActorSound.Crouch,soft,.28f,.88f),new Cue(ActorSound.Crawl,crawl,.32f),new Cue(ActorSound.Jump,jump,.45f),
            new Cue(ActorSound.Land,land,.75f),new Cue(ActorSound.Roll,crawl,.5f,.85f),new Cue(ActorSound.Climb,crawl,.38f),
            new Cue(ActorSound.Hurt,soft,.55f,.7f),new Cue(ActorSound.Death,land,.6f,.7f)});
        var zombie=Profile("Zombie",new[]{
            new Cue(ActorSound.Walk,walk,.62f,.78f),new Cue(ActorSound.Run,run,.8f,.85f),new Cue(ActorSound.Crawl,crawl,.48f,.8f),
            new Cue(ActorSound.Voice,Clips(ZombieVoices+"Zombie Growl").Concat(Clips(ZombieVoices+"Zombie Moan")).ToArray(),.42f),
            new Cue(ActorSound.Attack,Clips(ZombieVoices+"Zombie Aggressive").Concat(Clips(ZombieVoices+"Zombie Hiss")).ToArray(),.8f),
            new Cue(ActorSound.Hurt,Clips(ZombieVoices+"Zombie Grunt"),.7f),new Cue(ActorSound.Death,Clips(ZombieVoices+"Zombie Death"),.8f)});
        // Необязательный пример материала: назначить FootstepSurface на деревянную площадку.
        Profile("WoodSurface",new[]{new Cue(ActorSound.Walk,Clips("Assets/Footsteps - Essentials/Footsteps_Wood/Footsteps_Wood_Walk"),.6f),
            new Cue(ActorSound.Run,Clips("Assets/Footsteps - Essentials/Footsteps_Wood/Footsteps_Wood_Run"),.8f),
            new Cue(ActorSound.Quiet,Clips("Assets/Footsteps - Essentials/Footsteps_Wood/Footsteps_Wood_Walk"),.18f),
            new Cue(ActorSound.Crouch,Clips("Assets/Footsteps - Essentials/Footsteps_Wood/Footsteps_Wood_Walk"),.28f)});
        Configure("Assets/CharacterTesting/Prefabs/CharacterTestPlayer.prefab",player,true);
        Configure("Assets/Characters/Enemies/ZombieMaleActor.prefab",zombie,false);
        var scene=EditorSceneManager.OpenScene("Assets/CharacterTesting/CharacterTest.unity");
        EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
        Debug.Log("ACTOR_AUDIO_SETUP_OK: player gaits, NPC steps/voice/attacks/damage/death and replaceable profiles.");
    }
    private static AudioClip[] Clips(string folder,string contains="")=>AssetDatabase.FindAssets("t:AudioClip",new[]{folder})
        .Select(g=>AssetDatabase.LoadAssetAtPath<AudioClip>(AssetDatabase.GUIDToAssetPath(g))).Where(c=>c.name.Contains(contains)).OrderBy(c=>c.name).ToArray();
    private static ActorAudioProfile Profile(string name,Cue[] cues)
    {
        string path=Profiles+"/"+name+"Audio.asset";var profile=AssetDatabase.LoadAssetAtPath<ActorAudioProfile>(path);
        if(profile!=null)
        {
            // Однократная замена созданных этим инструментом временных голосов на новый пакет.
            // Уже назначенные вручную/пакетные клипы и громкость не перезаписываются.
            var existing=new SerializedObject(profile);var entries=existing.FindProperty("cues");
            for(int i=0;i<entries.arraySize;i++)
            {
                var entry=entries.GetArrayElementAtIndex(i);var clips=entry.FindPropertyRelative("clips");
                bool generatedVoice=false;
                for(int j=0;j<clips.arraySize;j++)
                    if(AssetDatabase.GetAssetPath(clips.GetArrayElementAtIndex(j).objectReferenceValue).StartsWith("Assets/Audio/Generated/Zombie"))generatedVoice=true;
                if(!generatedVoice)continue;
                var replacement=cues.First(c=>(int)c.sound==entry.FindPropertyRelative("sound").enumValueIndex);
                if(replacement.clips.Length==0||replacement.clips.Any(c=>c==null))throw new System.InvalidOperationException("Missing zombie voice pack clips");
                clips.arraySize=replacement.clips.Length;for(int j=0;j<replacement.clips.Length;j++)clips.GetArrayElementAtIndex(j).objectReferenceValue=replacement.clips[j];
            }
            existing.ApplyModifiedPropertiesWithoutUndo();return profile;
        }
        foreach(var cue in cues)if(cue.clips.Length==0||cue.clips.Any(c=>c==null))throw new System.InvalidOperationException("Missing audio for "+name+" / "+cue.sound);
        profile=ScriptableObject.CreateInstance<ActorAudioProfile>();AssetDatabase.CreateAsset(profile,path);
        var so=new SerializedObject(profile);var array=so.FindProperty("cues");array.arraySize=cues.Length;
        for(int i=0;i<cues.Length;i++)
        {
            var entry=array.GetArrayElementAtIndex(i);var cue=cues[i];entry.FindPropertyRelative("sound").enumValueIndex=(int)cue.sound;
            entry.FindPropertyRelative("volume").floatValue=cue.volume;entry.FindPropertyRelative("pitch").vector2Value=new Vector2(cue.pitch*.96f,cue.pitch*1.04f);
            var clips=entry.FindPropertyRelative("clips");clips.arraySize=cue.clips.Length;for(int j=0;j<cue.clips.Length;j++)clips.GetArrayElementAtIndex(j).objectReferenceValue=cue.clips[j];
        }
        so.ApplyModifiedPropertiesWithoutUndo();return profile;
    }
    private static void Configure(string path,ActorAudioProfile profile,bool player)
    {
        var root=PrefabUtility.LoadPrefabContents(path);
        try
        {
            var emitter=root.GetComponent<ActorAudioEmitter>()??root.AddComponent<ActorAudioEmitter>();
            var so=new SerializedObject(emitter);if(so.FindProperty("profile").objectReferenceValue==null)so.FindProperty("profile").objectReferenceValue=profile;
            foreach(var channel in new[]{"movementSource","voiceSource"})
            {
                if(so.FindProperty(channel).objectReferenceValue!=null)continue;
                var child=new GameObject(channel=="movementSource"?"Movement audio":"Voice audio");child.transform.SetParent(root.transform,false);child.transform.localPosition=Vector3.up*.2f;
                var source=child.AddComponent<AudioSource>();source.playOnAwake=false;source.spatialBlend=1;source.minDistance=2;source.maxDistance=26;source.dopplerLevel=0;source.rolloffMode=AudioRolloffMode.Linear;
                so.FindProperty(channel).objectReferenceValue=source;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            if(player)
            {
                if(root.GetComponent<PlayerMovementAudio>()==null)root.AddComponent<PlayerMovementAudio>();
                var interactor=root.GetComponent<PlayerInteractor>();var interaction=new SerializedObject(interactor);
                interaction.FindProperty("viewCamera").objectReferenceValue=root.GetComponentInChildren<Camera>(true);interaction.ApplyModifiedPropertiesWithoutUndo();
            }
            else if(root.GetComponent<NavMeshActorAudio>()==null)root.AddComponent<NavMeshActorAudio>();
            PrefabUtility.SaveAsPrefabAsset(root,path);
        }
        finally{PrefabUtility.UnloadPrefabContents(root);}
    }
}
