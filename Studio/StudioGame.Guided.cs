using Microsoft.Xna.Framework;
namespace KeyLearner.Studio;
public sealed partial class StudioGame
{
    void DrawGuidedLetters()
    {
        float spacing=Math.Min(115,1100f/Math.Max(1,target.Length));float start=W/2f-target.Length*spacing/2;
        float shake=now<guided.FeedbackUntil&&!S.GentleMotion?MathF.Sin((float)now*65)*7:0;
        for(int i=0;i<target.Length;i++){
            float x=start+i*spacing+shake;bool current=i==guided.Progress;var color=i<guided.Progress?(S.Theme==Mood.BlackAndWhite?Color.White:new Color(99,228,149)):current?canvas.Palette[1]:new Color(74,88,114);
            Fill(new((int)x,300,(int)spacing-10,112),current?new Color(60,72,96):new Color(23,32,52));
            string c=target[i].ToString().ToUpperInvariant();float scale=Math.Min(1.2f,(spacing-22)/Math.Max(1,title.MeasureString(c).X));var size=title.MeasureString(c)*scale;
            Text(c,x+(spacing-10-size.X)/2,300+(112-size.Y)/2,color,scale,title);
            if(current){Fill(new((int)x,420,(int)((spacing-10)*guided.Remaining(now)),5),color);}
        }
        string hint=now<guided.FeedbackUntil?"Time for that letter ran out. Let's try it again.":double.IsPositiveInfinity(guided.LetterSeconds)?"Find the glowing letter. Take all the time you need.":"Find the glowing letter. "+guided.LetterSeconds.ToString("0")+" seconds per letter.";
        Center(hint,475,Color.White*.8f,.55f);
        Center("Words discovered  "+guided.Completed,527,canvas.Palette[2],.46f);
    }
}
