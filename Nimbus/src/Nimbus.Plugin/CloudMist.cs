using System.Linq;
using Jotunn.Managers;
using UnityEngine;
using UnityEngine.Rendering;
namespace Nimbus;
// Cosmetic only: no fireplace, smoke obstruction, damage, collider or network state.
internal sealed class CloudMist:MonoBehaviour
{
    ParticleSystem mist,trail;
    Vector3 previousPosition;
    float trailDistance;
    readonly System.Random trailRandom=new System.Random();
    Material material;
    Texture2D fallbackTexture;
    readonly ParticleSystem.Particle[] particles=new ParticleSystem.Particle[96];
    void Start()
    {
        if(SystemInfo.graphicsDeviceType==GraphicsDeviceType.Null)return;
        var view=GetComponent<ZNetView>();
        if(!view || !view.IsValid())return; // Do not animate the registered prefab.
        Texture texture=null;
        foreach(var id in new[]{"vfx_ground_fog","SmokeParticleSystem"})
        {
            var prefab=PrefabManager.Instance.GetPrefab(id);
            if(prefab)texture=prefab.GetComponentsInChildren<ParticleSystemRenderer>(true)
                .Select(r=>r.sharedMaterial).Where(m=>m && m.HasProperty("_MainTex"))
                .Select(m=>m.GetTexture("_MainTex")).FirstOrDefault(t=>t);
            if(texture)break;
        }
        var shader=Shader.Find("Sprites/Default")??Shader.Find("GUI/Text Shader");
        if(!shader)return;
        if(!texture)
        {
            fallbackTexture=new Texture2D(32,32,TextureFormat.RGBA32,false){name="Nimbus soft mist",wrapMode=TextureWrapMode.Clamp};
            var pixels=new Color[1024];
            for(int y=0;y<32;y++)for(int x=0;x<32;x++)
            {
                float radius=new Vector2((x-15.5f)/15.5f,(y-15.5f)/15.5f).magnitude;
                pixels[y*32+x]=new Color(1,1,1,Mathf.Pow(Mathf.Max(0,1-radius),2));
            }
            fallbackTexture.SetPixels(pixels);fallbackTexture.Apply();texture=fallbackTexture;
        }
        material=new Material(shader){name="Nimbus white mist",mainTexture=texture,renderQueue=3000};
        if(material.HasProperty("_Color"))material.SetColor("_Color",Color.white);
        if(material.HasProperty("_TintColor"))material.SetColor("_TintColor",Color.white);
        var go=new GameObject("Nimbus swirling mist");go.transform.SetParent(transform.Find("Nimbus white cloud"),false);
        mist=go.AddComponent<ParticleSystem>();mist.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
        var main=mist.main;main.loop=true;main.playOnAwake=false;main.maxParticles=96;
        main.startLifetime=new ParticleSystem.MinMaxCurve(2.4f,3.2f);main.startSpeed=0;
        main.startSize=new ParticleSystem.MinMaxCurve(1f,1.7f);
        main.startRotation=new ParticleSystem.MinMaxCurve(0,Mathf.PI*2);
        main.startColor=new Color(.82f,.85f,.88f,.3f);main.simulationSpace=ParticleSystemSimulationSpace.Local;
        main.cullingMode=ParticleSystemCullingMode.PauseAndCatchup;
        var emission=mist.emission;emission.rateOverTime=20;
        var shape=mist.shape;shape.enabled=false;
        var size=mist.sizeOverLifetime;size.enabled=true;size.size=new ParticleSystem.MinMaxCurve(1,AnimationCurve.Linear(0,.65f,1,1.25f));
        var fade=mist.colorOverLifetime;fade.enabled=true;
        var gradient=new Gradient();gradient.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1)},new[]{new GradientAlphaKey(0,0),new GradientAlphaKey(.65f,.2f),new GradientAlphaKey(.45f,.65f),new GradientAlphaKey(0,1)});fade.color=gradient;
        var renderer=mist.GetComponent<ParticleSystemRenderer>();renderer.sharedMaterial=material;
        renderer.renderMode=ParticleSystemRenderMode.Billboard;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
        mist.Play();
        CreateTrail();
    }
    void CreateTrail()
    {
        var go=new GameObject("Nimbus cloud trail");go.transform.SetParent(transform,false);
        trail=go.AddComponent<ParticleSystem>();trail.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
        var main=trail.main;main.loop=true;main.playOnAwake=false;main.maxParticles=40;
        main.startLifetime=new ParticleSystem.MinMaxCurve(1.1f,1.6f);main.startSpeed=0;
        // Start at the previous size range's upper end, then vary upward.
        main.startSize=new ParticleSystem.MinMaxCurve(1.7f,2.55f);
        main.startRotation=new ParticleSystem.MinMaxCurve(0,Mathf.PI*2);
        main.startColor=new Color(.82f,.85f,.88f,.24f);
        // Released puffs stay behind in the world instead of following the vehicle.
        main.simulationSpace=ParticleSystemSimulationSpace.World;
        main.cullingMode=ParticleSystemCullingMode.Automatic;
        var emission=trail.emission;emission.enabled=false;
        var shape=trail.shape;shape.enabled=false;
        var size=trail.sizeOverLifetime;size.enabled=true;size.size=new ParticleSystem.MinMaxCurve(1,AnimationCurve.Linear(0,.65f,1,1.3f));
        var fade=trail.colorOverLifetime;fade.enabled=true;
        var gradient=new Gradient();gradient.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1)},new[]{new GradientAlphaKey(0,0),new GradientAlphaKey(.65f,.15f),new GradientAlphaKey(0,1)});fade.color=gradient;
        var renderer=trail.GetComponent<ParticleSystemRenderer>();renderer.sharedMaterial=material;
        renderer.renderMode=ParticleSystemRenderMode.Billboard;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
        previousPosition=transform.position;trailDistance=0;trail.Play();
    }
    void UpdateTrail()
    {
        if(!trail)return;
        Vector3 current=transform.position,delta=current-previousPosition;
        float distance=delta.magnitude;
        if(distance>5) // Teleports/network corrections must not paint a long smoke line.
        {trail.Clear();trailDistance=0;previousPosition=current;return;}
        var horizontal=new Vector3(delta.x,0,delta.z);
        if(horizontal.magnitude<.2f*Time.deltaTime)
        {trailDistance=0;previousPosition=current;return;}
        Vector3 behind=horizontal.normalized*.7f;
        Vector3 sideways=Vector3.ProjectOnPlane(transform.right,Vector3.up).normalized;
        float next=.55f-trailDistance;
        int count=0;
        while(next<=distance && count<10)
        {
            var puff=new ParticleSystem.EmitParams
            {
                // Scatter across the body width without changing spawn height.
                position=Vector3.Lerp(previousPosition,current,next/distance)-behind
                    +sideways*((float)trailRandom.NextDouble()*1.3f-.65f),
                velocity=Vector3.up*.08f
            };
            trail.Emit(puff,1);next+=.55f;count++;
        }
        trailDistance=Mathf.Repeat(trailDistance+distance,.55f);
        previousPosition=current;
    }
    void LateUpdate()
    {
        if(!mist)return;
        UpdateTrail();
        int count=mist.GetParticles(particles);
        for(int i=0;i<count;i++)
        {
            var p=particles[i];float age=p.startLifetime-p.remainingLifetime;
            float seed=(p.randomSeed%65536)/65536f;
            float angle=seed*Mathf.PI*2+age*.85f;
            float radius=.25f+.72f*((p.randomSeed/65536%1024)/1023f);
            // Each wisp revolves around the local origin, rises gently, and
            // stays below the rider's torso even when the cloud banks.
            p.position=new Vector3(Mathf.Cos(angle)*radius,-.12f+age*.16f,Mathf.Sin(angle)*radius*1.2f);
            particles[i]=p;
        }
        mist.SetParticles(particles,count);
    }
    void OnDisable(){if(mist)mist.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);if(trail)trail.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);}
    void OnEnable(){if(mist)mist.Play();if(trail){previousPosition=transform.position;trailDistance=0;trail.Play();}}
    void OnDestroy()
    {
        if(mist)Destroy(mist.gameObject);
        if(trail)Destroy(trail.gameObject);
        if(material)Destroy(material);
        if(fallbackTexture)Destroy(fallbackTexture);
    }
}
