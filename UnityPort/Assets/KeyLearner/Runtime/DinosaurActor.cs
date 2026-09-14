using System;
using System.Collections.Generic;
using UnityEngine;
namespace KeyLearner.Unity
{
    /// <summary>Authored T-rex poses with stride timing shared by feet, sound and camera impulses.</summary>
    public sealed class DinosaurActor
    {
        sealed class Rig
        {
            public Animation Animation;
            public AnimationState Walk,Run,Idle,Roar;
        }
        readonly List<Rig> rigs=new List<Rig>();
        public string Motion {get;private set;}="";
        public string Gait {get;private set;}="";
        bool wasRoaring;
        public DinosaurActor(GameObject body)
        {
            foreach(var animation in body.GetComponentsInChildren<Animation>())
            {
                var rig=new Rig{Animation=animation};
                foreach(AnimationState state in animation)
                {
                    if(state.name.IndexOf("Walk",StringComparison.OrdinalIgnoreCase)>=0)rig.Walk=state;
                    if(state.name.IndexOf("Run",StringComparison.OrdinalIgnoreCase)>=0)rig.Run=state;
                    if(state.name.IndexOf("Idle",StringComparison.OrdinalIgnoreCase)>=0)rig.Idle=state;
                    // This source take supplies jaw/head movement; it has no attack gameplay behavior.
                    if(state.name.IndexOf("Attack",StringComparison.OrdinalIgnoreCase)>=0)rig.Roar=state;
                }
                if(rig.Walk==null || rig.Run==null || rig.Idle==null || rig.Roar==null)
                    throw new InvalidOperationException("T-rex source animation set is incomplete.");
                Transform neck=null;
                foreach(var transform in animation.GetComponentsInChildren<Transform>())
                    if(transform.name=="Neck"){neck=transform;break;}
                if(!neck)throw new InvalidOperationException("T-rex source neck bone is missing.");
                rig.Roar.layer=1;rig.Roar.wrapMode=WrapMode.ClampForever;rig.Roar.speed=0;
                rig.Roar.AddMixingTransform(neck,true);
                rigs.Add(rig);
            }
            if(rigs.Count==0)throw new InvalidOperationException("T-rex source rig is missing.");
        }
        public void Tick(float distance,float speed,float roarProgress)
        {
            bool roaring=roarProgress>=0;
            string gait=speed<.7f?"Idle":speed>17?"Run":"Walk";
            foreach(var rig in rigs)
            {
                var state=gait=="Idle"?rig.Idle:gait=="Run"?rig.Run:rig.Walk;
                state.wrapMode=WrapMode.Loop;state.speed=gait=="Idle"?.8f:0;
                if(Gait!=gait)rig.Animation.CrossFade(state.name,.13f);
                if(gait!="Idle")state.normalizedTime=Mathf.Repeat(distance/10.4f,1);
                // The original neck/head action overlays the gait; the feet keep moving.
                if(roaring)
                {
                    if(!wasRoaring)rig.Animation.CrossFade(rig.Roar.name,.13f);
                    rig.Roar.normalizedTime=Mathf.Clamp01(roarProgress);
                }
                else if(wasRoaring)rig.Animation.Blend(rig.Roar.name,0,.13f);
                rig.Animation.Sample();
            }
            Gait=gait;Motion=roaring?"Roar":gait;wasRoaring=roaring;
        }
    }
}
