using UnityEngine;
namespace KeyLearner.Unity
{
    // One occasional visitor; bounded arrival, shared flight, then a clear departure.
    public sealed class CompanionBird
    {
        readonly GameServices services;
        readonly GameObject bird;
        readonly System.Random random=new System.Random(8214);
        float wait,age,wingClock;
        Vector3 velocity,previousPlayer;
        public Vector3 Position=>bird.transform.position;
        public int Visits { get; private set; }
        public bool Active=>bird.activeSelf;
        public float Age=>age;
        public CompanionBird(GameServices services,Transform parent)
        {
            this.services=services;
            bird=services.Content.Spawn("bird",0,parent,Vector3.zero,3.8f);
            bird.name="Visiting flight companion";bird.SetActive(false);
            wait=services.Preview && services.Options.Value("--scenario")=="bird-companion"?2:65+random.Next(45);
        }
        public void Tick(float dt,Vector3 player,Vector3 forward,float speed)
        {
            var right=Vector3.Cross(Vector3.up,forward).normalized;
            if(!Active)
            {
                wait-=dt;if(wait>0)return;
                bird.SetActive(true);age=wingClock=0;Visits++;
                bird.transform.position=player+right*70-forward*30+Vector3.up*20;
                velocity=Vector3.zero;previousPlayer=player;
                services.Audio.Play("bird-call-2",services.Settings,.08f,0,bird.transform.position);
            }
            age+=dt;wingClock-=dt;
            var target=player+right*18+forward*8+Vector3.up*(3+Mathf.Sin(age*.6f));
            if(age>13)target+=right*(age-13)*22+Vector3.up*(age-13)*7+forward*(age-13)*14;
            // Damped relative motion avoids an arrival overshoot through the child's bird.
            var old=bird.transform.position;
            var carried=old+(player-previousPlayer);previousPlayer=player;
            bird.transform.position=Vector3.SmoothDamp(carried,target,ref velocity,.65f,age>13?65:45,dt);
            var movement=bird.transform.position-old;
            if(movement.sqrMagnitude>.00001f)bird.transform.rotation=Quaternion.Slerp(bird.transform.rotation,Quaternion.LookRotation(movement)*Quaternion.Euler(0,services.Content.Category("bird")[0].Yaw,0),1-Mathf.Exp(-dt*4));
            if(wingClock<=0 && age>2 && age<13)
            {
                wingClock=3.5f+(float)random.NextDouble()*2;
                services.Audio.Play("wing-flap",services.Settings,.035f,0,bird.transform.position,.94f);
            }
            if(age>20){bird.SetActive(false);wait=110+random.Next(110);}
        }
    }
}
