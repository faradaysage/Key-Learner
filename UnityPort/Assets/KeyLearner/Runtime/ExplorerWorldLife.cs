using System.Collections.Generic;
using KeyLearner.Studio;
using UnityEngine;
namespace KeyLearner.Unity
{
    /// <summary>Small, biome-aware vignettes using the same licensed actors as future games.</summary>
    public sealed class ExplorerWorldLife
    {
        sealed class Actor { public Transform Transform; public Vector3 Origin; public float Phase, Scale; public int Kind; public CreatureLocomotion Creature; public Transform Leader; public Vector3 Velocity,Target; public float Decision,Travel; public bool SurfaceInitialized,Above; }
        readonly System.Random decisions=new System.Random(45177);
        readonly List<Actor> actors = new List<Actor>();
        readonly GameServices services;
        readonly ExplorerKind kind;
        readonly Transform effectsParent;
        float worldTime;
        public ExplorerWorldLife(GameServices services, ExplorerKind kind, Transform parent) { this.services=services;this.kind=kind;effectsParent=parent; }
        void Add(string category,int index,Transform parent,Vector3 origin,float size,int behavior,float phase)
        {
            var go=services.Content.Spawn(category,index,parent,origin,size);
            if(go)
            {
                actors.Add(new Actor{Transform=go.transform,Origin=origin,Kind=behavior,Phase=phase,Scale=size,Creature=behavior==2?new CreatureLocomotion(go,origin-Vector3.up*size*.5f,size,Mathf.RoundToInt(origin.z*71+origin.x*13),1.8f,20):null});
                foreach (var animation in go.GetComponentsInChildren<Animation>())
                    if (animation.clip)
                    {
                        var state = animation[animation.clip.name];
                        state.time = Mathf.Repeat(phase, animation.clip.length);
                        state.speed = services.Settings.GentleMotion ? .7f : .94f + Mathf.Repeat(phase,.12f);
                    }
            }
        }
        public void Populate(Transform parent,float start,int index,float centerX)
        {
            if(kind!=ExplorerKind.Bird && kind!=ExplorerKind.Racer)return;
            Region area=ExplorerWorld.Area(start-80);
            if(kind==ExplorerKind.Bird && index%4==0)
            {
                // A passing V formation, with independently phased source wing animation.
                float x=centerX+40,z=start-75,y=ExplorerWorld.Height(x,z)+75;
                for(int i=0;i<5;i++)Add("bird",0,parent,new Vector3(x+(i%2==0?-1:1)*(i+1)*5,y+(i%2)*1.5f,z+i*6),2.7f,0,index+i*.7f);
            }
            if(kind==ExplorerKind.Bird && (area==Region.City || area==Region.Town))
                for(int i=0;i<2;i++)
                {
                    float z=start-35-i*65;
                    Add("car",index+i,parent,new Vector3(centerX+ExplorerWorld.Valley(z)*.35f+(i==0?-5:5),ExplorerWorld.Height(centerX+ExplorerWorld.Valley(z)*.35f,z)+.15f,z),2.5f,1,i*Mathf.PI);
                }
            else if(area==Region.Forest || area==Region.Mountains || area==Region.Tundra)
            {
                if(index%3!=0)return;
                string category=area==Region.Tundra?"polar-bear":area==Region.Forest && index%2==0?"wolf":"animal";
                // Mother and cub retain the supplied walk cycle and texture.
                Transform mother=null;
                for(int i=0;i<2;i++)
                {
                    float z=start-65+i*7;
                    float x=kind==ExplorerKind.Racer?ExplorerWorld.Road(z)+75+i*12:centerX+55+i*12,y=ExplorerWorld.Height(x,z);
                    if(y>2)
                    {
                        float height=category=="polar-bear"?(i==0?7.2f:4.1f):(i==0?4.2f:2.5f);
                        Add(category,index,parent,new Vector3(x,y+height*.5f,z),height,2,i*.6f);
                        if(category=="polar-bear" && actors.Count>0){var actor=actors[actors.Count-1];if(i==0)mother=actor.Transform;else actor.Leader=mother;}
                    }
                }
            }
            else if(area==Region.Lakes && index%2==0)
            {
                for(int x=-150;x<=150;x+=25)
                {
                    float px=centerX+x,z=start-85;
                    if(ExplorerWorld.Height(px,z)<-3){Add("dolphin",0,parent,new Vector3(px,-6,z),5.6f,3,index);break;}
                }
            }
        }
        public void Tick(float dt,Vector3 player)
        {
            worldTime+=dt*(services.Settings.GentleMotion?.55f:1);
            float time=worldTime;
            for(int i=actors.Count-1;i>=0;i--)
            {
                var a=actors[i];if(!a.Transform){actors.RemoveAt(i);continue;}
                bool visible=((a.Kind==1?a.Transform.position:a.Origin)-player).sqrMagnitude<440*440;
                if(a.Transform.gameObject.activeSelf!=visible)a.Transform.gameObject.SetActive(visible);
                if(!visible)continue;
                if(a.Creature!=null)
                {
                    if(a.Leader)a.Creature.Mind.Follow(new System.Numerics.Vector3(a.Leader.position.x,a.Leader.position.y-a.Scale*.5f,a.Leader.position.z));
                    a.Creature.Tick(dt,player,q=>ExplorerWorld.Height(q.x,q.z),q=>ExplorerWorld.Height(q.x,q.z)>2 && (kind!=ExplorerKind.Racer || Mathf.Abs(q.x-ExplorerWorld.Road(q.z))>36));
                    continue;
                }
                float t=time+a.Phase;Vector3 p=a.Origin,forward=Vector3.forward;
                if(a.Kind==0)
                {
                    a.Decision-=dt;
                    if(a.Decision<=0){a.Decision=4+(float)decisions.NextDouble()*6;a.Target=a.Origin+new Vector3(decisions.Next(-80,81),decisions.Next(-8,16),decisions.Next(-110,111));}
                    var delta=a.Target-a.Transform.position;
                    if((a.Transform.position-player).sqrMagnitude<40*40)delta=(a.Transform.position-player).normalized*70+Vector3.up*15;
                    var desired=delta.sqrMagnitude>1?delta.normalized*15:Vector3.forward*15;
                    a.Velocity=Vector3.MoveTowards(a.Velocity,desired,dt*5);p=a.Transform.position+a.Velocity*dt;forward=a.Velocity;
                }
                if(a.Kind==1)
                {
                    a.Travel+=dt*(7+Mathf.Repeat(a.Phase,3));p.z-=a.Travel;

                    p.x+=(ExplorerWorld.Valley(p.z)-ExplorerWorld.Valley(a.Origin.z))*.35f;
                    p.y=ExplorerWorld.Height(p.x,p.z)+.15f;
                    forward=-new Vector3((ExplorerWorld.Valley(p.z+1)-ExplorerWorld.Valley(p.z-1))*.175f,0,1);
                }
                if(a.Kind==2){p.x+=Mathf.Sin(t*.18f)*14;p.z+=Mathf.Cos(t*.18f)*7;p.y=ExplorerWorld.Height(p.x,p.z)+a.Scale*.5f;forward=new Vector3(Mathf.Cos(t*.18f)*2,0,-Mathf.Sin(t*.18f));}
                if(a.Kind==3)
                {
                    float cycle=Mathf.Repeat(t,18),flight=cycle-5;
                    bool jumping=flight>=0 && flight<4.4f;
                    p.y=jumping?-6+20*flight-5*flight*flight:-8;
                    p.z-=jumping?flight*10:0;
                    forward=jumping?new Vector3(0,20-10*flight,-10):Vector3.back;
                    bool above=p.y>0;
                    if(a.SurfaceInitialized && above!=a.Above)
                        WaterSplash.Create(services,effectsParent,p,35,!above);
                    a.SurfaceInitialized=true;a.Above=above;
                }
                a.Transform.position=p;
                if(forward.sqrMagnitude>.001f)a.Transform.rotation=Quaternion.LookRotation(forward);
            }
        }
    }
}
