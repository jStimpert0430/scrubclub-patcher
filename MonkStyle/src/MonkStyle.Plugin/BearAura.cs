using System.Linq;
using BepInEx.Configuration;
using HarmonyLib;
using MonkStyle.Core;
using Jotunn.Managers;
using UnityEngine;
using UnityEngine.Rendering;
namespace MonkStyle;
[HarmonyPatch(typeof(VisEquipment),"UpdateEquipmentVisuals")]
internal static class BearEquipment
{
    static readonly int Helmet=BearAuraRules.Helmet.GetStableHashCode(),Chest=BearAuraRules.Chest.GetStableHashCode(),Legs=BearAuraRules.Legs.GetStableHashCode();
    static void Postfix(VisEquipment __instance,int ___m_currentHelmetItemHash,int ___m_currentChestItemHash,int ___m_currentLegItemHash)
    {
        if(!__instance.m_isPlayer || SystemInfo.graphicsDeviceType==GraphicsDeviceType.Null)return;
        var player=__instance.GetComponentInParent<Player>();if(!player)return;
        bool full=BearAuraRules.FullSet(___m_currentHelmetItemHash,___m_currentChestItemHash,___m_currentLegItemHash,Helmet,Chest,Legs);
        var aura=player.GetComponent<BearAura>();
        if(!aura)aura=player.gameObject.AddComponent<BearAura>();
        if(aura)aura.FullSet=full;
    }
}
internal sealed class BearAura:MonoBehaviour
{
    internal static ConfigEntry<bool> Enabled;
    internal bool FullSet;
    Player player;GameObject effect;Light glow;LineRenderer[] wind;Material material;Texture2D texture;
    internal string Status()=>"Bear aura fullSet="+FullSet+" enabled="+Enabled.Value+" effect="+(bool)effect+" shader="+(material?material.shader.name:"none");
    void Awake(){player=GetComponent<Player>();}
    void LateUpdate()
    {
        // Poll equipped inventory for the local player as well as replicated visuals;
        // cosmetic hide settings must not suppress the full-set bonus effect.
        if(player==Player.m_localPlayer)
        {
            var worn=player.GetInventory().GetAllItems().Where(i=>i.m_equipped && i.m_dropPrefab).Select(i=>i.m_dropPrefab.name).ToArray();
            FullSet=worn.Contains(BearAuraRules.Helmet) && worn.Contains(BearAuraRules.Chest) && worn.Contains(BearAuraRules.Legs);
        }
        var camera=Utils.GetMainCamera();
        float distance=camera?Vector3.Distance(camera.transform.position,transform.position):float.PositiveInfinity;
        bool visible=BearAuraRules.Visible(FullSet,Enabled.Value,player.IsDead(),SystemInfo.graphicsDeviceType!=GraphicsDeviceType.Null,distance);
        if(!visible){Clear();return;}
        if(!effect)Build();
        if(!effect)return;
        glow.intensity=.24f+.04f*Mathf.Sin(Time.time*2.4f);
        for(int band=0;band<wind.Length;band++)
        {
            float phase=Mathf.Repeat(Time.time*.42f+band/3f,1f);
            for(int point=0;point<28;point++)
            {
                float t=point/27f;float a=Time.time*1.2f+band*Mathf.PI*2/3+t*2.3f;
                float radius=.8f+.14f*Mathf.Sin(t*Mathf.PI);
                wind[band].SetPosition(point,new Vector3(Mathf.Cos(a)*radius,.12f+phase*2.15f+t*.26f,Mathf.Sin(a)*radius));
            }
            float alpha=.28f*Mathf.Sin(phase*Mathf.PI);
            wind[band].startColor=new Color(1,.94f,.75f,0);wind[band].endColor=new Color(1,.94f,.75f,alpha);
        }
    }
    void Build()
    {
        // Load a shipped VFX prefab explicitly: asset-bundled particle shaders
        // need not be loaded yet when the first player equips the armor.
        Material template=null;
        foreach(var id in new[]{"vfx_Potion_stamina_medium","vfx_Potion_health_medium","vfx_torchflame"})
        {
            var prefab=PrefabManager.Instance.GetPrefab(id);
            if(prefab)template=prefab.GetComponentsInChildren<ParticleSystemRenderer>(true).Select(r=>r.sharedMaterial).FirstOrDefault(m=>m && m.HasProperty("_MainTex"));
            if(template)break;
        }
        var shader=Shader.Find("Sprites/Default")??Shader.Find("GUI/Text Shader")??(template?template.shader:null);
        if(!shader)shader=Resources.FindObjectsOfTypeAll<ParticleSystemRenderer>().Select(r=>r.sharedMaterial).Where(m=>m && m.HasProperty("_MainTex")).Select(m=>m.shader).FirstOrDefault();
        if(!shader)return;
        texture=new Texture2D(32,32,TextureFormat.RGBA32,false){name="MonkStyle_AuraSoft",wrapMode=TextureWrapMode.Clamp};
        var pixels=new Color[1024];
        for(int y=0;y<32;y++)for(int x=0;x<32;x++){float r=new Vector2((x-15.5f)/15.5f,(y-15.5f)/15.5f).magnitude;float a=Mathf.Pow(Mathf.Max(0,1-r),2);pixels[y*32+x]=new Color(1,1,1,a);}
        texture.SetPixels(pixels);texture.Apply();material=new Material(shader);material.name="MonkStyle_Aura";material.mainTexture=texture;material.mainTextureScale=Vector2.one;material.mainTextureOffset=Vector2.zero;
        if(material.HasProperty("_TintColor"))material.SetColor("_TintColor",Color.white);
        if(material.HasProperty("_Color"))material.SetColor("_Color",Color.white);
        // Explicit transparent additive setup for the Standard Unlit fallback.
        if(material.HasProperty("_SrcBlend"))material.SetInt("_SrcBlend",(int)BlendMode.SrcAlpha);
        if(material.HasProperty("_DstBlend"))material.SetInt("_DstBlend",(int)BlendMode.OneMinusSrcAlpha);
        if(material.HasProperty("_ZWrite"))material.SetInt("_ZWrite",0);
        material.renderQueue=3000;
        effect=new GameObject("MonkStyle_BearAura");effect.transform.SetParent(transform,false);
        var rays=new GameObject("Rising aura");rays.transform.SetParent(effect.transform,false);rays.transform.localPosition=new Vector3(0,.12f,0);rays.transform.localRotation=Quaternion.Euler(-90,0,0);
        var particles=rays.AddComponent<ParticleSystem>();particles.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
        var main=particles.main;main.loop=true;main.playOnAwake=false;main.maxParticles=64;main.startLifetime=new ParticleSystem.MinMaxCurve(1.1f,1.7f);main.startSpeed=new ParticleSystem.MinMaxCurve(1f,1.45f);main.startSize=new ParticleSystem.MinMaxCurve(.065f,.13f);main.startColor=new Color(1,.72f,.28f,.65f);main.simulationSpace=ParticleSystemSimulationSpace.Local;
        var emission=particles.emission;emission.rateOverTime=22;
        var shape=particles.shape;shape.shapeType=ParticleSystemShapeType.Cone;shape.angle=12;shape.radius=.65f;shape.radiusThickness=.6f;
        var fade=particles.colorOverLifetime;fade.enabled=true;
        var gradient=new Gradient();gradient.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1)},new[]{new GradientAlphaKey(0,0),new GradientAlphaKey(.6f,.25f),new GradientAlphaKey(0,1)});fade.color=gradient;
        var render=particles.GetComponent<ParticleSystemRenderer>();render.sharedMaterial=material;render.renderMode=ParticleSystemRenderMode.Stretch;render.lengthScale=2.5f;render.velocityScale=.15f;render.shadowCastingMode=ShadowCastingMode.Off;render.receiveShadows=false;
        particles.Play();
        wind=new LineRenderer[3];
        for(int i=0;i<3;i++)
        {
            var go=new GameObject("Wind ribbon");go.transform.SetParent(effect.transform,false);
            var line=go.AddComponent<LineRenderer>();line.sharedMaterial=material;line.useWorldSpace=false;line.positionCount=28;line.widthMultiplier=.032f;line.shadowCastingMode=ShadowCastingMode.Off;line.receiveShadows=false;wind[i]=line;
        }
        var lightObject=new GameObject("Soft glow");lightObject.transform.SetParent(effect.transform,false);lightObject.transform.localPosition=Vector3.up;
        glow=lightObject.AddComponent<Light>();glow.type=LightType.Point;glow.color=new Color(1,.75f,.38f);glow.range=2.5f;glow.shadows=LightShadows.None;glow.intensity=.24f;
    }
    void Clear()
    {
        if(effect){effect.SetActive(false);Destroy(effect);effect=null;}
        if(material){Destroy(material);material=null;}
        if(texture){Destroy(texture);texture=null;}
    }
    void OnDisable()=>Clear();
    void OnDestroy()=>Clear();
}
