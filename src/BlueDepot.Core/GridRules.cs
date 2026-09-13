using System;
namespace BlueDepot.Core;

public static class GridRules
{
    public const int PageSize=24;
    public static int Pages(int items)=>items<=0?1:1+(items-1)/PageSize;
    public static int ClampPage(int page,int items)=>Math.Max(0,Math.Min(page,Pages(items)-1));
    public static int MoveAmount(int requested,int available,int capacity)=>Math.Max(0,Math.Min(requested,Math.Min(available,capacity)));
}
