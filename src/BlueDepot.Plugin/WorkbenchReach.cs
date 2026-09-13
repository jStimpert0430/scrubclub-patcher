using System;
using System.Linq;
using BlueDepot.Core;
using UnityEngine;

namespace BlueDepot;

internal static class WorkbenchReach
{
    static readonly int workbenchPrefab="piece_workbench".GetStableHashCode();
    internal static WorkbenchArea[] Connected(Vector3 playerPosition)
    {
        if(!Plugin.WorkbenchNetwork.Value)return Array.Empty<WorkbenchArea>();
        // Read loaded stations afresh: destroyed, unloaded or inaccessible relay benches
        // must stop extending access, including between successive ingredient transfers.
        var nodes=CraftingStation.Instances.OfType<CraftingStation>().Where(s=>s && s.isActiveAndEnabled)
            .Where(s=>!s.GetComponentInParent<Ship>() && !s.GetComponentInParent<Vagon>())
            .Select(s=>new{Station=s,View=s.GetComponent<ZNetView>()})
            .Where(x=>x.View && x.View.IsValid() && x.View.HasOwner() && x.View.GetZDO().GetPrefab()==workbenchPrefab &&
                PrivateArea.CheckAccess(x.Station.transform.position,0f,false,true))
            .Select(x=>new{Id=x.View.GetZDO().m_uid.ToString(),Position=x.Station.transform.position,Radius=x.Station.GetStationBuildRange()})
            .Where(x=>x.Radius>0 && !float.IsInfinity(x.Radius) && !float.IsNaN(x.Position.x) && !float.IsNaN(x.Position.z) &&
                !float.IsInfinity(x.Position.x) && !float.IsInfinity(x.Position.z))
            .Select(x=>new WorkbenchArea(x.Id,x.Position.x,x.Position.z,x.Radius));
        return BlueDepot.Core.WorkbenchNetwork.Connected(nodes,playerPosition.x,playerPosition.z);
    }
    internal static bool Covers(WorkbenchArea[] network,Vector3 point)=>BlueDepot.Core.WorkbenchNetwork.Covers(network,point.x,point.z);
}
