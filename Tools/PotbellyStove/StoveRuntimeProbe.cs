#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections;
using UnityEngine;
using UnityEditor;
using ZombieSurvival.EnvironmentProps;

public class StoveRuntimeProbe : MonoBehaviour
{
    static string Proof=>Path.GetFullPath("../../../Verification/PotbellyStove");
    int checks;
    void Check(bool value,string message){if(!value)throw new Exception(message);checks++;Debug.Log("STOVE_CHECK "+message);}
    IEnumerator Start()
    {
        yield return null;
        var stove=FindFirstObjectByType<StoveController>();
        Check(stove&&stove.IsBurning,"default Burning");
        var ps=stove.GetComponentsInChildren<ParticleSystem>();
        var smoke=ps.First(p=>p.name=="Smoke_VFX");var fire=ps.First(p=>p.name=="Fire_VFX");
        yield return new WaitForSeconds(3);
        Check(fire.particleCount>0,"live fire particles");Check(smoke.particleCount>0,"live smoke particles");
        Check(smoke.transform.position.y>2.6f,"smoke exits upper flue");
        var particles=new ParticleSystem.Particle[128];int count=smoke.GetParticles(particles);
        Check(particles.Take(count).Any(p=>p.position.y>2.8f),"smoke rises in world space");
        var light=stove.GetComponentInChildren<Light>();float previous=light.intensity;float maxStep=0;
        var camera=Camera.main;var cameraPosition=camera.transform.position;var cameraRotation=camera.transform.rotation;
        camera.transform.position=new Vector3(.62f,.61f,1.5f);camera.transform.LookAt(new Vector3(0,.34f,.08f));
        Directory.CreateDirectory(Path.Combine(Proof,"AnimationFrames"));
        for(int i=0;i<24;i++){yield return new WaitForSeconds(.1f);maxStep=Mathf.Max(maxStep,Mathf.Abs(light.intensity-previous));previous=light.intensity;Capture("AnimationFrames/Fire_"+i.ToString("D2")+".png",600,500);}
        camera.transform.SetPositionAndRotation(cameraPosition,cameraRotation);
        Check(light.enabled&&maxStep<.2f,"continuous warm light without abrupt flicker");
        var fireParticles=new ParticleSystem.Particle[64];int fireCount=fire.GetParticles(fireParticles);
        Check(fireParticles.Take(fireCount).Select(p=>fire.transform.TransformPoint(p.position)).All(p=>p.y>.1f&&p.y<.51f&&Mathf.Abs(p.x)<.15f&&Mathf.Abs(p.z)<.30f),"flame particle centres stay in firebox");
        Capture("Play_Burning.png");
        float original=stove.CurrentIntensity;stove.Extinguish();
        Check(stove.CurrentIntensity==original,"extinguish does not snap intensity");
        yield return new WaitForSeconds(3.2f);
        Check(stove.CurrentIntensity<.001f&&!light.enabled,"flame and light fade off");
        Check(stove.EmberHeat>.1f,"embers cool more slowly than fire");
        yield return new WaitForSeconds(10);
        Check(stove.EmberHeat<.001f,"embers eventually cold");Check(smoke.particleCount==0,"smoke dissipates after shutdown");Check(fire.particleCount==0,"no persistent flame after shutdown");
        Capture("Play_Extinguished.png");
        stove.Ignite();yield return new WaitForSeconds(3.2f);
        Check(stove.IsBurning&&stove.CurrentIntensity>.99f&&fire.particleCount>0,"reignite after complete shutdown");
        stove.SetFireIntensity(.35f);yield return new WaitForSeconds(3);
        Check(Mathf.Abs(stove.CurrentIntensity-.35f)<.01f,"intensity control");
        stove.SetFireIntensity(float.NaN);Check(stove.IsBurning,"NaN rejected");
        stove.SetFireIntensity(5);yield return new WaitForSeconds(3);Check(stove.CurrentIntensity<=1&&stove.CurrentIntensity>.99f,"intensity clamp");
        stove.SetFireIntensity(-1);yield return new WaitForSeconds(3);Check(!stove.IsBurning&&stove.CurrentIntensity<.001f,"negative intensity clamps to zero");
        stove.Ignite();yield return new WaitForSeconds(3);Check(stove.IsBurning&&stove.CurrentIntensity>.99f,"Ignite restores fire after zero intensity");
        stove.gameObject.SetActive(false);Check(!light.enabled&&ps.All(p=>p.particleCount==0),"disable clears presentation");stove.gameObject.SetActive(true);
        yield return new WaitForSeconds(.5f);Check(light.enabled&&fire.isPlaying,"reenable resumes presentation");
        var lod=stove.GetComponent<LODGroup>();for(int i=0;i<3;i++){lod.ForceLOD(i);yield return null;Check(lod.GetLODs()[i].renderers.All(r=>r&&r.sharedMaterials.All(m=>m&&m.shader.isSupported)),"LOD "+i+" materials valid");}lod.ForceLOD(-1);
        Check(Physics.Raycast(new Vector3(1,.4f,0),Vector3.left,out var hit,1f)&&hit.collider.transform.IsChildOf(stove.transform),"body blocks physics ray");
        Check(!Physics.Raycast(new Vector3(0,.32f,.8f),Vector3.back,.55f),"firebox mouth remains open to physics");
        File.WriteAllText(Path.Combine(Proof,"Runtime.json"),"{\"passed\":true,\"checks\":"+checks+",\"fireParticles\":"+fire.particleCount+",\"smokeParticles\":"+smoke.particleCount+",\"maxLightFrameDelta\":"+maxStep.ToString(System.Globalization.CultureInfo.InvariantCulture)+"}");
        Debug.Log("STOVE_RUNTIME_PASS "+checks);EditorApplication.Exit(0);
    }
    void Capture(string name,int width=1000,int height=1400)
    {
        var c=Camera.main;var rt=new RenderTexture(width,height,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);rt.Create();c.targetTexture=rt;c.Render();RenderTexture.active=rt;var t=new Texture2D(width,height,TextureFormat.RGB24,false);t.ReadPixels(new Rect(0,0,width,height),0,0);t.Apply();File.WriteAllBytes(Path.Combine(Proof,name),t.EncodeToPNG());c.targetTexture=null;RenderTexture.active=null;Destroy(t);rt.Release();Destroy(rt);
    }
}
#endif
