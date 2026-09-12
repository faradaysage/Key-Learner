namespace KeyLearner.Studio;
public sealed partial class StudioGame
{
    private void HandleInactiveParents()
    {
        if(guard==null)return;
        bool open=false;
        while(guard.TryRead(out var e)){
            if(e.Key==-1){parentHold.Reset();escapeExit.Reset();optionsTaps.Reset();continue;}
            if(e.Time<=0 || now-e.Time>.25)continue;
            if(escapeExit.Feed(e)){allowExit=true;Exit();return;}
            if(optionsTaps.Feed(e))open=true;
            if(e.Down)held.Add(e.Key);else held.Remove(e.Key);
            parentHold.Observe(KeySnapshot.From(held));
        }
        held.Clear();held.UnionWith(guard.Snapshot().Keys);
        var action=parentHold.Update(KeySnapshot.From(held),now);
        if(action==ParentAction.Exit){allowExit=true;Exit();return;}
        if(open || action==ParentAction.Options){
            if(!parent)ToggleParent();guard.ResetInput();held.Clear();parentHold.Reset();
            SDL_RestoreWindow(Window.Handle);SDL_RaiseWindow(Window.Handle);
        }
    }
    private void ResetVisibleSession()
    {
        guard?.ResetInput();held.Clear();previous=Microsoft.Xna.Framework.Input.Keyboard.GetState();
        previousMouse=Microsoft.Xna.Framework.Input.Mouse.GetState();
        parentHold.Reset();escapeExit.Reset();optionsTaps.Reset();analyzer.Reset();recognizer.Reset();counting.Reset();guided.Start(target);flight.ResetInputGestures();
        voice?.Stop();canvas.Clear();keyGlow.Clear();playStarted=false;awaitingBalloons=false;
        hero="";celebrateUntil=hintUntil=flightCelebration=0;wordImage?.Dispose();wordImage=null;
        if(calibration>=0){calibration=-1;parent=true;notice="Calibration stopped when the window changed. Start a new sample to continue.";}
        ResetElapsedTime();resumeClears++;
        lifecycleVerified=voice?.Pending==0 && canvas.ParticleCount==0 && canvas.GlyphCount==0 && held.Count==0;
    }
}
