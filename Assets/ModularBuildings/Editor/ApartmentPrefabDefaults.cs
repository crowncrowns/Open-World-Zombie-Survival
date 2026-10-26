using System;
using UnityEditor;
using UnityEngine;
namespace ZombieSurvival.ModularBuildings.Editor
{
    public static class ApartmentPrefabDefaults
    {
        public const string Source="Assets/Maps/Buildings/Apartments Prefabs/Apart_Variant_1.prefab";
        public const string LayoutPath=BuildingGenerator.Folder+"/ApartVariant1_Layout.asset";
        public static ApartmentPrefabLayout GetOrCreate()
        {
            var existing=AssetDatabase.LoadAssetAtPath<ApartmentPrefabLayout>(LayoutPath);if(existing)return existing;
            var source=AssetDatabase.LoadAssetAtPath<GameObject>(Source);if(!source)throw new InvalidOperationException("Не найден авторский Apart_Variant_1.");
            var layout=ScriptableObject.CreateInstance<ApartmentPrefabLayout>();layout.name="ApartVariant1_Layout";layout.prefab=source;
            // User-confirmed right-hand opening between source Room_Wall_Door (9) and (10).
            layout.entryFloorPosition=new Vector3(3.47753f,-3,1.126367f);layout.inwardDirection=Vector3.left;layout.floorY=-3;layout.roomHeight=3;
            // Reserve complete rooms, including the large north-eastern room beyond Door (9).
            layout.footprintRegions=new[]{Rect.MinMaxRect(-2.67247f,-3.473633f,3.52753f,8.526367f)};
            void Room(string title,GeneratedRoomKind kind,float x,float z)=>layout.rooms.Add(new ApartmentPrefabRoomMarker{title=title,kind=kind,position=new Vector3(x,-3,z),availableForLoot=true});
            Room("Кухня",GeneratedRoomKind.Kitchen,-1.07247f,-1.973633f);
            Room("Жилая комната — передняя",GeneratedRoomKind.Living,1.92753f,-1.973633f);
            Room("Жилая комната — большая",GeneratedRoomKind.Living,1.82753f,5.526367f);
            Room("Жилая комната — задняя",GeneratedRoomKind.Living,-1.27247f,7.026367f);
            Room("Ванная",GeneratedRoomKind.Bathroom,-2.12247f,1.126367f);
            Room("Туалет",GeneratedRoomKind.Toilet,-2.12247f,4.126367f);
            AssetDatabase.CreateAsset(layout,LayoutPath);return layout;
        }
        [MenuItem("Tools/Zombie Survival/Квартиры/Показать привязки Apart Variant 1")]
        static void Show(){var asset=GetOrCreate();Selection.activeObject=asset;EditorGUIUtility.PingObject(asset);}
    }
}
