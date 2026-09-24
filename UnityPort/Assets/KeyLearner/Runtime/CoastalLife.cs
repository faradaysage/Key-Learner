using System.Collections.Generic;
using KeyLearner.Studio;
using UnityEngine;
namespace KeyLearner.Unity
{
    // Source-animated crew and boats placed only where the landscape supports them.
    // Ownership follows streamed chapters, so both geometry and behavior stay bounded.
    public sealed class CoastalLife
    {
        sealed class Actor { public Transform Art;public Vector3 Anchor;public float Phase,Yaw;public bool Boat,Cheered,Leader; }
        readonly List<Actor> actors=new List<Actor>();
        readonly GameServices services;
        public CoastalLife(GameServices services){this.services=services;}
        public void Populate(Transform parent,float start,int index,float centerX,ExplorerKind kind)
        {
            var area=ExplorerWorld.Area(start-80);
            if(kind==ExplorerKind.Dolphin)
            {
                if(index%9==1)Boat(parent,new Vector3(centerX+(index%2==0?-90:90),9,start-60),index);
                return;
            }
            if(area==Region.Lakes && index%5==0)
            {
                for(int offset=-250;offset<=250;offset+=35)
                {
                    float x=centerX+offset,z=start-65;
                    if(ExplorerWorld.Height(x,z)>-5 || ExplorerWorld.Height(x-18,z)>-2 || ExplorerWorld.Height(x+18,z)>-2 || ExplorerWorld.Height(x,z-22)>-2 || ExplorerWorld.Height(x,z+22)>-2)continue;
                    Boat(parent,new Vector3(x,9,z),index);break;
                }
            }
            int region=Mathf.FloorToInt(-start/ExplorerWorld.RegionLength);
            int localChapter=index-Mathf.CeilToInt(region*ExplorerWorld.RegionLength/160);
            if(kind!=ExplorerKind.Racer || localChapter!=2 || (area!=Region.Town && area!=Region.City))return;
            var rng=new System.Random(index*619+83);
            for(int i=0;i<7;i++)
            {
                float z=start-20-i*5-rng.Next(3),x=ExplorerWorld.Road(z)+41+rng.Next(5),h=ExplorerWorld.Height(x,z),height=3.2f+(float)rng.NextDouble()*.6f;
                if(h<2)continue;
                var go=services.Content.Spawn("spectator",i,parent,new Vector3(x,h+height*.5f,z),height,-90);
                if(!go)continue;
                Phase(go,(float)rng.NextDouble()*24);
                actors.Add(new Actor{Art=go.transform,Anchor=go.transform.position,Phase=i*.83f,Leader=i==0});
            }
        }
        void Boat(Transform parent,Vector3 position,int index)
        {
            var go=services.Content.SpawnWidth("boat",0,parent,position,12,index*37);
            if(!go)return;Phase(go,index*.83f);
            actors.Add(new Actor{Art=go.transform,Anchor=position,Phase=index*.73f,Yaw=index*37,Boat=true});
        }
        static void Phase(GameObject go,float phase)
        {
            foreach(var a in go.GetComponentsInChildren<Animation>())if(a.clip){a[a.clip.name].time=Mathf.Repeat(phase,a.clip.length);a[a.clip.name].speed=.9f+Mathf.Repeat(phase,.2f);}
        }
        public void Tick(float dt,Vector3 player)
        {
            for(int i=actors.Count-1;i>=0;i--)
            {
                var a=actors[i];if(!a.Art){actors.RemoveAt(i);continue;}
                float distance=(a.Anchor-player).magnitude;bool visible=distance<650;
                if(a.Art.gameObject.activeSelf!=visible)a.Art.gameObject.SetActive(visible);
                if(!visible)continue;
                if(a.Boat)
                {
                    float t=(float)services.Now+a.Phase;
                    // Two wave frequencies produce gentle heave/pitch/roll around an anchored boat.
                    a.Art.position=a.Anchor+Vector3.up*(Mathf.Sin(t*.83f)*.28f+Mathf.Sin(t*1.19f)*.12f);
                    a.Art.rotation=Quaternion.Euler(Mathf.Sin(t*.83f)*1.2f,a.Yaw,Mathf.Sin(t*1.19f)*1.7f);
                }
                else if(distance<95)
                {
                    var toward=player-a.Art.position;toward.y=0;
                    if(toward.sqrMagnitude>1)a.Art.rotation=Quaternion.Slerp(a.Art.rotation,Quaternion.LookRotation(toward),1-Mathf.Exp(-dt*2));
                    if(a.Leader && !a.Cheered && distance<65){a.Cheered=true;services.Audio.Play("crowd-cheer",services.Settings,.22f,3,a.Anchor);}
                }
            }
        }
    }
}
