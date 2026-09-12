namespace KeyLearner.Studio;
/// <summary>A focus loss revokes capture until the visible game loop explicitly re-arms it.</summary>
public sealed class InputFocus(nint window,Action clear)
{
    public bool Active {get;private set;}
    public int Losses {get;private set;}
    public void SetActive(bool active){if(Active==active)return;Active=active;clear();if(!active)Losses++;}
    public bool Accepts(nint foreground,bool desktopReceivesInput=true){
        if(!desktopReceivesInput || window==0 || foreground!=window){SetActive(false);return false;}
        return Active;
    }
}
