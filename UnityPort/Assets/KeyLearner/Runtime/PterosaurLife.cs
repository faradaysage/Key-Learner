using System.Collections.Generic;
using UnityEngine;
namespace KeyLearner.Unity
{
    // Six source-animated animals share only a thermal location, not a clock or path.
    // Infrequent decisions and bounded smooth steering give each an independent flight.
    public sealed class PterosaurLife
    {
        sealed class Flyer
        {
            public Transform Art;
            public Vector3 Velocity,Target;
            public float Decision,Alarm;
            public Animation Animation;
            public AnimationState Clip;
            public int Side;
            public Renderer[] Renderers;
        }
        readonly GameServices services;
        readonly List<Flyer> flyers=new List<Flyer>();
        readonly System.Random random=new System.Random(43191);
        public int Count=>flyers.Count;
        public int Visible { get { int n=0;foreach(var f in flyers)foreach(var r in f.Renderers)if(r.isVisible){n++;break;}return n; } }
        float Range(float a,float b)=>Mathf.Lerp(a,b,(float)random.NextDouble());
        public PterosaurLife(GameServices services,Transform parent)
        {
            this.services=services;
            for(int i=0;i<6;i++)
            {
                var go=services.Content.SpawnWidth("pterosaur",0,parent,Vector3.zero,Range(9,14));
                if(!go)throw new System.InvalidOperationException("Animated Pteranodon catalog missing.");
                var a=go.GetComponentInChildren<Animation>();
                var f=new Flyer{Art=go.transform,Side=i%2==0?-1:1,Velocity=new Vector3(0,0,Range(12,19)),Animation=a,Renderers=go.GetComponentsInChildren<Renderer>()};
                if(a && a.clip){f.Clip=a[a.clip.name];f.Clip.time=Range(0,a.clip.length);f.Clip.speed=Range(.7f,1.05f);}
                f.Art.position=new Vector3(f.Side*Range(110,180),Range(65,115),Range(180,320));
                flyers.Add(f);
            }
        }
        public void Startle(Vector3 source)
        {
            foreach(var f in flyers)if((f.Art.position-source).sqrMagnitude<230*230){f.Alarm=4;f.Decision=0;}
        }
        public void Tick(float dt,Vector3 player)
        {
            foreach(var f in flyers)
            {
                // Recycle only well behind or beyond the visible horizon, never in front of the player.
                if(f.Art.position.z<player.z-470 || f.Art.position.z>player.z+950 || Mathf.Abs(f.Art.position.x-player.x)>1000)
                {f.Art.position=new Vector3(player.x+f.Side*Range(240,340),Range(125,185),player.z+Range(480,690));f.Decision=0;}
                f.Decision-=dt;f.Alarm=Mathf.Max(0,f.Alarm-dt);
                if(f.Decision<=0)
                {
                    f.Decision=Range(5,11);
                    var thermal=new Vector3(player.x+f.Side*170,Range(85,140),player.z+230);
                    var radial=f.Art.position-thermal;radial.y=0;
                    var tangent=Vector3.Cross(Vector3.up,radial).normalized;
                    f.Target=thermal+radial.normalized*Range(55,125)+tangent*Range(75,130);
                    if(f.Alarm>0)f.Target=f.Art.position+(f.Art.position-player).normalized*160+Vector3.up*35;
                }
                var desired=(f.Target-f.Art.position).normalized*(f.Alarm>0?27:17);
                var before=f.Velocity;
                f.Velocity=Vector3.MoveTowards(f.Velocity,desired,dt*4.5f);
                f.Art.position+=f.Velocity*dt;
                if(f.Velocity.sqrMagnitude>1)
                {
                    float bank=Mathf.Clamp(-Vector3.SignedAngle(before,f.Velocity,Vector3.up)*8,-24,24);
                    var rotation=Quaternion.LookRotation(f.Velocity)*Quaternion.Euler(0,0,bank);
                    f.Art.rotation=Quaternion.Slerp(f.Art.rotation,rotation,1-Mathf.Exp(-dt*2.5f));
                }
                if(f.Clip!=null)f.Clip.speed=Mathf.Lerp(f.Clip.speed,f.Alarm>0?1.25f:.74f,1-Mathf.Exp(-dt*2));
            }
        }
    }
}
