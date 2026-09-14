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
                rigs.Add(rig);
            }
            if(rigs.Count==0)throw new InvalidOperationException("T-rex source rig is missing.");
        }
        public void Tick(float distance,float speed,float roarProgress)
        {
            string next=roarProgress>=0?"Roar":speed<.7f?"Idle":speed>17?"Run":"Walk";
            foreach(var rig in rigs)
            {
                var state=next=="Roar"?rig.Roar:next=="Idle"?rig.Idle:next=="Run"?rig.Run:rig.Walk;
                state.wrapMode=next=="Roar"?WrapMode.ClampForever:WrapMode.Loop;
                state.speed=next=="Idle"?.8f:0;
                if(Motion!=next)rig.Animation.CrossFade(state.name,.13f);
                if(next!="Idle")state.normalizedTime=next=="Roar"?Mathf.Clamp01(roarProgress):Mathf.Repeat(distance/10.4f,1);
                rig.Animation.Sample();
            }
            Motion=next;
        }
    }
}
