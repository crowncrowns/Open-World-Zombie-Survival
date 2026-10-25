using UnityEngine;

namespace PostApocalypse.Lighting
{
    /// <summary>Optional controls used only by the pack's own demonstration scene.</summary>
    public sealed class LightingShowcaseControls : MonoBehaviour
    {
        public Camera viewCamera;
        public LightFixtureController[] gallery;
        public string[] sampleNames;
        public bool[] ceilingSamples;
        public LightFixtureController[] independentExamples;
        int selected;
        bool galleryView;
        static readonly string[] Zones={"Подъезд","Коридор","Двор","Вход","Техпомещение"};
        static readonly string[] Modes={"Нормальный","Мерцание","Тусклый","Сбои","Выключен"};
        void OnGUI()
        {
            if(!viewCamera)return;
            GUILayout.BeginArea(new Rect(16,16,350,400),GUI.skin.box);
            GUILayout.Label("Светильники · обзор набора");
            GUILayout.BeginHorizontal();
            for(int i=0;i<Zones.Length;i++)if(GUILayout.Button(Zones[i],GUILayout.Height(30)))
            {
                galleryView=false;float x=i*7;viewCamera.orthographic=false;
                viewCamera.transform.position=new Vector3(x+3.6f,3.6f,6.4f);viewCamera.transform.LookAt(new Vector3(x,1.4f,-.7f));
            }
            GUILayout.EndHorizontal();
            if(GUILayout.Button("Все модели: просмотр по одной",GUILayout.Height(28))){galleryView=true;FocusSample();}
            if(galleryView&&gallery!=null&&gallery.Length>0)
            {
                GUILayout.BeginHorizontal();
                if(GUILayout.Button("◀",GUILayout.Width(35))){selected=(selected+gallery.Length-1)%gallery.Length;FocusSample();}
                GUILayout.Label((selected+1)+" / "+gallery.Length+"  "+sampleNames[selected]);
                if(GUILayout.Button("▶",GUILayout.Width(35))){selected=(selected+1)%gallery.Length;FocusSample();}
                GUILayout.EndHorizontal();
                DrawSettings(gallery[selected]);
            }
            if(GUILayout.Button("Два экземпляра: разные яркость и цвет",GUILayout.Height(28)))
            {
                galleryView=false;viewCamera.orthographic=false;viewCamera.transform.position=new Vector3(.5f,1.4f,5);viewCamera.transform.LookAt(new Vector3(.5f,1.2f,3));
            }
            if(!galleryView&&independentExamples!=null)
                for(int i=0;i<independentExamples.Length;i++)
                {
                    var c=independentExamples[i];if(!c)continue;
                    GUILayout.Label(i==0?"Левый · тёплый":"Правый · холодный");
                    c.SetIntensity(GUILayout.HorizontalSlider(c.intensity,0,8));
                }
            GUILayout.Label("Все параметры доступны на корне светильника.");
            GUILayout.EndArea();
        }
        void DrawSettings(LightFixtureController c)
        {
            if(!c)return;
            c.lightEnabled=GUILayout.Toggle(c.lightEnabled,"Питание");
            GUILayout.Label("Яркость: "+c.intensity.ToString("F1"));c.intensity=GUILayout.HorizontalSlider(c.intensity,0,12);
            int mode=GUILayout.SelectionGrid((int)c.mode,Modes,3);c.mode=(LightFixtureController.OperatingMode)mode;
            c.ApplyNow();
        }
        void FocusSample()
        {
            if(gallery==null||gallery.Length==0||!gallery[selected])return;
            var c=gallery[selected];var lod=c.GetComponent<LODGroup>();var bounds=new Bounds(c.transform.position,Vector3.zero);
            foreach(var r in lod.GetLODs()[0].renderers)bounds.Encapsulate(r.bounds);
            float size=Mathf.Max(bounds.size.x,Mathf.Max(bounds.size.y,bounds.size.z));viewCamera.orthographic=true;viewCamera.orthographicSize=Mathf.Max(.16f,size*.8f);
            Vector3 direction=ceilingSamples[selected]?new Vector3(.5f,-1.1f,1.6f):new Vector3(.65f,.35f,2);
            viewCamera.transform.position=bounds.center+direction.normalized*Mathf.Max(.6f,size*3);viewCamera.transform.LookAt(bounds.center);
        }
    }
}
