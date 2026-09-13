using System;
using System.Collections.Generic;
using System.Linq;

namespace BlueDepot.Core;

public readonly struct WorkbenchArea
{
    public string Id { get; }
    public double X { get; }
    public double Z { get; }
    public double Radius { get; }
    public WorkbenchArea(string id,double x,double z,double radius)
    {
        if(string.IsNullOrEmpty(id) || !Finite(x) || !Finite(z) || !Finite(radius) || radius<=0)
            throw new ArgumentException("Invalid workbench area");
        Id=id;X=x;Z=z;Radius=radius;
    }
    static bool Finite(double n)=>!double.IsNaN(n) && !double.IsInfinity(n);
    public bool Contains(double x,double z)=>DistanceSquared(X,Z,x,z)<Radius*Radius;
    public bool Overlaps(WorkbenchArea other)=>DistanceSquared(X,Z,other.X,other.Z)<(Radius+other.Radius)*(Radius+other.Radius);
    static double DistanceSquared(double x,double z,double otherX,double otherZ)=>(x-otherX)*(x-otherX)+(z-otherZ)*(z-otherZ);
}

public static class WorkbenchNetwork
{
    // Match vanilla's horizontal build circles; touching edges alone have no shared area.
    // Nodes are supplied by the game adapter only after loading/access checks.
    public static WorkbenchArea[] Connected(IEnumerable<WorkbenchArea> areas,double playerX,double playerZ)
    {
        var all=areas.GroupBy(a=>a.Id,StringComparer.Ordinal).Select(g=>g.First()).ToArray();
        var visited=new bool[all.Length];var queue=new Queue<int>();
        for(int i=0;i<all.Length;i++)if(all[i].Contains(playerX,playerZ)){visited[i]=true;queue.Enqueue(i);}
        while(queue.Count>0)
        {
            var current=all[queue.Dequeue()];
            for(int i=0;i<all.Length;i++)if(!visited[i] && current.Overlaps(all[i])){visited[i]=true;queue.Enqueue(i);}
        }
        return all.Where((_,i)=>visited[i]).ToArray();
    }
    public static bool Covers(IEnumerable<WorkbenchArea> connected,double x,double z)=>connected.Any(a=>a.Contains(x,z));
}
