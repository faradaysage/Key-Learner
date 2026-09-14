using System.Collections.Generic;
using UnityEngine;
namespace KeyLearner.Unity
{
    /// <summary>Composed geological silhouettes, separate from nearby streamed learning terrain.</summary>
    public sealed class PrehistoricLandmarks
    {
        readonly GameServices services;
        readonly Transform parent;
        int currentChapter=int.MinValue;
        readonly Dictionary<int,GameObject> chapters=new Dictionary<int,GameObject>();
        public PrehistoricLandmarks(GameServices services,Transform parent){this.services=services;this.parent=parent;}
        public void Tick(Vector3 player)
        {
            int chapter=Mathf.FloorToInt(player.z/800);
            if(chapter==currentChapter)return;currentChapter=chapter;
            var expired=new List<int>();foreach(var entry in chapters)if(Mathf.Abs(entry.Key-chapter)>1)expired.Add(entry.Key);
            foreach(int key in expired){Object.Destroy(chapters[key]);chapters.Remove(key);}
            for(int index=chapter-1;index<=chapter+1;index++)if(!chapters.ContainsKey(index))chapters[index]=Build(index);
        }
        GameObject Build(int chapter)
        {
            var group=new GameObject("Ancient skyline "+chapter);group.transform.SetParent(parent,false);
            var random=new System.Random(unchecked(chapter*38717+711));
            for(int i=0;i<6;i++)
            {
                float z=chapter*800+100+i*120,x=(i%2==0?-1:1)*(290+random.Next(90));
                float size=90+random.Next(110);
                services.Content.Spawn("mountainside",0,group.transform,new Vector3(x,-20,z),size,i%2==0?75:255);
                if(i%2==0)services.Content.SpawnWidth("cliff",0,group.transform,new Vector3(x*.65f,-8,z-45),240,random.Next(-15,16));
            }
            return group;
        }
    }
}
