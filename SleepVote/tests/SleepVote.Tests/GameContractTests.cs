using System;
using System.IO;
using System.Linq;
using Mono.Cecil;
using Xunit;
namespace SleepVote.Tests;
public class GameContractTests
{
    [Fact] public void HarmonyHooksMatchInstalledGame()
    {
        using var game=AssemblyDefinition.ReadAssembly("/home/bunta/.local/share/Steam/steamapps/common/Valheim/valheim_Data/Managed/assembly_valheim.dll");
        var dir=new DirectoryInfo(AppContext.BaseDirectory);
        while(dir!=null && !Directory.Exists(Path.Combine(dir.FullName,"src/SleepVote.Plugin")))dir=dir.Parent;
        using var plugin=AssemblyDefinition.ReadAssembly(Path.Combine(dir!.FullName,"src/SleepVote.Plugin/bin/Release/net472/SleepVote.dll"));
        foreach(var type in plugin.MainModule.Types)
        foreach(var attr in type.CustomAttributes.Where(a=>a.AttributeType.Name=="HarmonyPatch"))
        {
            var target=(TypeReference)attr.ConstructorArguments[0].Value;
            var method=(string)attr.ConstructorArguments[1].Value;
            var engineType=game.MainModule.Types.Single(t=>t.FullName==target.FullName);
            Assert.Contains(engineType.Methods,m=>m.Name==method);
            foreach(var p in type.Methods.Where(m=>m.Name=="Prefix"||m.Name=="Postfix").SelectMany(m=>m.Parameters).Where(p=>!p.Name.StartsWith("__")))
                Assert.Contains(engineType.Methods.Where(m=>m.Name==method).SelectMany(m=>m.Parameters),q=>q.Name==p.Name&&q.ParameterType.FullName==p.ParameterType.FullName);
        }
        var popup=game.MainModule.Types.Single(t=>t.Name=="UnifiedPopup");
        Assert.Contains(popup.Fields,f=>f.Name=="instance");Assert.Contains(popup.Fields,f=>f.Name=="popupStack");
        var instructions=plugin.MainModule.Types.SelectMany(t=>t.Methods).Where(m=>m.HasBody).SelectMany(m=>m.Body.Instructions);
        Assert.DoesNotContain(instructions,i=>i.Operand is MethodReference m && new[]{"SkipToMorning","AddStatusEffect","SetSleeping","AttachStop"}.Contains(m.Name));
    }
}
