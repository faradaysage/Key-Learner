using System;
using System.Linq;
using UnityEngine;

namespace KeyLearner.Unity
{
    public sealed partial class ParentStudio
    {
        string[] touchCreditPages;
        int touchCreditPage;
        void DrawTouchCredits()
        {
            if (touchCreditPages == null)
                touchCreditPages = BitmapText.Pages(RuntimeFonts.Load("Fredoka-Play"), creditsText, 24, 1250, 565);
            Ui.Panel(new Rect(0,0,1440,900),Style.Navy,0);
            Ui.Label(new Rect(70,30,1300,80),"Art and licenses",40,Color.white,TextAnchor.MiddleLeft);
            Ui.Label(new Rect(70,115,1300,45),"Page "+(touchCreditPage+1)+" of "+touchCreditPages.Length,24,Style.Mint,TextAnchor.MiddleLeft);
            Ui.Label(new Rect(80,180,1250,565),touchCreditPages[touchCreditPage],24,Color.white,TextAnchor.UpperLeft);
            Ui.Button(new Rect(70,785,310,90),"Previous",()=>touchCreditPage--,touchCreditPage>0);
            Ui.Button(new Rect(410,785,310,90),"Next",()=>touchCreditPage++,touchCreditPage+1<touchCreditPages.Length);
            Ui.Button(new Rect(850,785,520,90),"Parent Options",()=>showCredits=false);
        }
        int touchParentPage;
        bool confirmProgressReset;
        void TouchToggle(Rect rect, string label, bool value, Action<bool> set)
        {
            Ui.Button(rect,label+": "+(value?"On":"Off"),()=>{set(!value);S.Normalize();services.ApplyGraphics();});
        }
        void DrawTouchParent()
        {
            Ui.Panel(new Rect(0,0,1440,900),Style.Navy,0);
            Ui.Label(new Rect(70,35,1100,85),"Parent Options",46,Color.white,TextAnchor.MiddleLeft);
            if(showCredits){DrawCredits();return;}
            if(confirmProgressReset)
            {
                Ui.Label(new Rect(170,190,1100,200),"Reset learned word habits?\nYour settings and word list will be kept.",34,Color.white);
                Ui.Button(new Rect(180,490,510,110),"Keep progress",()=>confirmProgressReset=false);
                Ui.Button(new Rect(750,490,510,110),"Reset progress",()=>{
                    services.Store.ResetWordLearning();
                    services.Store.Save();
                    confirmProgressReset=false;
                });
            }
            else if(touchParentPage==0)
            {
                Ui.Button(new Rect(160,180,1120,105),"Narrator and sound",()=>touchParentPage=1);
                Ui.Button(new Rect(160,315,1120,105),"Learning and play",()=>touchParentPage=2);
                Ui.Button(new Rect(160,450,1120,105),"About KeyLearner",()=>touchParentPage=3);
                Ui.Button(new Rect(160,585,1120,105),"Exit KeyLearner",()=>{services.Store.Save();quit();});
            }
            else if(touchParentPage==1)
            {
                Ui.Label(new Rect(180,150,1080,70),"Narrator: "+services.Store.SpeechPacks.DisplayName(S.VoicePackId),34,Color.white);
                var voices=services.Store.SpeechPacks.Packs.Select(p=>p.Id).ToArray();
                Ui.Button(new Rect(160,240,530,95),"Change narrator",()=>{
                    int next=(Array.IndexOf(voices,services.Store.SpeechPacks.NormalizeChoice(S.VoicePackId))+1)%voices.Length;
                    services.Audio.SelectVoice(S,voices[next]);
                },voices.Length>1);
                Ui.Button(new Rect(750,240,530,95),"Hear this voice",()=>services.Audio.PreviewVoice(S.VoicePackId,S));
                Ui.Label(new Rect(430,370,580,85),"Volume: "+S.Volume+"%",38,Color.white);
                Ui.Button(new Rect(170,365,210,100),"Quieter",()=>S.Volume=Mathf.Max(0,S.Volume-10));
                Ui.Button(new Rect(1060,365,210,100),"Louder",()=>S.Volume=Mathf.Min(100,S.Volume+10));
                TouchToggle(new Rect(160,505,530,100),"Sound",S.Sound,v=>S.Sound=v);
                TouchToggle(new Rect(750,505,530,100),"Effects",S.EffectsSound,v=>S.EffectsSound=v);
                TouchToggle(new Rect(160,640,1120,95),"Say letters",S.SpeakLetters,v=>S.SpeakLetters=v);
            }
            else if(touchParentPage==2)
            {
                TouchToggle(new Rect(160,190,1120,95),"Gentle motion",S.GentleMotion,v=>S.GentleMotion=v);
                TouchToggle(new Rect(160,315,1120,95),"Learn word habits",S.AdaptiveLearning,v=>S.AdaptiveLearning=v);
                TouchToggle(new Rect(160,440,1120,95),"Help with flying and swimming",S.FlightAssist,v=>S.FlightAssist=v);
                Ui.Button(new Rect(160,565,1120,95),"Reset learning progress…",()=>confirmProgressReset=true);
            }
            else
            {
                Ui.Label(new Rect(180,180,1080,90),"KeyLearner "+Application.version,44,Color.white);
                Ui.Label(new Rect(180,290,1080,150),"Blake narration is included.\nPlay offline with no accounts or subscriptions.",32,Color.white);
                Ui.Button(new Rect(250,505,940,110),"Art and licenses",()=>showCredits=true);
            }
            if(touchParentPage!=0 && !confirmProgressReset)
                Ui.Button(new Rect(70,785,510,90),"Parent menu",()=>touchParentPage=0);
            Ui.Button(new Rect(850,785,520,90),"Save and return",SaveClose);
        }
    }
}
