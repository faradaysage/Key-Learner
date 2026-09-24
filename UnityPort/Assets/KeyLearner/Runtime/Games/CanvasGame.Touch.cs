using System;
using System.Linq;
using System.Collections.Generic;
using KeyLearner.Studio;
using UnityEngine;
using PlayMode = KeyLearner.Studio.PlayMode;

namespace KeyLearner.Unity
{
    public sealed partial class CanvasGame
    {
        double nextLearningTap;
        bool GuidedActivity => mode == PlayMode.WordAdventure || (S.TouchPlay && mode == PlayMode.SmashGarden);
        static readonly Rect SmashTarget = new Rect(330, 455, 780, 240);
        static readonly Rect CountTarget = new Rect(480, 490, 480, 225);
        Rect LetterChoice(int i) => new Rect(200 + i*265, 550, 245, 175);
        char[] LetterChoices()
        {
            char letter = guided.Progress < guided.Target.Length ? guided.Target[guided.Progress] : 'a';
            var choices = new[] {letter, (char)('a'+(letter-'a'+7)%26), (char)('a'+(letter-'a'+13)%26), (char)('a'+(letter-'a'+19)%26)};
            int position = (guided.Progress+wordIndex)%4;
            (choices[0], choices[position]) = (choices[position], choices[0]);
            return choices;
        }
        void SubmitGuidedLetter(char letter, InputContext context)
        {
            if (guided.Update(S.Now)) S.Audio.Play("retry", S.Settings, .45f);
            Emit(char.ToUpperInvariant(letter), letter, context);
            // Every accepted touch speaks its letter, including the final one.
            // Android serializes narration so the completed word follows it.
            if (S.TouchPlay && S.Settings.SpeakLetters) S.Audio.Say(letter.ToString(), S.Settings, key:true);
            if (guided.Add(letter, S.Now))
            {
                if (S.Settings.AdaptiveLearning)
                    S.Store.Profile.WordCounts[target.Word] = Math.Min(100000, S.Store.Profile.WordCounts.GetValueOrDefault(target.Word)+1);
                Announce(target);
                reward.Start(target.Word.Length+target.Word.Count(c=>"jqxz".Contains(c)));
                for (int i=0;i<reward.Remaining;i++) MakeBalloon(i,reward.Remaining);
            }
            else if (!S.TouchPlay && S.Settings.SpeakLetters) S.Audio.Say(letter.ToString(), S.Settings, key:true);
        }
        void TouchLearning(Vector2 point)
        {
            if (Input.touchCount > 1 || S.Now < nextLearningTap || S.Audio.Pending > 0) return;
            if (reward.Remaining > 0)
            {
                var balloon = glyphs.Where(g=>g.Reward && Vector2.Distance(g.P,point)<Mathf.Max(70,g.Radius*1.5f)).OrderBy(g=>Vector2.Distance(g.P,point)).FirstOrDefault();
                if (balloon != null) { nextLearningTap=S.Now+.22; Pop(balloon); }
                return;
            }
            if (mode == PlayMode.Counting)
            {
                if (!CountTarget.Contains(point) || S.Now < celebrationUntil || !double.IsPositiveInfinity(hundredAt)) return;
                nextLearningTap=S.Now+.6;
                Count(counting.CountNext());
                S.Burst(point,Style.Dots,12);
                return;
            }
            if (guided.Progress >= guided.Target.Length) return;
            char selected;
            if (mode == PlayMode.SmashGarden)
            {
                if (!SmashTarget.Contains(point)) return;
                selected=guided.Target[guided.Progress];
            }
            else
            {
                int choice=Enumerable.Range(0,4).Where(i=>LetterChoice(i).Contains(point)).DefaultIfEmpty(-1).First();
                if(choice<0)return;
                selected=LetterChoices()[choice];
                if(selected!=guided.Target[guided.Progress])
                { nextLearningTap=S.Now+.5;S.Audio.Play("retry",S.Settings,.4f);S.Burst(point,Style.Blue,6);return; }
            }
            nextLearningTap=S.Now+.45;
            typed=true;
            current?.Balloon.Release();current=null;
            SubmitGuidedLetter(selected,new InputContext(Gesture.Deliberate,1,.5,.45,0,0,1,0,Array.Empty<double>()));
            current?.Balloon.Release();current=null;
        }
        void DrawTouchLearning()
        {
            if (reward.Remaining>0 || (mode==PlayMode.Counting && S.Now<celebrationUntil)) return;
            bool available=S.Audio.Pending==0 && S.Now>=nextLearningTap && (mode!=PlayMode.Counting || (S.Now>=celebrationUntil && double.IsPositiveInfinity(hundredAt)));
            if (mode==PlayMode.Counting)
            {
                Ui.Panel(CountTarget,Style.Panel);
                Ui.Label(CountTarget,"Tap to count a star",42,available?Style.Dots:Color.gray);
            }
            else if (mode==PlayMode.SmashGarden)
            {
                Ui.Panel(SmashTarget,Style.Panel);
                Ui.Label(SmashTarget,"Tap to grow the next letter",40,available?Style.Mint:Color.gray);
            }
            else
            {
                var choices=LetterChoices();
                for(int i=0;i<choices.Length;i++)
                { Ui.Panel(LetterChoice(i),Style.Panel);Ui.Label(LetterChoice(i),choices[i].ToString().ToUpperInvariant(),76,available?Color.white:Color.gray); }
            }
        }
    }
}
