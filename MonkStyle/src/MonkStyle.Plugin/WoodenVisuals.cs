using System;
using System.Collections.Generic;
using System.Linq;
using Jotunn.Managers;
using MonkStyle.Core;
using UnityEngine;
namespace MonkStyle;
internal static class WoodenVisuals
{
    internal static void Apply(GameObject prefab,Weapon rule)
    {
        var skin=prefab.GetComponentInChildren<SkinnedMeshRenderer>(true);
        if(!skin)throw new InvalidOperationException("Missing fist skin on "+prefab.name);
        var wood=PrefabManager.Instance.GetPrefab(rule.Wood).GetComponentsInChildren<MeshRenderer>(true).FirstOrDefault();
        var material=new Material(wood?wood.sharedMaterial:skin.sharedMaterial);
        if(material.HasProperty("_Color"))material.color=rule.Binding=="LeatherScraps"?new Color(.68f,.43f,.23f):rule.Binding=="Silver"?new Color(.86f,.66f,.4f):new Color(.37f,.25f,.17f);
        var band=new Material(skin.sharedMaterial);
        if(band.HasProperty("_Color"))band.color=rule.Binding=="LeatherScraps"?new Color(.23f,.13f,.07f):rule.Binding=="Silver"?new Color(.7f,.75f,.8f):new Color(.2f,.23f,.25f);
        var primitive=GameObject.CreatePrimitive(PrimitiveType.Cube);primitive.SetActive(false);
        var cube=primitive.GetComponent<MeshFilter>().sharedMesh;
        var vertices=new List<Vector3>();var uvs=new List<Vector2>();var weights=new List<BoneWeight>();
        var woodTriangles=new List<int>();var bandTriangles=new List<int>();
        var binds=skin.sharedMesh.bindposes;
        foreach(var hand in new[]{"LeftHand","RightHand"})
        {
            int bone=Array.FindIndex(skin.bones,b=>b && b.name==hand);
            if(bone<0)throw new InvalidOperationException("Missing fist bone "+hand);
            // Source rig is centimetre-scaled (renderer scale 100). Bind-pose
            // transforms place the geometry in that rig's mesh coordinate space.
            var toMesh=binds[bone].inverse;
            AddBox(new Vector3(0,.00045f,0),new Vector3(.0011f,.0007f,.0006f),toMesh,bone,woodTriangles);
            AddBox(new Vector3(0,.0000f,0),new Vector3(.00095f,.00023f,.0007f),toMesh,bone,bandTriangles);
            AddBox(new Vector3(0,.00065f,0),new Vector3(.00118f,.00017f,.00068f),toMesh,bone,bandTriangles);
        }
        var mesh=new Mesh{name=prefab.name+"_wooden_knuckles"};mesh.SetVertices(vertices);mesh.SetUVs(0,uvs);
        mesh.boneWeights=weights.ToArray();mesh.bindposes=binds;mesh.subMeshCount=2;
        mesh.SetTriangles(woodTriangles,0);mesh.SetTriangles(bandTriangles,1);mesh.RecalculateNormals();mesh.RecalculateBounds();
        skin.sharedMesh=mesh;skin.sharedMaterials=new[]{material,band};skin.localBounds=mesh.bounds;
        // Ground/display model: replace claw meshes with a pair of carved blocks.
        foreach(var filter in prefab.GetComponentsInChildren<MeshFilter>(true))
        {
            var renderer=filter.GetComponent<MeshRenderer>();if(!renderer)continue;
            filter.sharedMesh=GroundMesh(cube);renderer.sharedMaterials=new[]{material};
        }
        UnityEngine.Object.Destroy(primitive);
        prefab.GetComponent<ItemDrop>().m_itemData.m_shared.m_icons=new[]{Icon(material,band)};
        void AddBox(Vector3 center,Vector3 size,Matrix4x4 matrix,int bone,List<int> indices)
        {
            int start=vertices.Count;var cv=cube.vertices;var uv=cube.uv;
            for(int i=0;i<cv.Length;i++){vertices.Add(matrix.MultiplyPoint3x4(center+Vector3.Scale(cv[i],size)));uvs.Add(uv[i]);weights.Add(new BoneWeight{boneIndex0=bone,weight0=1});}
            indices.AddRange(cube.triangles.Select(i=>start+i));
        }
    }
    static Mesh GroundMesh(Mesh cube)
    {
        var mesh=new Mesh{name="MonkStyle_ground_knuckles"};var vertices=new List<Vector3>();var uv=new List<Vector2>();var tris=new List<int>();
        for(int side=0;side<2;side++)
        {
            int offset=vertices.Count;vertices.AddRange(cube.vertices.Select(v=>Vector3.Scale(v,new Vector3(.12f,.07f,.08f))+new Vector3((side-.5f)*.15f,0,0)));
            uv.AddRange(cube.uv);tris.AddRange(cube.triangles.Select(i=>i+offset));
        }
        mesh.SetVertices(vertices);mesh.SetUVs(0,uv);mesh.SetTriangles(tris,0);mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
    }
    static Sprite Icon(Material wood,Material band)
    {
        if(UnityEngine.SystemInfo.graphicsDeviceType==UnityEngine.Rendering.GraphicsDeviceType.Null)
            return Sprite.Create(Texture2D.whiteTexture,new Rect(0,0,1,1),new Vector2(.5f,.5f));
        var preview=new GameObject("MonkStyle icon preview");
        try
        {
            // Photograph a pair of wood knuckle grips with their binding bands,
            // using the game's material lighting and a transparent background.
            for(int hand=0;hand<2;hand++)
            {
                var grip=new GameObject("Knuckle grip");grip.transform.SetParent(preview.transform,false);
                grip.transform.localPosition=new Vector3((hand-.5f)*.8f,0,(hand-.5f)*.3f);
                grip.transform.localRotation=Quaternion.Euler(0,hand==0?-12:12,0);
                Box(grip.transform,new Vector3(0,0,0),new Vector3(.62f,.28f,.5f),wood);
                for(int n=0;n<4;n++)Box(grip.transform,new Vector3(-.24f+n*.16f,.14f,.12f),new Vector3(.145f,.13f,.24f),wood);
                foreach(float x in new[]{-.2f,.2f})Box(grip.transform,new Vector3(x,.01f,0),new Vector3(.07f,.3f,.52f),band);
            }
            return RenderManager.Instance.Render(new RenderManager.RenderRequest(preview){Width=128,Height=128,Rotation=Quaternion.Euler(28,-25,12),UseCache=false});
        }
        finally{UnityEngine.Object.DestroyImmediate(preview);}
    }
    static void Box(Transform parent,Vector3 position,Vector3 scale,Material material)
    {
        var part=GameObject.CreatePrimitive(PrimitiveType.Cube);UnityEngine.Object.DestroyImmediate(part.GetComponent<Collider>());
        part.transform.SetParent(parent,false);part.transform.localPosition=position;part.transform.localScale=scale;
        part.GetComponent<Renderer>().sharedMaterial=material;
    }
}
