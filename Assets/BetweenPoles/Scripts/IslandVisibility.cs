using System.Collections.Generic;
using UnityEngine;
namespace BetweenPoles {
// Count intervening empty grid cells along cardinal steps, including offset shores.
public static class IslandVisibility {
    public const int MaximumEmptyCells=2;
    public static int GapForScene(string sceneName){return sceneName=="Chapter01_IceWorld"?1:MaximumEmptyCells;}
    public static HashSet<T> Neighbors<T>(Dictionary<Vector2Int,T> owners,T current,int maximumEmptyCells=MaximumEmptyCells){
        var result=new HashSet<T>();var equality=EqualityComparer<T>.Default;
        var visited=new HashSet<Vector2Int>();var queue=new Queue<KeyValuePair<Vector2Int,int>>();
        foreach(var pair in owners)if(equality.Equals(pair.Value,current)){
            visited.Add(pair.Key);queue.Enqueue(new KeyValuePair<Vector2Int,int>(pair.Key,0));
        }
        var directions=new[]{Vector2Int.right,Vector2Int.left,Vector2Int.up,Vector2Int.down};
        while(queue.Count>0){
            var item=queue.Dequeue();
            foreach(var direction in directions){
                var cell=item.Key+direction;
                if(owners.TryGetValue(cell,out var owner)){
                    if(!equality.Equals(owner,current))result.Add(owner);
                    // A neighboring island never becomes a second source of visibility.
                    continue;
                }
                if(item.Value<maximumEmptyCells&&visited.Add(cell))queue.Enqueue(new KeyValuePair<Vector2Int,int>(cell,item.Value+1));
            }
        }
        return result;
    }
}
}
