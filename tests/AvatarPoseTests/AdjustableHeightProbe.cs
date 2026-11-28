using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using HarmonyLib;
using UnityEngine;
using ValheimVRM;

static class AdjustableHeightProbe
{
    static readonly System.Reflection.MethodInfo Synchronize = AccessTools.Method(typeof(VRMAnimationSync),"LateUpdate");
    public static IEnumerator Run(GameObject imported, Animator source, List<string> report, string output)
    {
        if(Environment.GetEnvironmentVariable("VRM_HEIGHT_REPEAT")=="1")
        { yield return Repeat(imported,source,report,output); yield break; }
        var preferences = new AvatarHeightOptions(Path.Combine(output,"height-preferences"));
        Check(preferences.Get("new")==2f,"New character height is not 2 m");
        preferences.Set("a",1.4f); preferences.Set("b",2f); preferences.Load();
        Check(preferences.Get("a")==1.4f && preferences.Get("b")==2f,"Character height preferences crossed or failed to persist");
        var templateScale=imported.transform.localScale; float measured=imported.GetComponent<AvatarScale>().UnscaledHeight;
        foreach(float height in new[]{1.4f,2f,2.2f})
        {
            float maxContact=0;
            foreach(int loadingPose in new[]{229373857,-1544306596,-1829310159})
            {
                source.Rebind(); source.Play(loadingPose,0,.5f); source.Update(0);
                var model=UnityEngine.Object.Instantiate(imported); model.transform.SetParent(source.transform.parent,false);
                AvatarScale.ApplyHeight(model,height);
                Check(Mathf.Abs(model.transform.localScale.y*measured-height)<.0001f,"Exact height was not applied from cached import");
                AccessTools.Method(typeof(ValheimVRM.VRM),"PrepareVrm10Clone").Invoke(null,new object[]{imported,model});
                model.SetActive(true);
                foreach(var behaviour in model.GetComponents<MonoBehaviour>())behaviour.enabled=false;
                var sync=model.AddComponent<VRMAnimationSync>();
                sync.Setup(source,new ValheimVRM.Settings.VrmSettingsContainer()); sync.enabled=false;
                using(var rendered=new GroundingProbe.Surface(model.GetComponent<Animator>()))
                using(var native=new GroundingProbe.Surface(source))
                {
                    foreach(int pose in new[]{229373857,890925016,-1544306596,-805461806,-1829310159})
                        for(int frame=0;frame<5;frame++)
                        {
                            source.Rebind(); source.Play(pose,0,frame*.125f); source.Update(0);
                            native.Sample(out _,out float seat,out _);
                            var hips=source.GetBoneTransform(HumanBodyBones.Hips).position;
                            Synchronize.Invoke(sync,null); rendered.Sample(out float foot,out float targetSeat,out float lower);
                            float error=pose==229373857 ? Mathf.Abs(foot-source.transform.position.y)
                                : pose==-1829310159 ? Mathf.Abs(targetSeat-seat) : Mathf.Abs(lower-source.transform.position.y);
                            if(frame==4 && (pose==229373857 || pose==-1544306596 || pose==-1829310159))
                                Check(error<(pose==229373857?.06f:.025f),$"Height {height} loaded in {loadingPose}, pose {pose}: reference contact error {error}");
                            Check(source.GetBoneTransform(HumanBodyBones.Hips).position==hips,"Height calibration changed native hips");
                            if(frame==4 && (pose==229373857 || pose==-1544306596 || pose==-1829310159)) maxContact=Mathf.Max(maxContact,error);
                        }
                }
                if(loadingPose==229373857)
                {
                    source.Rebind(); source.SetBool("onGround",true); source.Play(229373857,0,.5f); source.Update(0);
                    var snapshot=source.gameObject.AddComponent<NativePoseSnapshot>(); snapshot.Setup(source);
                    var observer=model.AddComponent<GaitObserver>(); observer.Source=source; observer.Target=model.GetComponent<Animator>(); observer.Snapshot=snapshot;
                    sync.enabled=true;
                    float minimum=float.PositiveInfinity, maximum=float.NegativeInfinity;
                    foreach(float speed in new[]{0f,1.5f,6f,0f})
                    {
                        source.SetFloat("forward_speed",speed); observer.ResetRange();
                        for(int frame=0;frame<90;frame++) { yield return null; if(observer.Failure!=null)throw observer.Failure; }
                        minimum=Mathf.Min(minimum,observer.MinOffset); maximum=Mathf.Max(maximum,observer.MaxOffset);
                    }
                    Check(maximum-minimum<.001f,"Height adjustment added gait-dependent lift");
                    report.Add($"height={height:F2}m: {observer.Frames} live start/walk/run/stop frames, additional lift variation={maximum-minimum:F6}m");
                    sync.enabled=observer.enabled=snapshot.enabled=false;
                    UnityEngine.Object.Destroy(snapshot);
                }
                UnityEngine.Object.Destroy(model); yield return null;
            }
            Check(imported.transform.localScale==templateScale,"Personal height changed the shared import");
            report.Add($"height={height:F2}m: 75 mesh contact checks from standing/sitting/chair loading poses; maximum contact error={maxContact:F6}m; cached template unchanged");
            File.WriteAllLines(Path.Combine(output,"results.txt"),report);
        }
    }
    static IEnumerator Repeat(GameObject imported,Animator source,List<string> report,string output)
    {
        var templateScale=imported.transform.localScale;
        var baselines=new Dictionary<string,Vector3>();
        float maximumDrift=0;
        for(int change=0;change<36;change++)
        {
            float height=new[]{1.4f,2.2f,2f}[change%3];
            int pose=new[]{229373857,-1544306596,-1829310159}[(change/3)%3];
            source.Rebind(); source.Play(pose,0,.5f); source.Update(0);
            var nativeHips=source.GetBoneTransform(HumanBodyBones.Hips).position;
            var model=UnityEngine.Object.Instantiate(imported); model.transform.SetParent(source.transform.parent,false);
            float scale=AvatarScale.ApplyHeight(model,height);
            Check(AvatarScale.ApplyHeight(model,height)==scale,"Repeated same height compounded scale");
            Check(Mathf.Abs(scale*model.GetComponent<AvatarScale>().UnscaledHeight-height)<.0001f,"Height is not absolute");
            AccessTools.Method(typeof(ValheimVRM.VRM),"PrepareVrm10Clone").Invoke(null,new object[]{imported,model});
            model.SetActive(true); foreach(var behaviour in model.GetComponents<MonoBehaviour>())behaviour.enabled=false;
            var sync=model.AddComponent<VRMAnimationSync>();sync.Setup(source,new ValheimVRM.Settings.VrmSettingsContainer());sync.enabled=false;
            using(var surface=new GroundingProbe.Surface(model.GetComponent<Animator>()))
            {
                for(int frame=0;frame<30;frame++)
                {
                    Synchronize.Invoke(sync,null);
                    Check(source.GetBoneTransform(HumanBodyBones.Hips).position==nativeHips,"Repeated calibration wrote native hips");
                }
                surface.Sample(out float feet,out float seat,out float lower);
                var sample=new Vector3(model.GetComponent<Animator>().GetBoneTransform(HumanBodyBones.Hips).position.y,feet,pose==-1829310159?seat:lower);
                string key=height+":"+pose;
                if(baselines.TryGetValue(key,out var baseline))
                {
                    float drift=Vector3.Distance(sample,baseline);maximumDrift=Mathf.Max(maximumDrift,drift);
                    Check(drift<.0005f,"Repeated height changes accumulated contact/hip offset: "+drift);
                }
                else baselines.Add(key,sample);
            }
            Check(imported.transform.localScale==templateScale,"Repeated height changes resized template");
            UnityEngine.Object.Destroy(model);yield return null;
        }
        report.Add($"repeat: 36 height changes, standing/ground-sit/chair, 1.4/2.2/2 m, double application per change and 30 pose updates; maximum same-height/pose drift={maximumDrift:F9}m; native hips/template unchanged");
        File.WriteAllLines(Path.Combine(output,"results.txt"),report);
    }
    static void Check(bool ok,string message) { if(!ok)throw new Exception(message); }
}
