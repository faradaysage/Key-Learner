namespace KeyLearner.Studio;
public sealed partial class StudioGame
{
    private void ResetVisibleSession()
    {
        guard?.ResetInput();held.Clear();previous=default; // Do not sample keyboard state while the window is inactive.
        previousMouse=Microsoft.Xna.Framework.Input.Mouse.GetState();
        parentHold.Reset();escapeExit.Reset();optionsTaps.Reset();gameShortcut.Reset();analyzer.Reset();recognizer.Reset();counting.Reset();guided.Start(target);flight.ResetInputGestures();
        ResetDotSession();voice?.Stop();canvas.Clear();keyGlow.Clear();playStarted=false;awaitingBalloons=false;
        hero="";celebrateUntil=hintUntil=flightCelebration=0;wordImage?.Dispose();wordImage=null;
        if(calibration>=0){calibration=-1;parent=true;notice="Calibration stopped when the window changed. Start a new sample to continue.";}
        ResetElapsedTime();resumeClears++;
        lifecycleVerified=voice?.Pending==0 && canvas.ParticleCount==0 && canvas.GlyphCount==0 && held.Count==0;
    }
}
