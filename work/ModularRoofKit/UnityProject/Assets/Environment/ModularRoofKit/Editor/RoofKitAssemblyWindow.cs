using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using ZombieSurvival.ModularRoofKit;

public sealed class RoofKitAssemblyWindow : EditorWindow
{
    const string Root="Assets/Environment/ModularRoofKit";
    RoofSnapPoint target;
    GameObject prefab;
    string input="Start",output="End";
    int count=1,seed=24;
    float width=8,depth=6;
    string state="Bent";
    float chance=.3f;
    [MenuItem("Tools/Zombie Survival/Modular Roof Kit/Сборка модулей")]
    public static void Open()=>GetWindow<RoofKitAssemblyWindow>("Кровля и водостоки");
    void OnGUI()
    {
        EditorGUILayout.LabelField("Соединения в метрах",EditorStyles.boldLabel);
        target=(RoofSnapPoint)EditorGUILayout.ObjectField("Открытый стык",target,typeof(RoofSnapPoint),true);
        prefab=(GameObject)EditorGUILayout.ObjectField("Добавляемый префаб",prefab,typeof(GameObject),false);
        input=EditorGUILayout.TextField("Входной SnapPoint",input);output=EditorGUILayout.TextField("Выходной SnapPoint",output);
        count=EditorGUILayout.IntSlider("Число секций",count,1,30);
        using(new EditorGUI.DisabledScope(!target||!prefab))
            if(GUILayout.Button("Присоединить цепочку"))Guard(()=>{for(int i=0;i<count;i++)target=Attach(prefab,target,input,output);});
        using(new EditorGUI.DisabledScope(!target||target.kind!="Pipe"))
            if(GUILayout.Button("Трасса вниз: отступ 0,3 м + трубы + слив"))Guard(()=>RouteDown(target,count));
        EditorGUILayout.Space();EditorGUILayout.LabelField("Ограждение прямоугольной крыши",EditorStyles.boldLabel);
        width=EditorGUILayout.FloatField("Ширина X, м (шаг 0,5)",width);depth=EditorGUILayout.FloatField("Глубина Z, м (шаг 0,5)",depth);
        if(GUILayout.Button("Создать периметр у выбранного объекта"))Guard(()=>Perimeter(width,depth,Selection.activeTransform));
        EditorGUILayout.Space();state=EditorGUILayout.TextField("Состояние (condition)",state);
        if(GUILayout.Button("Заменить выбранные совместимые секции"))Guard(()=>ReplaceSelected(state,1,0));
        seed=EditorGUILayout.IntField("Seed",seed);chance=EditorGUILayout.Slider("Доля повреждений",chance,0,1);
        if(GUILayout.Button("Варьировать выбранные: целые / указанное состояние"))Guard(()=>ReplaceSelected(state,chance,seed));
        EditorGUILayout.HelpBox("Все операции поддерживают Undo. Вход и выход задаются именами SnapPoint. Поворот и позиция вычисляются по стыкам; несовместимые размеры отклоняются. Замена сохраняет позу и проверяет все стыки. Сцена автоматически не сохраняется.",MessageType.Info);
    }
    static void Guard(Action run){try{run();}catch(Exception e){Debug.LogError("Roof Kit: "+e.Message);}}
    public static GameObject Find(string name)=>AssetDatabase.FindAssets(name+" t:Prefab",new[]{Root+"/Prefabs"}).Select(AssetDatabase.GUIDToAssetPath).Where(p=>System.IO.Path.GetFileNameWithoutExtension(p)==name).Select(AssetDatabase.LoadAssetAtPath<GameObject>).SingleOrDefault();
    static RoofSnapPoint Point(GameObject root,string name)=>root.GetComponentsInChildren<RoofSnapPoint>(true).SingleOrDefault(s=>s.name==name);
    public static RoofSnapPoint Attach(GameObject asset,RoofSnapPoint destination,string inputName,string outputName)
    {
        if(!asset||!destination)throw new ArgumentException("Нужны префаб и стык назначения.");
        var source=Point(asset,inputName);
        if(!source||!source.Compatible(destination))throw new InvalidOperationException("Несовместимые типы/размеры или отсутствует входной SnapPoint.");
        if(!Point(asset,outputName))throw new InvalidOperationException("Отсутствует выходной SnapPoint.");
        if((destination.transform.lossyScale-Vector3.one).sqrMagnitude>1e-6f)throw new InvalidOperationException("Родительский масштаб должен быть 1:1.");
        var go=(GameObject)PrefabUtility.InstantiatePrefab(asset);Undo.RegisterCreatedObjectUndo(go,"Присоединить кровельный модуль");
        var parent=destination.GetComponentInParent<RoofKitModule>()?.transform.parent;go.transform.SetParent(parent,true);
        var localInput=Point(go,inputName);var desired=Quaternion.LookRotation(-destination.transform.forward,destination.transform.up);
        go.transform.rotation=desired*Quaternion.Inverse(localInput.transform.rotation)*go.transform.rotation;
        go.transform.position+=destination.transform.position-localInput.transform.position;
        Selection.activeGameObject=go;return Point(go,outputName);
    }
    public static void RouteDown(RoofSnapPoint start,int metres)
    {
        if(start.kind!="Pipe"||Vector3.Dot(start.transform.forward,Vector3.down)<.99f)throw new InvalidOperationException("Выберите направленный вниз стык Pipe.");
        var point=Attach(Find("Downpipe_Offset"),start,"In","Out");
        for(int i=0;i<metres;i++)point=Attach(Find("Downpipe_Straight_1m"),point,"In","Out");
        Attach(Find("Downpipe_BottomDrain"),point,"In","Out");
    }
    public static GameObject Perimeter(float w,float d,Transform anchor)
    {
        if(w<1||d<1||Mathf.Abs(w*2-Mathf.Round(w*2))>.001f||Mathf.Abs(d*2-Mathf.Round(d*2))>.001f)throw new InvalidOperationException("Размеры от 1 м с шагом 0,5 м.");
        if(anchor&&(anchor.lossyScale-Vector3.one).sqrMagnitude>1e-6f)throw new InvalidOperationException("Масштаб опоры должен быть 1:1.");
        var root=new GameObject("Roof fence perimeter");Undo.RegisterCreatedObjectUndo(root,"Ограждение по периметру");
        if(anchor){root.transform.position=anchor.position;root.transform.rotation=anchor.rotation;}
        var starts=new[]{Vector3.zero,new Vector3(w,0,0),new Vector3(w,0,d),new Vector3(0,0,d)};
        // Each module owns only its starting post, so corners do not stack posts.
        for(int side=0;side<4;side++)
        {
            float len=side%2==0?w:d;float yaw=new[]{0,-90,180,90}[side];var rot=Quaternion.Euler(0,yaw,0);
            for(float x=0;x<len-.001f;x+=.5f){var go=(GameObject)PrefabUtility.InstantiatePrefab(Find("RoofFence_Straight_0.5m"));go.transform.SetParent(root.transform,false);go.transform.localPosition=starts[side]+rot*(Vector3.right*x);go.transform.localRotation=rot;}
        }
        Selection.activeGameObject=root;return root;
    }
    static bool SameContract(GameObject a,GameObject b)
    {
        var sa=a.GetComponentsInChildren<RoofSnapPoint>(true);var sb=b.GetComponentsInChildren<RoofSnapPoint>(true);
        return sa.Length==sb.Length&&sa.All(x=>sb.Any(y=>x.name==y.name&&x.kind==y.kind&&Mathf.Abs(x.size-y.size)<.0001f&&(a.transform.InverseTransformPoint(x.transform.position)-b.transform.InverseTransformPoint(y.transform.position)).sqrMagnitude<1e-8f&&Vector3.Dot(a.transform.InverseTransformDirection(x.transform.forward),b.transform.InverseTransformDirection(y.transform.forward))>.9999f));
    }
    public static void ReplaceSelected(string condition,float probability,int randomSeed)
    {
        var random=new System.Random(randomSeed);var assets=AssetDatabase.FindAssets("t:Prefab",new[]{Root+"/Prefabs"}).Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.LoadAssetAtPath<GameObject>).ToArray();
        var selected=Selection.gameObjects.Select(g=>g.GetComponentInParent<RoofKitModule>()).Where(m=>m).Distinct().ToArray();
        // Validate every replacement before modifying the scene.
        var replacements=selected.Select(old=>{string c=random.NextDouble()<probability?condition:"IntactOld";if(old.category!="RoofFences"&&c=="IntactOld")c="Intact";var asset=assets.FirstOrDefault(a=>{var m=a.GetComponent<RoofKitModule>();return m&&m.family==old.family&&m.condition==c;});if(!asset||!SameContract(old.gameObject,asset))throw new InvalidOperationException("Нет совместимого варианта "+old.family+" / "+c);return (old,asset);}).ToArray();
        foreach(var pair in replacements)
        {
            var go=(GameObject)PrefabUtility.InstantiatePrefab(pair.asset);Undo.RegisterCreatedObjectUndo(go,"Заменить состояние модуля");go.transform.SetParent(pair.old.transform.parent,false);go.transform.SetPositionAndRotation(pair.old.transform.position,pair.old.transform.rotation);go.transform.localScale=pair.old.transform.localScale;Undo.DestroyObjectImmediate(pair.old.gameObject);
        }
    }
}
