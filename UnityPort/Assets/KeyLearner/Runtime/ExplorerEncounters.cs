using System;
using System.Collections.Generic;
using System.Linq;
using KeyLearner.Studio;
using UnityEngine;
namespace KeyLearner.Unity
{
    /// <summary>Bounded, optional discoveries alongside the spelling course.</summary>
    public sealed class ExplorerEncounters : IDisposable
    {
        sealed class Pickup { public GameObject Object; public Vector3 Origin; public float Age; public bool Treasure, Bubble; }
        readonly List<Pickup> pickups = new List<Pickup>();
        readonly GameServices services;
        readonly Transform parent;
        readonly ExplorerKind kind;
        readonly System.Random random = new System.Random(7351);
        readonly Material bubble;
        float next, spin;
        Vector3 previous;
        bool hasPrevious;
        string message = "";
        double messageUntil;
        public int BubblesPopped { get; private set; }
        public int ObstaclesHit { get; private set; }
        public float Spin => services.Settings.GentleMotion ? 0 : spin;
        public ExplorerEncounters(GameServices services, Transform parent, ExplorerKind kind)
        {
            this.services=services;this.parent=parent;this.kind=kind;
            next=kind==ExplorerKind.Dolphin?4:6;
            if(services.Preview && services.Options.Value("--scenario").StartsWith("racer-")) next=.8f;
            bubble = new Material(Resources.Load<Shader>("Shaders/Pearl"));
            bubble.SetColor("_BaseColor", new Color(.45f,.88f,1));
        }
        static Vector3 V(System.Numerics.Vector3 p)=>new Vector3(p.X,p.Y,p.Z);
        public void Tick(float dt, FlightModel model)
        {
            spin = model.ObstacleRemaining > 0 ? 720 * (1 - model.ObstacleRemaining / 1.5f) : 0;
            next -= dt;
            if(next<=0 && pickups.Count<5 && kind!=ExplorerKind.Bird)
            {
                next=kind==ExplorerKind.Dolphin?7+random.Next(4):8+random.Next(5);
                var position=V(model.Position)+V(model.Forward)*(kind==ExplorerKind.Racer?150:68);
                bool water=kind==ExplorerKind.Dolphin, treasure=!water && random.Next(3)==0;
                string fixture=services.Preview?services.Options.Value("--scenario"):"";
                if(fixture=="racer-treasure") treasure=true;
                if(fixture=="racer-obstacle") treasure=false;
                if(water)
                {
                    position += new Vector3(random.Next(-15,16), random.Next(-5,6),0);
                    position.y = Mathf.Min(-8, position.y);
                }
                else position=new Vector3(ExplorerWorld.Road(position.z)+(fixture.StartsWith("racer-")?0:(random.Next(3)-1)*10),6,position.z);
                GameObject go=water?Visuals.Sphere(parent,position,3.5f,Color.white):services.Content.Spawn(treasure?"treasure":"road-obstacle",random.Next(2),parent,position,treasure?3.5f:2.5f);
                if(go)
                {
                    if(water) go.GetComponent<Renderer>().sharedMaterial=bubble;
                    pickups.Add(new Pickup{Object=go,Origin=position,Bubble=water,Treasure=treasure});
                }
            }
            for(int i=pickups.Count-1;i>=0;i--)
            {
                var p=pickups[i];p.Age+=dt;
                if(p.Bubble) p.Object.transform.position=p.Origin+new Vector3(Mathf.Sin(p.Age)*1.3f,p.Age*.4f,0);
                if(p.Treasure) p.Object.transform.rotation=Quaternion.Euler(0,Mathf.Sin(p.Age)*12,0);
                float radius=p.Bubble?6:5;
                Vector3 current=V(model.Position), from=hasPrevious?previous:current;
                Vector3 travel=current-from;
                float along=travel.sqrMagnitude<.0001f?0:Mathf.Clamp01(Vector3.Dot(p.Object.transform.position-from,travel)/travel.sqrMagnitude);
                if((p.Object.transform.position-(from+travel*along)).sqrMagnitude<radius*radius)
                    Collect(i,model);
                else if(p.Age>30 || (p.Object.transform.position-V(model.Position)).sqrMagnitude>500*500)
                {UnityEngine.Object.Destroy(p.Object);pickups.RemoveAt(i);}
            }
            previous=V(model.Position);hasPrevious=true;
        }
        void Collect(int index,FlightModel model)
        {
            var p=pickups[index];var pos=p.Object.transform.position;
            if(p.Bubble){model.AwardBubble();BubblesPopped++;message="Bubble bonus +5";}
            else if(p.Treasure){model.AwardTreasure();message="Treasure! +25 · New look";}
            else
            {
                if(!model.HitObstacle())return;
                ObstaclesHit++;message="Whoops! Keep going";
            }
            services.Audio.Play(p.Bubble?"bubble-pop":p.Treasure?"treasure":"road-bump",services.Settings,.4f,.15);
            services.Rewards.Burst(pos,p.Bubble?Style.Blue:Style.Dots,p.Bubble?20:35,5);
            messageUntil=services.Now+2.2;
            UnityEngine.Object.Destroy(p.Object);pickups.RemoveAt(index);
        }
        public bool Pointer(Vector2 logical,FlightModel model)
        {
            Ray ray=services.Camera.ScreenPointToRay(services.ScreenFromLogical(logical));
            int selected=-1;float closest=float.PositiveInfinity;
            for(int i=0;i<pickups.Count;i++)
            {
                var p=pickups[i];if(!p.Bubble)continue;
                Vector3 delta=p.Object.transform.position-ray.origin;
                float distance=Vector3.Dot(delta,ray.direction);
                if(distance<=0 || distance>=closest)continue;
                if((delta-ray.direction*distance).sqrMagnitude<16){selected=i;closest=distance;}
            }
            if(selected<0)return false;Collect(selected,model);return true;
        }
        public object[] PreviewTargets() => pickups.Where(p=>p.Bubble).Select(p=>
        {
            var point=services.Camera.WorldToScreenPoint(p.Object.transform.position);
            return (object)new { x=point.x, y=Screen.height-point.y, visible=point.z>0 && point.x>0 && point.x<Screen.width && point.y>0 && point.y<Screen.height };
        }).ToArray();
        public void DrawUI()
        {
            if(services.Now<messageUntil)Ui.Label(new Rect(400,185,640,65),message,31,Style.Dots);
        }
        public void Dispose(){if(bubble)UnityEngine.Object.Destroy(bubble);}
    }
}
