using System;
using System.IO;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Xunit;
namespace MonkStyle.Tests;
public class GameContractTests
{
    static string Root()
    {
        var d=new DirectoryInfo(AppContext.BaseDirectory);
        while(d!=null && !File.Exists(Path.Combine(d.FullName,"src/MonkStyle.Plugin/MonkStyle.Plugin.csproj")))d=d.Parent;
        return d?.FullName??throw new Exception("Project root not found");
    }
    [Fact] public void PluginNeverWritesSharedWeaponToolTierOrTreeHardness()
    {
        using var a=AssemblyDefinition.ReadAssembly(Path.Combine(Root(),"src/MonkStyle.Plugin/bin/Release/net472/MonkStyle.dll"));
        var writes=a.MainModule.Types.SelectMany(t=>t.Methods.Concat(t.NestedTypes.SelectMany(n=>n.Methods))).Where(m=>m.HasBody)
            .SelectMany(m=>m.Body.Instructions).Where(i=>i.OpCode.Code==Code.Stfld).Select(i=>i.Operand).OfType<FieldReference>().ToArray();
        Assert.DoesNotContain(writes,f=>f.Name=="m_minToolTier");
        Assert.All(writes.Where(f=>f.Name=="m_toolTier"),f=>Assert.Equal("HitData",f.DeclaringType.Name));
        Assert.Contains(writes,f=>f.Name=="m_toolTier"&&f.DeclaringType.Name=="HitData");
    }
    [Fact] public void MeleeScopeHasExceptionSafeRestoration()
    {
        using var a=AssemblyDefinition.ReadAssembly(Path.Combine(Root(),"src/MonkStyle.Plugin/bin/Release/net472/MonkStyle.dll"));
        var type=a.MainModule.Types.Single(t=>t.Name=="MeleeScope");
        Assert.Contains(type.Methods,m=>m.Name=="Finalizer" && m.Parameters.Any(p=>p.Name=="__state"));
        Assert.Contains(type.Fields.Single(f=>f.Name=="Current").CustomAttributes,a=>a.AttributeType.Name=="ThreadStaticAttribute");
    }
    [Fact] public void NativeTreeAndAttackHooksStillExist()
    {
        using var a=AssemblyDefinition.ReadAssembly("/home/bunta/.local/share/Steam/steamapps/common/Valheim/valheim_Data/Managed/assembly_valheim.dll");
        foreach(var name in new[]{"TreeBase","TreeLog","Destructible"})
            Assert.Contains(a.MainModule.Types.Single(t=>t.Name==name).Methods,m=>m.Name=="Damage"&&m.Parameters.Count==1&&m.Parameters[0].ParameterType.Name=="HitData");
        var attack=a.MainModule.Types.Single(t=>t.Name=="Attack");
        Assert.Contains(attack.Methods,m=>m.Name=="DoMeleeAttack"&&m.Parameters.Count==0);
        Assert.Contains(attack.Fields,f=>f.Name=="m_weapon"&&f.FieldType.Name=="ItemData");
    }
}
