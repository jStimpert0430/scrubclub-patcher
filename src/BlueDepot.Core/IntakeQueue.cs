using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BlueDepot.Core;

public static class IntakeQueue
{
    public static string Encode(IEnumerable<KeyValuePair<int,string>> entries)=>string.Join("\n",entries
        .Where(p=>p.Key>=0 && p.Key<100 && p.Value.Length<=8192).OrderBy(p=>p.Key)
        .Select(p=>p.Key+","+Convert.ToBase64String(Encoding.UTF8.GetBytes(p.Value))));
    public static Dictionary<int,string> Decode(string data)
    {
        var result=new Dictionary<int,string>();
        if(data.Length>1200000)return result;
        foreach(var line in data.Split('\n').Take(100))
        {
            int comma=line.IndexOf(',');
            if(comma<1 || !int.TryParse(line.Substring(0,comma),out var slot) || slot<0 || slot>=100)continue;
            try
            {
                var key=Encoding.UTF8.GetString(Convert.FromBase64String(line.Substring(comma+1)));
                if(key.Length<=8192)result[slot]=key;
            }
            catch(FormatException) { }
        }
        return result;
    }
}
