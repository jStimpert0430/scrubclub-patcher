using System;
using System.IO;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Xunit;
namespace Torchlight.Tests;
public class GameContractTests
{
    [Fact] public void GameStillUsesPlayerDistanceAndLightCutoff()
    {
        using var a=AssemblyDefinition.ReadAssembly("/home/bunta/.local/share/Steam/steamapps/common/Valheim/valheim_Data/Managed/assembly_valheim.dll");
        var t=a.MainModule.Types.Single(t=>t.Name=="LightLod");
        Assert.Single(t.Methods,m=>m.Name=="Awake");
        Assert.Contains(t.Fields,f=>f.Name=="m_lightDistance" && f.FieldType.FullName=="System.Single" && f.IsPublic);
        Assert.Contains(t.Methods.Single(m=>m.Name=="GetLightReferencePoint").Body.Instructions,i=>i.Operand is FieldReference f && f.Name=="m_localPlayer");
        Assert.Contains(t.NestedTypes.SelectMany(t=>t.Methods).Where(m=>m.HasBody).SelectMany(m=>m.Body.Instructions),i=>i.Operand is FieldReference f && f.Name=="m_lightDistance");
    }
    [Fact] public void PluginOnlyWritesVisibilityDistanceOnGameComponents()
    {
        var dir=new DirectoryInfo(AppContext.BaseDirectory);
        while(dir!=null && !File.Exists(Path.Combine(dir.FullName,"src/Torchlight.Plugin/Torchlight.Plugin.csproj")))dir=dir.Parent;
        Assert.NotNull(dir);
        using var a=AssemblyDefinition.ReadAssembly(Path.Combine(dir!.FullName,"src/Torchlight.Plugin/bin/Release/net472/Torchlight.dll"));
        var methods=a.MainModule.Types.SelectMany(t=>t.Methods.Concat(t.NestedTypes.SelectMany(n=>n.Methods))).Where(m=>m.HasBody);
        var instructions=methods.SelectMany(m=>m.Body.Instructions).ToArray();
        var writes=instructions.Where(i=>i.OpCode==OpCodes.Stfld).Select(i=>i.Operand).OfType<FieldReference>().Where(f=>f.DeclaringType.Scope.Name=="assembly_valheim").ToArray();
        Assert.Single(writes);Assert.Equal("m_lightDistance",writes[0].Name);
        Assert.DoesNotContain(instructions,i=>i.Operand is MethodReference m && m.DeclaringType.Namespace=="UnityEngine" && m.Name.StartsWith("set_"));
    }
}
