using System;
using System.Collections.Generic;
using System.Linq;

namespace BlueDepot.Core;

public sealed class ConsolidationMove
{
    public string SourceChest { get; }
    public string SourceSlot { get; }
    public Placement Target { get; }
    public ConsolidationMove(string chest,string slot,Placement target)
    {SourceChest=chest;SourceSlot=slot;Target=target;}
}

public static class Consolidation
{
    // Items flow only toward earlier slots in a deterministic global order.
    // This cannot ping-pong stacks between concurrently sorting chest owners.
    public static ConsolidationMove? Next(IEnumerable<Chest> inventories,string sourceChest)
    {
        var rows=inventories.Where(c=>c.Accessible).GroupBy(c=>c.Id).Select(g=>g.First())
            .OrderBy(c=>c.Id,StringComparer.Ordinal).SelectMany(c=>c.Items.OrderBy(i=>int.Parse(i.Slot))
                .Select(i=>(Chest:c,Item:i))).ToArray();
        for(int source=1;source<rows.Length;source++)
        {
            var from=rows[source];if(from.Chest.Id!=sourceChest)continue;
            for(int target=0;target<source;target++)
            {
                var to=rows[target];
                if(to.Item.Key!=from.Item.Key || to.Item.Maximum!=from.Item.Maximum || to.Item.Count>=to.Item.Maximum)continue;
                return new ConsolidationMove(from.Chest.Id,from.Item.Slot,new Placement(to.Chest.Id,to.Item.Slot,
                    Math.Min(from.Item.Count,to.Item.Maximum-to.Item.Count)));
            }
        }
        return null;
    }
}
