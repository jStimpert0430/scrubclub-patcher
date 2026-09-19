using System.Linq;
using UnityEngine;
namespace Nimbus;
internal static class CloudModel
{
    internal static void Create(GameObject root)
    {
        var source=root.GetComponentsInChildren<MeshRenderer>(true).First(r=>r.sharedMaterial);
        var shader=Shader.Find("Standard");var material=shader?new Material(shader):new Material(source.sharedMaterial);
        material.name="Nimbus white cloud";
        material.DisableKeyword("_EMISSION");
        if(material.HasProperty("_EmissionColor"))material.SetColor("_EmissionColor",Color.black);
        if(material.HasProperty("_SpecularHighlights"))material.SetFloat("_SpecularHighlights",0);
        if(material.HasProperty("_GlossyReflections"))material.SetFloat("_GlossyReflections",0);
        if(material.HasProperty("_Color"))material.SetColor("_Color",new Color(.72f,.74f,.77f));
        if(material.HasProperty("_MainTex"))material.SetTexture("_MainTex",Texture2D.whiteTexture);
        if(material.HasProperty("_BumpMap"))material.SetTexture("_BumpMap",null);
        if(material.HasProperty("_Metallic"))material.SetFloat("_Metallic",0);
        if(material.HasProperty("_Glossiness"))material.SetFloat("_Glossiness",.05f);
        var model=new GameObject("Nimbus white cloud");model.transform.SetParent(root.transform,false);
        var positions=new[]{new Vector3(0,0,0),new Vector3(-.4f,.05f,.35f),new Vector3(.4f,.08f,.3f),new Vector3(-.38f,.02f,-.35f),new Vector3(.4f,.03f,-.4f),new Vector3(0,.06f,.68f),new Vector3(0,0,-.72f),new Vector3(-.65f,0,0),new Vector3(.65f,.02f,0)};
        for(int i=0;i<positions.Length;i++)
        {
            var puff=GameObject.CreatePrimitive(PrimitiveType.Sphere);puff.name="Cloud puff";puff.transform.SetParent(model.transform,false);puff.transform.localPosition=positions[i];
            puff.transform.localScale=i==0?new Vector3(1.3f,.44f,1.65f):new Vector3(.7f,.4f,.8f);
            Object.DestroyImmediate(puff.GetComponent<Collider>());puff.GetComponent<Renderer>().sharedMaterial=material;
        }
        var texture=new Texture2D(64,64,TextureFormat.RGBA32,false){name="Nimbus icon"};var colors=new Color[4096];
        for(int y=0;y<64;y++)for(int x=0;x<64;x++)
        {
            float nx=(x-32)/25f,ny=(y-30)/15f;bool inside=nx*nx+ny*ny<1 || (nx+.6f)*(nx+.6f)*4+(ny-.05f)*(ny-.05f)*2<1 || (nx-.6f)*(nx-.6f)*4+(ny-.12f)*(ny-.12f)*2<1;
            if(inside)colors[y*64+x]=Color.Lerp(new Color(.62f,.67f,.74f),Color.white,Mathf.Clamp01((ny+1)/1.5f));
        }
        texture.SetPixels(colors);texture.Apply();root.GetComponent<Piece>().m_icon=Sprite.Create(texture,new Rect(0,0,64,64),new Vector2(.5f,.5f));
    }
}
