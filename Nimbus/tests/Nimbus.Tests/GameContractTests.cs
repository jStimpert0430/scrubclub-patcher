using System;
using System.IO;
using System.Linq;
using Mono.Cecil;
using Xunit;
namespace Nimbus.Tests;
public class GameContractTests
{
    static string Root()
    {
        var d=new DirectoryInfo(AppContext.BaseDirectory);
        while(d!=null && !File.Exists(Path.Combine(d.FullName,"src/Nimbus.Plugin/Nimbus.Plugin.csproj")))d=d.Parent;
        return d?.FullName??throw new Exception("Nimbus root not found");
    }
    [Fact] public void ControlHookMatchesInstalledGame()
    {
        using var game=AssemblyDefinition.ReadAssembly("/home/bunta/.local/share/Steam/steamapps/common/Valheim/valheim_Data/Managed/assembly_valheim.dll");
        var method=game.MainModule.Types.Single(t=>t.Name=="ShipControlls").Methods.Single(m=>m.Name=="ApplyControlls");
        Assert.Contains(method.Parameters,p=>p.Name=="moveDir" && p.ParameterType.Name=="Vector3");
        Assert.Contains(method.Parameters,p=>p.Name=="lookDir" && p.ParameterType.Name=="Vector3");
        Assert.Contains(game.MainModule.Types.Single(t=>t.Name=="ShipControlls").Methods,m=>m.Name=="GetUser" && m.ReturnType.Name=="Int64");
    }
    [Fact] public void MountHooksMatchGameAndBypassBoatVolumeRequirementOnlyForNimbus()
    {
        using var game=AssemblyDefinition.ReadAssembly("/home/bunta/.local/share/Steam/steamapps/common/Valheim/valheim_Data/Managed/assembly_valheim.dll");
        var controls=game.MainModule.Types.Single(t=>t.Name=="ShipControlls");
        var request=controls.Methods.Single(m=>m.Name=="RPC_RequestControl");
        Assert.Equal(new[]{"sender","playerID"},request.Parameters.Select(p=>p.Name));
        Assert.All(request.Parameters,p=>Assert.Equal("Int64",p.ParameterType.Name));
        Assert.Contains(controls.Methods,m=>m.Name=="HaveValidUser" && m.ReturnType.Name=="Boolean");
        using var plugin=AssemblyDefinition.ReadAssembly(Path.Combine(Root(),"src/Nimbus.Plugin/bin/Release/net472/Nimbus.dll"));
        foreach(var type in new[]{"NimbusMountRequest","NimbusValidRider"})
        {
            var calls=plugin.MainModule.Types.Single(t=>t.Name==type).Methods.Single(m=>m.Name=="Prefix").Body.Instructions.Select(i=>i.Operand).OfType<MethodReference>().ToArray();
            Assert.Contains(calls,m=>m is GenericInstanceMethod g && g.GenericArguments.Any(a=>a.Name=="NimbusMotor"));
            Assert.DoesNotContain(calls,m=>m.Name=="IsPlayerInBoat");
        }
    }
    [Fact] public void HoverTicksExactlyOnceThroughUnityFixedUpdate()
    {
        using var plugin=AssemblyDefinition.ReadAssembly(Path.Combine(Root(),"src/Nimbus.Plugin/bin/Release/net472/Nimbus.dll"));
        var motor=plugin.MainModule.Types.Single(t=>t.Name=="NimbusMotor");
        Assert.Contains(motor.Methods.Single(m=>m.Name=="FixedUpdate").Body.Instructions,i=>i.Operand is MethodReference m && m.Name=="Step");
        var prefix=plugin.MainModule.Types.Single(t=>t.Name=="HoverPhysics").Methods.Single(m=>m.Name=="Prefix");
        Assert.DoesNotContain(prefix.Body.Instructions,i=>i.Operand is MethodReference m && m.Name=="Step");
    }
    [Fact] public void MotorHasNoWindDependencyAndValidatesInputOwner()
    {
        using var plugin=AssemblyDefinition.ReadAssembly(Path.Combine(Root(),"src/Nimbus.Plugin/bin/Release/net472/Nimbus.dll"));
        var motor=plugin.MainModule.Types.Single(t=>t.Name=="NimbusMotor");
        var calls=motor.Methods.Where(m=>m.HasBody).SelectMany(m=>m.Body.Instructions).Select(i=>i.Operand).OfType<MethodReference>().ToArray();
        Assert.DoesNotContain(calls,m=>m.Name.Contains("Wind") || m.DeclaringType.Name=="EnvMan");
        var receive=motor.Methods.Single(m=>m.Name=="ReceiveInput").Body.Instructions.Select(i=>i.Operand).OfType<MethodReference>().ToArray();
        foreach(var name in new[]{"IsOwner","GetUser","HaveValidUser","GetOwner","IsDead"})Assert.Contains(receive,m=>m.Name==name);
        Assert.Contains(calls,m=>m.Name=="FreshInput");
    }
}
