using System.Collections.Generic;
using KeyLearner.Studio;
using UnityEngine;
namespace KeyLearner.Unity
{
    // Tire contacts emit into one shared, fixed-size particle pool.
    public sealed class TireWake
    {
        readonly GameServices services;
        readonly ParticleSystem particles;
        readonly List<Transform> wheels=new List<Transform>();
        float timer,soundTimer;
        int serial;
        public int Emitted { get; private set; }
        public TireWake(GameServices services,Transform parent,GameObject vehicle)
        {
            this.services=services;Bind(vehicle);
            var go=new GameObject("Tire contact dust");go.transform.SetParent(parent,false);
            particles=go.AddComponent<ParticleSystem>();particles.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var main=particles.main;main.playOnAwake=false;main.loop=false;main.startSpeed=0;main.startLifetime=.9f;main.startSize=.5f;main.maxParticles=180;main.simulationSpace=ParticleSystemSimulationSpace.World;
            var emission=particles.emission;emission.enabled=false;var shape=particles.shape;shape.enabled=false;
            var fade=particles.colorOverLifetime;fade.enabled=true;var gradient=new Gradient();
            gradient.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1)},new[]{new GradientAlphaKey(.45f,0),new GradientAlphaKey(.25f,.4f),new GradientAlphaKey(0,1)});fade.color=gradient;
            var size=particles.sizeOverLifetime;size.enabled=true;size.size=new ParticleSystem.MinMaxCurve(1,AnimationCurve.Linear(0,.35f,1,2));
            particles.GetComponent<ParticleSystemRenderer>().sharedMaterial=Visuals.DustMaterial();particles.Play();
        }
        public void Bind(GameObject vehicle)
        {
            wheels.Clear();foreach(var t in vehicle.GetComponentsInChildren<Transform>())if(t.name.StartsWith("wheel-") && t.name.Contains("back"))wheels.Add(t);
            if(wheels.Count==0)foreach(var t in vehicle.GetComponentsInChildren<Transform>())if(t.name.StartsWith("wheel-"))wheels.Add(t);
        }
        public void Tick(float dt,Vector3 forward,float speed,bool braking)
        {
            timer-=dt;soundTimer-=dt;if(timer>0 || speed<8)return;
            timer=services.Settings.GentleMotion?.16f:.075f;
            foreach(var wheel in wheels)
            {
                if(!wheel)continue;var p=wheel.position;
                bool verge=Mathf.Abs(p.x-ExplorerWorld.Road(p.z))>19.2f;
                if(!verge && !(braking && speed>45))continue;
                p.y=verge?ExplorerWorld.Land(p.x,p.z,true)+.2f:6;
                for(int i=0;i<(services.Settings.GentleMotion?1:3);i++)
                {
                    float a=(serial++)*2.399f;
                    particles.Emit(new ParticleSystem.EmitParams{position=p,velocity=-forward*(speed*.035f)+new Vector3(Mathf.Sin(a)*.7f,.8f,Mathf.Cos(a)*.7f),startSize=verge?.65f:.32f,startColor=verge?new Color(.57f,.47f,.31f,.4f):new Color(.65f,.7f,.72f,.22f)},1);Emitted++;
                }
                if(verge && soundTimer<=0){services.Audio.Play("road-bump",services.Settings,.035f,0,p,.75f);soundTimer=.65f;}
            }
        }
    }
}
