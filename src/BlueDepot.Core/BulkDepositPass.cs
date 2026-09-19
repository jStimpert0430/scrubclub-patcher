using System;
using System.Collections.Generic;

namespace BlueDepot.Core;

// Track quantities and stable item keys, never ItemData references: a declined
// network deposit can replace the source stack while returning its contents.
public sealed class BulkDepositPass
{
    readonly Dictionary<string,int> remaining=new Dictionary<string,int>();
    readonly Dictionary<string,int> failures=new Dictionary<string,int>();
    public void Include(string key,int amount)
    {
        remaining.TryGetValue(key,out var count);remaining[key]=count+amount;
    }
    public int Allowance(string key)=>remaining.TryGetValue(key,out var count) &&
        (!failures.TryGetValue(key,out var failed) || failed<3)?count:0;
    public void Observe(string key,int before,int after,bool confirmed)
    {
        // Source removal before an acknowledgement is not proof of delivery.
        if(!confirmed){remaining[key]=0;return;}
        int moved=Math.Max(0,before-after);
        if(moved>0){remaining[key]=Math.Max(0,Allowance(key)-moved);failures[key]=0;}
        else {failures.TryGetValue(key,out var count);failures[key]=count+1;}
    }
    public void NoSpace(string key)=>remaining[key]=0;
}
