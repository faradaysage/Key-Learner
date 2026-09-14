using UnityEngine;
namespace KeyLearner.Unity
{
    // Twelve soft intersecting light volumes; one mesh/material, no per-frame allocations.
    // Depth fading keeps beams from drawing across foreground fish, trees, or learning targets.
    public sealed class SunShaftField : MonoBehaviour
    {
        GameServices services;
        Transform[] beams;
        Mesh mesh;
        Material material;
        bool ocean;
        public static SunShaftField Create(GameServices services,Transform parent,bool ocean)
        {
            var go=new GameObject(ocean?"Reef sunlight volumes":"Forest sunlight volumes");go.transform.SetParent(parent,false);
            var field=go.AddComponent<SunShaftField>();field.services=services;field.ocean=ocean;field.Initialize();return field;
        }
        void Initialize()
        {
            material=new Material(Resources.Load<Shader>("Shaders/SunShaft"));
            material.SetColor("_BaseColor",ocean?new Color(.48f,.86f,1,.045f):new Color(1,.89f,.63f,.025f));
            mesh=new Mesh{name="Crossed soft light planes"};
            var vertices=new Vector3[12];var uv=new Vector2[12];var triangles=new int[18];
            for(int i=0;i<3;i++)
            {
                var across=Quaternion.Euler(0,i*60,0)*Vector3.right;int v=i*4,t=i*6;
                vertices[v]=-across;vertices[v+1]=across;vertices[v+2]=Vector3.up-across*.2f;vertices[v+3]=Vector3.up+across*.2f;
                uv[v]=new Vector2(0,0);uv[v+1]=new Vector2(1,0);uv[v+2]=new Vector2(0,1);uv[v+3]=Vector2.one;
                triangles[t]=v;triangles[t+1]=v+2;triangles[t+2]=v+1;triangles[t+3]=v+1;triangles[t+4]=v+2;triangles[t+5]=v+3;
            }
            mesh.vertices=vertices;mesh.uv=uv;mesh.triangles=triangles;mesh.RecalculateBounds();
            beams=new Transform[12];
            for(int i=0;i<beams.Length;i++)
            {
                var go=new GameObject("Sunlight "+i);go.transform.SetParent(transform,false);go.AddComponent<MeshFilter>().sharedMesh=mesh;
                var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;renderer.receiveShadows=false;
                beams[i]=go.transform;beams[i].rotation=Quaternion.FromToRotation(Vector3.up,new Vector3(-.28f,1,.18f));
                beams[i].localScale=new Vector3(11+i%3*3,ocean?82:44,11+i%3*3);
            }
        }
        void LateUpdate()
        {
            var player=services.Camera.transform.position;
            bool active=ocean?player.y<1:player.y<100;
            for(int i=0;i<beams.Length;i++)
            {
                float x=Mathf.Floor((player.x+220-(i%4)*110)/440)*440+(i%4)*110+28;
                float z=Mathf.Floor((player.z+210-(i/4)*140)/420)*420+(i/4)*140+51;
                float ground=ocean?-76:services.Settings.Mode==KeyLearner.Studio.PlayMode.Dinosaur?KeyLearner.Studio.DinosaurWorld.Ground(x,z):KeyLearner.Studio.ExplorerWorld.Height(x,z);
                bool show=active&&(ocean||ground>2);
                if(beams[i].gameObject.activeSelf!=show)beams[i].gameObject.SetActive(show);
                if(show)beams[i].position=new Vector3(x,ground,z);
            }
        }
        void OnDestroy(){if(mesh)Destroy(mesh);if(material)Destroy(material);}
    }
}
