using System;
using KeyLearner.Studio;
using UnityEngine;
namespace KeyLearner.Unity
{
    /// <summary>Source gait selection and grounding over the inexpensive shared wildlife mind.</summary>
    public sealed class CreatureLocomotion
    {
        public readonly Transform Transform;
        public readonly WildlifeMotion Mind;
        readonly float height,walkingSpeed;
        readonly Animation animation;
        readonly string walk,idle,run;
        string playing;
        public CreatureLocomotion(GameObject go,Vector3 feet,float height,int seed,float speed=2,float alert=18)
        {
            Transform=go.transform;this.height=height;walkingSpeed=speed;
            Mind=new WildlifeMotion(N(feet),seed,28,speed,alert);
            animation=go.GetComponentInChildren<Animation>();
            if(animation && animation.clip)
            {
                walk=animation.clip.name;
                foreach(AnimationState state in animation)
                {
                    if(state.name.IndexOf("Idle",StringComparison.OrdinalIgnoreCase)>=0 && idle==null)idle=state.name;
                    if(state.name.IndexOf("Run",StringComparison.OrdinalIgnoreCase)>=0 && run==null)run=state.name;
                }
                animation[walk].time=Mathf.Repeat(seed*.173f,animation.clip.length);playing=walk;
            }
        }
        static System.Numerics.Vector3 N(Vector3 p)=>new System.Numerics.Vector3(p.x,p.y,p.z);
        public void Startle()=>Mind.Startle();
        public void Tick(float dt,Vector3 player,Func<Vector3,float> ground,Func<Vector3,bool> allowed)
        {
            if(!Transform)return;
            Mind.Step(dt,N(player),p=>allowed(new Vector3(p.X,p.Y,p.Z)));
            var position=Mind.Position;var feet=new Vector3(position.X,0,position.Z);feet.y=ground(feet);Mind.SetGroundHeight(feet.y);
            Transform.position=feet+Vector3.up*height*.5f;
            var forward=Mind.Forward;Transform.rotation=Quaternion.LookRotation(new Vector3(forward.X,0,forward.Z));
            if(!animation || walk==null)return;
            bool stopped=Mind.Speed<.12f;
            string next=stopped && idle!=null?idle:Mind.State==WildlifeState.Flee && run!=null?run:walk;
            if(playing!=next){animation.CrossFade(next,.25f);playing=next;}
            // Hold a supported source pose if the pack has no idle; never moonwalk in place.
            animation[next].speed=stopped?(next==idle?1:0):Mathf.Clamp(Mind.Speed/(next==run?walkingSpeed*2.2f:walkingSpeed),.2f,2.6f);
        }
    }
}
