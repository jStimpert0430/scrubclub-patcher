using System.Linq;
using RoadLights.Core;
using Xunit;
namespace RoadLights.Tests;
public class RoadTests
{
    [Fact] public void OnlySuccessfulLocalPathenCanRequestLighting()
    {
        Assert.True(RoadPolicy.ShouldPlace("path_v2",true,true,true,false));
        foreach(var other in new[]{"mud_road_v2","paved_road_v2","raise_v2","piece_groundtorch_wood","path"})
            Assert.False(RoadPolicy.ShouldPlace(other,true,true,true,false));
        Assert.False(RoadPolicy.ShouldPlace("path_v2",false,true,true,false));
        Assert.False(RoadPolicy.ShouldPlace("path_v2",true,false,true,false));
        Assert.False(RoadPolicy.ShouldPlace("path_v2",true,true,false,false));
        Assert.False(RoadPolicy.ShouldPlace("path_v2",true,true,true,true));
    }
    [Fact] public void SpacingIncludesBoundaryAndBothDirections()
    {
        Assert.True(RoadPolicy.WithinRadius(0,0,0));
        Assert.True(RoadPolicy.WithinRadius(RoadPolicy.Radius,0,0));
        Assert.True(RoadPolicy.WithinRadius(-RoadPolicy.Radius,0,0));
        Assert.False(RoadPolicy.WithinRadius(RoadPolicy.Radius+0.01f,0,0));
        Assert.False(RoadPolicy.WithinRadius(10,0,10));
        Assert.False(RoadPolicy.WithinRadius(0,20,0));
    }
    [Theory]
    [InlineData(0,1)] [InlineData(1,0)] [InlineData(-4,3)] [InlineData(0,0)]
    public void BothRoadsidesHaveEqualOffsetAndOppositeDirections(float x,float z)
    {
        var a=RoadPolicy.Side(x,z,true);var b=RoadPolicy.Side(x,z,false);
        Assert.Equal(-a.x,b.x);Assert.Equal(-a.z,b.z);
        Assert.InRange(a.x*a.x+a.z*a.z,8.999f,9.001f);
        Assert.InRange(a.x*x+a.z*z,-0.001f,0.001f);
    }
    [Fact] public void RoadMenuExcludesWallAndHangingLightsButKeepsStandingBraziers()
    {
        foreach(var name in new[]{"piece_walltorch","piece_brazierceiling01","piece_dvergr_lantern","fire_pit","hearth","bonfire","unknown"})
            Assert.False(RoadPolicy.Freestanding(name));
        foreach(var name in new[]{"piece_groundtorch_wood","piece_brazierfloor01","piece_brazierfloor02","piece_dvergr_lantern_pole"})
            Assert.True(RoadPolicy.Freestanding(name));
        Assert.Equal(12,LightingPolicy.Prefabs.Count(RoadPolicy.Freestanding));
    }
}
