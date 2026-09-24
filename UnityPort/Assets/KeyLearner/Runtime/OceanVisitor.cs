using UnityEngine;
namespace KeyLearner.Unity
{
    /// <summary>A rare, harmless shark pass. The school reacts before the visitor reaches view.</summary>
    public sealed class OceanVisitor
    {
        readonly GameServices services;
        readonly Transform parent;
        readonly System.Random random;
        GameObject shark;
        float wait,elapsed=-1;
        Vector3 ahead,side,entry,velocity;
        public Vector3 Position=>shark?shark.transform.position:Vector3.zero;
        public int Visits {get;private set;}
        public bool Warning=>elapsed>=0 && elapsed<3;
        public bool Active=>elapsed>=0;
        public bool Visible=>elapsed>5 && elapsed<10 && shark && shark.activeSelf;
        public Vector3 Threat {get;private set;}
        public OceanVisitor(GameServices services,Transform parent)
        {
            this.services=services;this.parent=parent;random=new System.Random(unchecked(System.Environment.TickCount^1873));
            wait=services.Preview && services.Options.Value("--scenario")=="ocean-visitor"?2:55+random.Next(50);
        }
        public void Tick(float dt,Vector3 player,Vector3 forward,float playerSpeed)
        {
            if(elapsed<0)
            {
                wait-=dt;if(wait>0 || player.y>-9)return;
                elapsed=0;Visits++;ahead=forward; ahead.y=0;ahead.Normalize();side=Vector3.Cross(Vector3.up,ahead)*(random.Next(2)==0?-1:1);
                entry=player+ahead*105+side*70;entry.y=Mathf.Clamp(player.y-3,-48,-15);Threat=entry;
                if(!shark)shark=services.Content.Spawn("reef-shark",0,parent,entry,4.5f);
                shark.SetActive(false);
            }
            elapsed+=dt;
            if(elapsed<3){Threat=player+ahead*45+side*30;return;}
            ahead=Vector3.ProjectOnPlane(forward,Vector3.up).normalized;
            if(!shark.activeSelf)
            {
                shark.transform.position=player+ahead*60+side*85+Vector3.down*7;
                velocity=ahead*Mathf.Min(playerSpeed,38)-side*20;
                shark.transform.rotation=Quaternion.LookRotation(velocity);shark.SetActive(true);
            }
            // Match cruising travel during a crossing; boosting lets the child leave it behind.
            Vector3 goal=elapsed<10?player+ahead*35-side*24:player+ahead*130-side*115;
            goal.y=Mathf.Clamp(player.y-7,-58,-18);
            var desired=ahead*Mathf.Min(playerSpeed,38)+Vector3.ClampMagnitude((goal-shark.transform.position)*1.5f,elapsed<10?30:55);
            velocity=Vector3.MoveTowards(velocity,desired,dt*35);
            shark.transform.position+=velocity*dt;
            if(velocity.sqrMagnitude>.1f)shark.transform.rotation=Quaternion.Slerp(shark.transform.rotation,Quaternion.LookRotation(velocity),1-Mathf.Exp(-dt*5));
            Threat=shark.transform.position;
            if(elapsed>16){shark.SetActive(false);elapsed=-1;wait=110+random.Next(110);}
        }
    }
}
