using System;
using System.Collections.Generic;
using System.Linq;
using KeyLearner.Studio;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
namespace KeyLearner.Unity
{
    /// <summary>Selectable chase/eye-level presentation over the engine-independent ground/letter course.</summary>
    public sealed class DinosaurGame : Minigame
    {
        DinosaurModel model;
        PrehistoricLandmarks landmarks;
        PterosaurLife pterosaurs;
        readonly Dictionary<Vector2Int,GameObject> tiles=new Dictionary<Vector2Int,GameObject>();
        readonly List<CreatureLocomotion> walkers=new List<CreatureLocomotion>();
        readonly System.Random words=new System.Random(819);
        Material groundMaterial,waterMaterial,skyMaterial,ringMaterial,shockMaterial;
        GameObject gate,body,shock;
        TextMesh letter;
        AudioSource ambience;
        AmbientLifeAudio lifeAudio;
        Vector2Int center=new Vector2Int(int.MinValue,int.MinValue);
        float footImpulse,roarRemaining,nextReport,lookYaw,lookPitch;
        int steps;
        bool validateWord,cameraKeyHeld,cameraReady;
        // Session-local view choice keeps existing saved data compatible.
        int cameraView;
        Renderer[] bodyRenderers;
        DinosaurActor actor;
        Vector3 cameraPosition;
        float viewAge,motionAge;
        string lastMotion;
        readonly HashSet<int> capturedViews=new HashSet<int>();
        readonly HashSet<string> capturedMotions=new HashSet<string>();
        static readonly string[] CameraNames={"Close","Far","First person"};
        CameraOverrideOption priorOpaque;
        static Vector3 V(System.Numerics.Vector3 p)=>new Vector3(p.X,p.Y,p.Z);
        public override void Enter(GameServices services)
        {
            base.Enter(services);
            if(S.Session.TryGetValue("dinosaur",out var saved))model=(DinosaurModel)saved;
            else {model=new DinosaurModel();S.Session["dinosaur"]=model;PickWord();}
            if(S.Preview && float.TryParse(S.Options.Value("--preview-distance"),System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out float distance) && distance>0)
            { distance=Mathf.Min(distance,6500);model.Position=new System.Numerics.Vector3(DinosaurWorld.Path(distance),DinosaurWorld.Ground(DinosaurWorld.Path(distance),distance),distance);model.SetWord("dino"); }
            if(S.Session.TryGetValue("dinosaur-camera",out var view))cameraView=(int)view;
            steps=model.Footfalls;
            S.Camera.orthographic=false;S.Camera.fieldOfView=72;S.Camera.nearClipPlane=.2f;S.Camera.farClipPlane=1500;
            S.Camera.clearFlags=CameraClearFlags.Skybox;
            skyMaterial=new Material(Shader.Find("KeyLearner/Sky"));RenderSettings.skybox=skyMaterial;
            RenderSettings.fog=true;RenderSettings.fogMode=FogMode.Exponential;RenderSettings.fogColor=new Color(.57f,.7f,.64f);RenderSettings.fogDensity=.0025f;
            S.ConfigureLighting(false);
            groundMaterial=new Material(Shader.Find("KeyLearner/Terrain"));S.Content.ConfigureGround(groundMaterial);waterMaterial=new Material(Shader.Find("KeyLearner/Water"));waterMaterial.SetColor("_BaseColor",new Color(.08f,.37f,.32f,.85f));
            var cameraData=S.Camera.GetComponent<UniversalAdditionalCameraData>();priorOpaque=cameraData.requiresColorOption;cameraData.requiresColorOption=CameraOverrideOption.On;
            shock=GameObject.CreatePrimitive(PrimitiveType.Quad);UnityEngine.Object.Destroy(shock.GetComponent<Collider>());shock.name="Roar pressure wave";shock.transform.SetParent(Root.transform,false);
            shockMaterial=new Material(Shader.Find("KeyLearner/RoarShockwave"));shock.GetComponent<Renderer>().sharedMaterial=shockMaterial;shock.SetActive(false);
            var dinosaurs=S.Content.Category("dinosaur");int rex=Array.FindIndex(dinosaurs,a=>a.Id.IndexOf("Trex",StringComparison.OrdinalIgnoreCase)>=0);
            if(rex<0)throw new InvalidOperationException("Licensed T-Rex source missing from dinosaur catalog.");
            body=S.Content.Spawn("dinosaur",rex,Root.transform,V(model.Position),9);
            bodyRenderers=body.GetComponentsInChildren<Renderer>();
            actor=new DinosaurActor(body);
            ApplyCameraView();
            gate=new GameObject("Prehistoric letter trail");gate.transform.SetParent(Root.transform,false);
            var ring=gate.AddComponent<LineRenderer>();ring.useWorldSpace=false;ring.loop=true;ring.positionCount=64;ring.widthMultiplier=.16f;
            ringMaterial=new Material(Shader.Find("Universal Render Pipeline/Unlit"));ringMaterial.color=Style.Dots;ring.sharedMaterial=ringMaterial;
            for(int i=0;i<64;i++){float a=i*Mathf.PI*2/64;ring.SetPosition(i,new Vector3(Mathf.Cos(a)*3.7f,Mathf.Sin(a)*3.7f,0));}
            letter=Visuals.Text(gate.transform,"D",Vector3.zero,7,Style.Dots);
            var sound=new GameObject("Prehistoric forest ambience");sound.transform.SetParent(Root.transform,false);ambience=sound.AddComponent<AudioSource>();ambience.loop=true;ambience.playOnAwake=false;ambience.clip=S.Audio.SoundClip("ambient-wind");ambience.volume=0;
            ExplorerCloudLayer.Create(Root.transform,S.Camera.transform,S.Settings.GentleMotion,190);
            landmarks=new PrehistoricLandmarks(S,Root.transform);
            pterosaurs=new PterosaurLife(S,Root.transform);
            lifeAudio=new AmbientLifeAudio(S,Root.transform);
            SunShaftField.Create(S,Root.transform,false);
            UpdateTiles();Tick(0);
        }
        void PickWord(){var list=S.Store.Words.Where(w=>w.Enabled&&w.Adventure).ToArray();model.SetWord(list.Length==0?"dino":list[words.Next(list.Length)].Word);}
        public override void Key(KeyEvent e)
        {
            if(e.Key==112)
            {
                if(e.Down && !cameraKeyHeld){cameraView=(cameraView+1)%CameraNames.Length;S.Session["dinosaur-camera"]=cameraView;ApplyCameraView();}
                cameraKeyHeld=e.Down;return;
            }
            if(!e.Down || (e.Key!=162 && e.Key!=163) || !model.Roar())return;
            foreach(var creature in walkers)creature.Startle();
            pterosaurs.Startle(V(model.Position));
            roarRemaining=1.15f;S.Audio.Play("dinosaur-roar",S.Settings,.48f,3);
            S.Feedback.Pulse(.65f,.6f);
            S.Rewards.Burst(V(model.Position)+V(model.Forward)*9+Vector3.up*2,new Color(.69f,.6f,.34f,.35f),S.Settings.GentleMotion?8:24,7);
        }
        public override void Tick(float dt)
        {
            if(validateWord){validateWord=false;if(!S.Store.Words.Any(w=>w.Enabled&&w.Adventure&&w.Word==model.Course.Word))PickWord();}
            int before=model.Course.Collected,completed=model.Course.Completed;
            float turn=(S.Keys.IsDown(39)?1:0)-(S.Keys.IsDown(37)?1:0),drive=(S.Keys.IsDown(38)?1:0)-(S.Keys.IsDown(40)?1:0);
            model.Step(dt,turn,drive,S.Keys.IsDown(32),S.Settings.FlightAssist,(float)S.Settings.FlightResponse);
            if(model.Course.Collected!=before){S.Audio.Play("pop",S.Settings,.45f);S.Audio.Say(model.Course.Word[before].ToString(),S.Settings,key:true);}
            if(model.Course.Completed!=completed)
            {
                var word=S.Store.Words.FirstOrDefault(w=>w.Word==model.Course.Word);
                S.Audio.Say(word!=null&&word.Spoken.Length>0?word.Spoken:model.Course.Word,S.Settings,word?.Recording??"");
                S.Audio.Play("powerup",S.Settings,.6f);S.Rewards.Burst(V(model.Position)+V(model.Forward)*20+Vector3.up*7,Style.Mint,55,8);
            }
            if(model.Course.Collected>=model.Course.Word.Length && model.Course.RewardRemaining==0)PickWord();
            var p=V(model.Position);var forward=V(model.Forward);
            if(model.Footfalls>steps)
            {
                steps=model.Footfalls;footImpulse=1;S.Audio.Play(steps%2==0?"dinosaur-step-l":"dinosaur-step-r",S.Settings,.5f,.15);
                S.Feedback.Pulse(Mathf.Lerp(.055f,.12f,model.Speed/23),.16f);
                if(!S.Settings.GentleMotion)S.Rewards.Burst(p-forward*2, new Color(.5f,.42f,.26f,.2f),6,1.5f);
            }
            footImpulse*=Mathf.Exp(-dt*14);roarRemaining=Mathf.Max(0,roarRemaining-dt);
            float lookX=S.Settings.MousePlay?(Input.mousePosition.x/Mathf.Max(1,Screen.width)-.5f)*.5f:0;
            float lookY=S.Settings.MousePlay?(Input.mousePosition.y/Mathf.Max(1,Screen.height)-.5f)*.38f:0;
            lookYaw=Mathf.Lerp(lookYaw,lookX,1-Mathf.Exp(-dt*4));lookPitch=Mathf.Lerp(lookPitch,lookY,1-Mathf.Exp(-dt*4));
            float impulseScale=S.Settings.GentleMotion?.08f:.24f;
            var lookRotation=Quaternion.Euler(-lookPitch*Mathf.Rad2Deg,model.Yaw*Mathf.Rad2Deg+lookYaw*Mathf.Rad2Deg,0);
            if(cameraView==2)
            {
                cameraPosition=p+Vector3.up*(7-footImpulse*impulseScale)+forward*2;
                S.Camera.transform.SetPositionAndRotation(cameraPosition,lookRotation);
            }
            else
            {
                float distance=cameraView==0?25:43,height=cameraView==0?13:22;
                var desired=p-(lookRotation*Vector3.forward)*distance+Vector3.up*height;
                // The terrain is sampled directly: scenery has no gameplay colliders.
                desired.y=Mathf.Max(desired.y,DinosaurWorld.Ground(desired.x,desired.z)+4);
                cameraPosition=cameraReady?Vector3.Lerp(cameraPosition,desired,1-Mathf.Exp(-dt*7)):desired;
                cameraPosition.y=Mathf.Max(cameraPosition.y,DinosaurWorld.Ground(cameraPosition.x,cameraPosition.z)+4);
                var aim=p+forward*10+Vector3.up*6;
                S.Camera.transform.SetPositionAndRotation(cameraPosition-Vector3.up*footImpulse*impulseScale,Quaternion.LookRotation(aim-cameraPosition));
            }
            cameraReady=true;viewAge+=dt;
            S.Camera.fieldOfView=72+(S.Settings.GentleMotion?.5f:3.5f)*Mathf.Sin(Mathf.Clamp01(roarRemaining/1.15f)*Mathf.PI);
            body.transform.SetPositionAndRotation(p+Vector3.up*4.5f,Quaternion.LookRotation(forward));
            actor.Tick(model.Stride,model.Speed,roarRemaining>0?1-roarRemaining/1.15f:-1);
            motionAge=lastMotion==actor.Motion?motionAge+dt:0;lastMotion=actor.Motion;
            if(S.Preview && motionAge>.18f && !capturedMotions.Contains(actor.Motion) && S.Options.Value("--motion-screenshots").Length>0 && (actor.Motion!="Roar" || roarRemaining<.8f))
            {
                var folder=System.IO.Path.GetFullPath(S.Options.Value("--motion-screenshots"));System.IO.Directory.CreateDirectory(folder);
                ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(folder,"motion-"+actor.Motion+".png"));capturedMotions.Add(actor.Motion);
            }
            gate.SetActive(model.Course.Collected<model.Course.Word.Length);
            gate.transform.position=V(model.Gate);gate.transform.rotation=Quaternion.LookRotation((V(model.Gate)-S.Camera.transform.position).normalized);
            letter.text=model.Course.Word[Mathf.Min(model.Course.Collected,model.Course.Word.Length-1)].ToString().ToUpperInvariant();
            float gain=S.Settings.Sound&&S.Settings.EffectsSound?S.Settings.Volume/100f*S.Settings.EffectsVolume/100f*.09f*(S.Audio.Pending>0?.3f:1):0;
            ambience.volume=Mathf.MoveTowards(ambience.volume,gain,dt*.4f);if(gain>0&&!ambience.isPlaying)ambience.Play();if(gain==0)ambience.Stop();
            shock.SetActive(roarRemaining>0);
            if(shock.activeSelf)
            {
                float progress=1-roarRemaining/1.15f;shockMaterial.SetFloat("_Progress",progress);shockMaterial.SetFloat("_Strength",S.Settings.GentleMotion?.1f:1);
                shock.transform.SetPositionAndRotation(S.Camera.transform.position+S.Camera.transform.forward, S.Camera.transform.rotation);
                float height=2*Mathf.Tan(S.Camera.fieldOfView*Mathf.Deg2Rad*.5f);shock.transform.localScale=new Vector3(height*S.Camera.aspect,height,1);
            }
            if(S.Preview && viewAge>.6f && !capturedViews.Contains(cameraView) && S.Options.Value("--camera-screenshots").Length>0)
            {
                var folder=System.IO.Path.GetFullPath(S.Options.Value("--camera-screenshots"));System.IO.Directory.CreateDirectory(folder);
                ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(folder,"camera-"+cameraView+".png"));capturedViews.Add(cameraView);
            }
            UpdateTiles();
            landmarks.Tick(p);
            pterosaurs.Tick(dt,p);
            lifeAudio.Tick(dt,p,DinosaurWorld.Biome(p.z)!=1);
            for(int i=walkers.Count-1;i>=0;i--)
            {
                var w=walkers[i];if(!w.Transform){walkers.RemoveAt(i);continue;}
                bool nearby=(w.Transform.position-p).sqrMagnitude<310*310;
                if(w.Transform.gameObject.activeSelf!=nearby)w.Transform.gameObject.SetActive(nearby);
                if(nearby)w.Tick(dt,p,q=>DinosaurWorld.Ground(q.x,q.z),q=>DinosaurWorld.Ground(q.x,q.z)>3);

            }
            if(S.Preview && !string.IsNullOrEmpty(S.Options.Value("--interaction-report")) && S.Now>=nextReport)
            {
                nextReport=(float)S.Now+.08f;
                var path=S.Options.Value("--interaction-report");var data=System.Text.Json.JsonSerializer.Serialize(new{time=S.Now,mode=12,speed=model.Speed,heroMotion=actor.Motion,heroGait=actor.Gait,heading=model.Yaw,footfalls=model.Footfalls,roars=model.Roars,roarActive=roarRemaining>0,letters=model.Course.Collected,words=model.Course.Completed,score=model.Course.Score,x=p.x,y=p.y,z=p.z,firstPerson=cameraView==2,cameraView=CameraNames[cameraView],bodyVisible=cameraView!=2,cameraHeight=S.Camera.transform.position.y-DinosaurWorld.Ground(S.Camera.transform.position.x,S.Camera.transform.position.z),dinosaurs=walkers.Count,pterosaurs=pterosaurs.Count,pterosaursVisible=pterosaurs.Visible});
                System.IO.File.WriteAllText(path+".tmp",data);if(System.IO.File.Exists(path))System.IO.File.Replace(path+".tmp",path,null);else System.IO.File.Move(path+".tmp",path);
            }
        }
        void ApplyCameraView()
        {
            cameraReady=false;viewAge=0;
            foreach(var renderer in bodyRenderers)renderer.shadowCastingMode=cameraView==2?ShadowCastingMode.ShadowsOnly:ShadowCastingMode.On;
        }
        void UpdateTiles()
        {
            var current=new Vector2Int(Mathf.FloorToInt(model.Position.X/128),Mathf.FloorToInt(model.Position.Z/128));if(current==center)return;center=current;
            foreach(var key in tiles.Keys.Where(k=>Mathf.Abs(k.x-center.x)>2||Mathf.Abs(k.y-center.y)>2).ToArray()){UnityEngine.Object.Destroy(tiles[key]);tiles.Remove(key);}
            for(int x=center.x-2;x<=center.x+2;x++)for(int z=center.y-2;z<=center.y+2;z++){var key=new Vector2Int(x,z);if(!tiles.ContainsKey(key))tiles[key]=Tile(key);}
        }
        GameObject Tile(Vector2Int key)
        {
            var go=new GameObject("Prehistoric clearing "+key);go.transform.SetParent(Root.transform,false);
            var rng=new System.Random(unchecked(key.x*7193+key.y*3137+911));int biome=DinosaurWorld.Biome(key.y*128+64);
            var vertices=new List<Vector3>();var colors=new List<Color>();var triangles=new List<int>();
            for(int z=0;z<=32;z++)for(int x=0;x<=32;x++)
            {
                float px=key.x*128+x*4,pz=key.y*128+z*4,h=DinosaurWorld.Ground(px,pz),side=Mathf.Abs(px-DinosaurWorld.Path(pz));
                vertices.Add(new Vector3(px,h,pz));var grass=biome==1?new Color(.42f,.39f,.2f):new Color(.19f,.34f,.18f);var sand=biome==1?new Color(.59f,.36f,.19f):new Color(.47f,.43f,.28f);
                colors.Add(Color.Lerp(sand,grass,Mathf.SmoothStep(0,1,Mathf.InverseLerp(6,17,side))));
                if(x<32&&z<32){int a=z*33+x;triangles.AddRange(new[]{a,a+33,a+1,a+1,a+33,a+34});}
            }
            MeshSurface("Ancient earth",go.transform,vertices,triangles,colors,groundMaterial);
            MeshSurface("Lagoon",go.transform,new List<Vector3>{new Vector3(key.x*128,2,key.y*128),new Vector3((key.x+1)*128,2,key.y*128),new Vector3(key.x*128,2,(key.y+1)*128),new Vector3((key.x+1)*128,2,(key.y+1)*128)},new List<int>{0,2,1,1,2,3},null,waterMaterial);
            for(int i=0;i<42;i++)
            {
                float x=key.x*128+rng.Next(128),z=key.y*128+rng.Next(128),h=DinosaurWorld.Ground(x,z);if(h<3||Mathf.Abs(x-DinosaurWorld.Path(z))<14)continue;
                string category=i<18?(biome!=1 && i%4!=0?"tropical-tree":"tree"):i<25?"rock":"fern";float size=(category=="tree"||category=="tropical-tree")?17+rng.Next(19):category=="rock"?5+rng.Next(biome==1?16:7):2+rng.Next(4);
                int variant=rng.Next(1000);
                if(category=="tree")
                {
                    var trees=S.Content.Category("tree");var pines=Array.FindAll(trees,t=>t.Id.Contains("Pine"));
                    if(pines.Length>0)variant=Array.IndexOf(trees,pines[variant%pines.Length]);
                }
                S.Content.Spawn(category,variant,go.transform,new Vector3(x,h-.15f,z),size,rng.Next(360));
            }
            var ferns=S.Content.Category("fern");
            if(ferns.Length==0)throw new InvalidOperationException("Authored fern catalog missing.");
            for(int i=0;i<28;i++)
            {
                float z=key.y*128+rng.Next(128),x=key.x*128+rng.Next(128),h=DinosaurWorld.Ground(x,z);if(h<3||Mathf.Abs(x-DinosaurWorld.Path(z))<10)continue;
                S.Content.Spawn("fern",i,go.transform,new Vector3(x,h-.08f,z),1.5f+(float)rng.NextDouble()*2.5f,rng.Next(360));
            }
            if((key.x+key.y)%2==0)
            {
                float z=key.y*128+60,x=key.x*128+70,h=DinosaurWorld.Ground(x,z);if(h>3 && Mathf.Abs(x-DinosaurWorld.Path(z))>25)
                {
                    int index=Math.Abs(unchecked(key.x*17+key.y*7))%S.Content.Category("dinosaur").Length;float height=6+(index%3)*2;
                    var d=S.Content.Spawn("dinosaur",index,go.transform,new Vector3(x,h+height*.5f,z),height,rng.Next(360));
                    if(d){var animation=d.GetComponentInChildren<Animation>();if(animation&&animation.clip){animation[animation.clip.name].time=(float)rng.NextDouble()*animation.clip.length;animation[animation.clip.name].speed=.65f;}
                        walkers.Add(new CreatureLocomotion(d,new Vector3(x,h,z),height,rng.Next(),2.2f,23));}
                }
            }
            return go;
        }
        static void MeshSurface(string name,Transform parent,List<Vector3> vertices,List<int> triangles,List<Color> colors,Material material)
        {
            var go=new GameObject(name);go.transform.SetParent(parent,false);var mesh=new Mesh{name=name};mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);if(colors!=null)mesh.SetColors(colors);mesh.RecalculateNormals();mesh.RecalculateBounds();go.AddComponent<TransientMesh>().Mesh=mesh;go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterial=material;
        }
        public override void DrawUI()
        {
            Ui.Panel(new Rect(40,30,590,125),new Color(.035f,.075f,.06f,.9f));Ui.Label(new Rect(60,40,550,32),"DINOSAUR SPELLER",20,Style.Mint,TextAnchor.MiddleLeft);
            float size=Mathf.Min(52,530f/model.Course.Word.Length);
            for(int i=0;i<model.Course.Word.Length;i++){var r=new Rect(60+i*size,82,size-5,55);Ui.Panel(r,i<model.Course.Collected?Style.Mint:i==model.Course.Collected?Style.Dots:Style.Panel);Ui.Label(r,model.Course.Word[i].ToString().ToUpperInvariant(),29,i<=model.Course.Collected?Style.Navy:Color.white);}
            Ui.Panel(new Rect(1160,30,240,95),new Color(.035f,.075f,.06f,.9f));Ui.Label(new Rect(1170,40,220,36),model.Course.Score+" points",26,Color.white);Ui.Label(new Rect(1170,82,220,25),model.Course.Completed+" words discovered",15,Style.Mint);
            Ui.Panel(new Rect(30,785,1380,88),new Color(.025f,.07f,.05f,.8f));Ui.Label(new Rect(48,788,1000,35),new[]{"THE FERN FOREST","RED ROCK VALLEY","THE ANCIENT LAGOONS"}[DinosaurWorld.Biome(model.Position.Z)],21,Style.Mint,TextAnchor.MiddleLeft);
            Ui.Label(new Rect(48,825,1300,35),"← → Turn    ↑ Faster    ↓ Stop    SPACE Run    CTRL Roar    F1 View: "+CameraNames[cameraView]+"    G G Games",18,Color.white,TextAnchor.MiddleLeft);
            if(model.Course.RewardRemaining>0)Ui.Label(new Rect(280,300,880,130),model.Course.Word.ToUpperInvariant()+"!",80,Style.Dots);
        }
        public override void Suspend(){ambience?.Stop();lifeAudio?.Stop();roarRemaining=footImpulse=0;cameraKeyHeld=false;shock?.SetActive(false);model.Course.DismissReward();validateWord=true;}
        public override void Exit(){S.Camera.GetComponent<UniversalAdditionalCameraData>().requiresColorOption=priorOpaque;base.Exit();foreach(var m in new[]{groundMaterial,waterMaterial,skyMaterial,ringMaterial,shockMaterial})if(m)UnityEngine.Object.Destroy(m);}
        public override string DiagnosticState=>"dinosaur camera="+CameraNames[cameraView]+" letters="+model.Course.Collected+" words="+model.Course.Completed+" score="+model.Course.Score+" footfalls="+model.Footfalls+" roars="+model.Roars+" tiles="+tiles.Count+" actors="+walkers.Count;
    }
}
