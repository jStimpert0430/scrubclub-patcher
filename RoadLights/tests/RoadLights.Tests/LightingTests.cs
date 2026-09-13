using System;
using System.Linq;
using RoadLights.Core;
using Xunit;
namespace RoadLights.Tests;
public class LightingTests
{
    [Theory]
    [InlineData("piece_groundtorch_wood")][InlineData("piece_groundtorch")][InlineData("piece_groundtorch_blue")]
    [InlineData("piece_groundtorch_green")][InlineData("piece_walltorch")][InlineData("piece_groundtorch_mist")]
    [InlineData("piece_dvergr_lantern")][InlineData("piece_dvergr_lantern_pole")][InlineData("piece_hoodedlantern")]
    [InlineData("piece_Lavalantern")][InlineData("Candle_resin")][InlineData("piece_jackoturnip")]
    [InlineData("piece_brazierceiling01")][InlineData("piece_brazierfloor01")][InlineData("piece_brazierfloor02")]
    public void DecorativeLightsAreIncluded(string prefab)=>Assert.True(LightingPolicy.Includes(prefab));
    [Theory]
    [InlineData("fire_pit")][InlineData("hearth")][InlineData("bonfire")][InlineData("piece_oven")]
    [InlineData("smelter")][InlineData("charcoal_kiln")][InlineData("blastfurnace")][InlineData("piece_cookingstation")]
    [InlineData("piece_workbench")][InlineData("piece_magetable_ext2")][InlineData("Torch")][InlineData("Lantern")]
    [InlineData("piece_groundtorch_modded")][InlineData("Candle_resin_bogwitch")][InlineData(null)]
    public void FunctionalFiresAndUnknownPiecesRemainVanilla(string? prefab)=>Assert.False(LightingPolicy.Includes(prefab));
    [Fact] public void PolicyCannotBeChangedThroughExportedList()
    {Assert.Equal(15,LightingPolicy.Prefabs.Distinct().Count());Assert.False(LightingPolicy.Prefabs is System.Collections.Generic.HashSet<string>);}
    [Theory]
    [InlineData(true,true,true,false,false,true,true)]
    [InlineData(true,true,true,true,false,true,false)]
    [InlineData(true,true,true,false,true,true,false)]
    [InlineData(true,true,true,false,false,false,false)]
    [InlineData(false,true,true,false,false,true,false)]
    [InlineData(true,false,true,false,false,true,false)]
    [InlineData(true,true,false,false,false,true,false)]
    public void ToggleRetainsNativeCapabilitiesAndWardAccess(bool target,bool valid,bool toggle,bool hold,bool alt,bool access,bool expected)
        =>Assert.Equal(expected,LightingPolicy.CanToggle(target,valid,toggle,hold,alt,access));
}
