namespace KeyLearner.Studio;

/// <summary>Streaming exact matches keep their prefix alive after speaking. Only typo
/// recovery requires deliberate input; fast normal typing must still recognize words.</summary>
public sealed class WordRecognizer
{
    private readonly Store store;
    private string buffer="",announced="";
    private double last;
    private bool deliberate=true;
    private readonly HashSet<string> passedPrefixes=new();
    private Dictionary<string,WordEntry> words=new(StringComparer.Ordinal);
    private HashSet<string> prefixes=new(StringComparer.Ordinal);
    private readonly Queue<WordEntry> output=new();
    public string Buffer=>buffer;
    public WordRecognizer(Store store) {this.store=store;RefreshDictionary();}
    public void RefreshDictionary()
    {
        words=store.Words.Where(w=>w.Enabled).GroupBy(w=>w.Word).ToDictionary(g=>g.Key,g=>g.First(),StringComparer.Ordinal);
        prefixes=words.Keys.SelectMany(w=>Enumerable.Range(1,w.Length).Select(n=>w[..n])).ToHashSet(StringComparer.Ordinal);
    }
    public void Reset() { ClearEpisode();output.Clear(); }
    private void ClearEpisode() {buffer="";announced="";deliberate=true;last=0;passedPrefixes.Clear();}
    public void Backspace() {if(buffer.Length>0) {buffer=buffer[..^1];announced="";passedPrefixes.RemoveWhere(p=>!buffer.StartsWith(p,StringComparison.Ordinal));}}
    private bool HasExtensions(string prefix)=>words.Keys.Any(w=>w.Length>prefix.Length && w.StartsWith(prefix,StringComparison.Ordinal));
    public double DelayFor(string prefix)
    {
        if(!store.Settings.AdaptiveLearning || !HasExtensions(prefix)) return 0;
        var evidence=store.Profile.PrefixHabits.GetValueOrDefault(prefix);
        return Math.Min(store.Settings.PrefixPause,Math.Max(0,evidence)*Math.Clamp(store.Profile.TypingInterval,.12,.6)*.65);
    }
    private double EpisodeGap=>Math.Max(3,store.Settings.PrefixPause+1);
    private void Announce(WordEntry word)
    {
        if(announced==word.Word)return;
        output.Enqueue(word);announced=word.Word;
        if(store.Settings.AdaptiveLearning)
            store.Profile.WordCounts[word.Word]=Math.Min(100000,store.Profile.WordCounts.GetValueOrDefault(word.Word)+1);
    }
    private void Learn(string terminal)
    {
        if(!store.Settings.AdaptiveLearning)return;
        foreach(var prefix in passedPrefixes)
        {
            if(!terminal.StartsWith(prefix,StringComparison.Ordinal))continue;
            var previous=store.Profile.PrefixHabits.GetValueOrDefault(prefix);
            store.Profile.PrefixHabits[prefix]=Math.Clamp(previous+(terminal.Length>prefix.Length?1:-1),0,8);
        }
    }
    private WordEntry? Pop()=>output.TryDequeue(out var word)?word:null;
    public WordEntry? Add(char c,double now,InputContext context,string? target=null)
    {
        if(!char.IsAsciiLetter(c)) {Close(target);return Pop();}
        // Concurrent key clusters are not spelling. Rate or a learned gesture label alone
        // must never erase an exact word (adult typing is often >6 characters/second).
        if(context.Held>=3) {Reset();return null;}
        if(buffer.Length>0 && now-last>EpisodeGap) Close(target);
        c=char.ToLowerInvariant(c);
        if(buffer.Length>0 && words.ContainsKey(buffer) && !prefixes.Contains(buffer+c))
            Close(target); // "mom"+"d" resolves mom now, and starts dad/daddy.
        if(buffer.Length>0 && now>last && now-last<2 && store.Settings.AdaptiveLearning)
            store.Profile.TypingInterval=Math.Clamp(store.Profile.TypingInterval*.9+(now-last)*.1,.12,1.5);
        buffer+=c;last=now;
        deliberate &= context.Intentional || context.Gesture==Gesture.Rapid && context.Held<=2;
        if(buffer.Length>24) {ClearEpisode();buffer=c.ToString();last=now;}
        // Recover exact words after unrelated noise without blocking typo recovery mid-word.
        if(!prefixes.Contains(buffer) && !words.ContainsKey(buffer))
        {
            for(var start=1;start<buffer.Length-1;start++)
                if(words.ContainsKey(buffer[start..])) {var suffix=buffer[start..];ClearEpisode();buffer=suffix;last=now;break;}
        }
        if(words.TryGetValue(buffer,out var exact))
        {
            if(HasExtensions(buffer)) passedPrefixes.Add(buffer);
            if(target!=null)
            {
                if(buffer==target) {Announce(exact);Learn(buffer);ClearEpisode();}
            }
            else if(DelayFor(buffer)<=0) Announce(exact);
        }
        return Pop();
    }
    public WordEntry? Update(double now,string? target=null)
    {
        if(buffer.Length>0)
        {
            if(target==null && words.TryGetValue(buffer,out var exact) && now-last>=DelayFor(buffer)) Announce(exact);
            if(now-last>=EpisodeGap) Close(target);
            else if(now-last>=store.Settings.WordPause && !prefixes.Contains(buffer)) Close(target);
        }
        return Pop();
    }
    public WordEntry? Flush(string? target=null) {Close(target);return Pop();}
    private void Close(string? target=null)
    {
        if(buffer.Length==0)return;
        words.TryGetValue(buffer,out var match);
        if(match==null && deliberate && store.Settings.ForgivingSpelling && buffer.Length>=4)
        {
            var candidates=words.Values.Where(w=>(target==null || w.Word==target) && w.Word.Length>=3 && Math.Abs(w.Word.Length-buffer.Length)<=1 && Distance(buffer,w.Word)==1).Take(2).ToArray();
            if(candidates.Length==1)match=candidates[0];
        }
        if(match!=null && (target==null || match.Word==target)) {Announce(match);Learn(match.Word);}
        ClearEpisode();
    }
    public static int Distance(string a,string b)
    {
        var d=new int[a.Length+1,b.Length+1];
        for(var i=0;i<=a.Length;i++)d[i,0]=i;
        for(var j=0;j<=b.Length;j++)d[0,j]=j;
        for(var i=1;i<=a.Length;i++)for(var j=1;j<=b.Length;j++)
        {
            d[i,j]=Math.Min(d[i-1,j]+1,Math.Min(d[i,j-1]+1,d[i-1,j-1]+(a[i-1]==b[j-1]?0:1)));
            if(i>1 && j>1 && a[i-1]==b[j-2] && a[i-2]==b[j-1])d[i,j]=Math.Min(d[i,j],d[i-2,j-2]+1);
        }
        return d[a.Length,b.Length];
    }
}
public sealed class CountingRecognizer
{
    public int Expected { get; private set; } = 1;
    public string Pending { get; private set; } = "";
    private double last;
    public void Reset() { Expected=1; Pending=""; }
    public int? Add(char digit,double now)
    {
        if(!char.IsAsciiDigit(digit)) return null;
        if(now-last>4) Pending="";
        last=now; Pending+=digit;
        var expected=Expected.ToString(System.Globalization.CultureInfo.InvariantCulture);
        if(Pending==expected) { var result=Expected; Expected=Expected>=100?1:Expected+1; Pending=""; return result; }
        if(!expected.StartsWith(Pending,StringComparison.Ordinal)) Pending=expected.StartsWith(digit) ? digit.ToString() : "";
        return null;
    }
    public void Update(double now) { if(now-last>4) Pending=""; }
}
