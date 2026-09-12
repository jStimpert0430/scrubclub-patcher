using System;
using System.IO;
using System.Linq;
using Mono.Cecil;
using Xunit;
namespace BlueDepot.Tests;

// Metadata checks: catches renamed/removed Harmony targets without loading Unity.
// This is a compile/ABI regression check, not a substitute for an in-game test.
public class GameContractTests
{
    static string Root()
    {
        var dir=new DirectoryInfo(AppContext.BaseDirectory);
        while(dir!=null && !File.Exists(Path.Combine(dir.FullName,"Directory.Build.props")))dir=dir.Parent;
        return dir?.FullName??throw new Exception("Project root not found");
    }
    [Fact] public void EveryHarmonyTargetExistsInInstalledGameOrPinnedDependency()
    {
        var root=Root();var game=Environment.GetEnvironmentVariable("GameManaged")??"/home/bunta/.local/share/Steam/steamapps/common/Valheim/valheim_Data/Managed";
        using var resolver=new DefaultAssemblyResolver();resolver.AddSearchDirectory(Path.Combine(root,".deps"));resolver.AddSearchDirectory(game);
        using var plugin=AssemblyDefinition.ReadAssembly(Path.Combine(root,"src/BlueDepot.Plugin/bin/Release/net472/BlueDepot.dll"),new ReaderParameters{AssemblyResolver=resolver});
        using var valheim=AssemblyDefinition.ReadAssembly(Path.Combine(game,"assembly_valheim.dll"));
        using var muc=AssemblyDefinition.ReadAssembly(Path.Combine(root,".deps/MultiUserChest.dll"));
        var types=valheim.MainModule.Types.Concat(muc.MainModule.Types).ToArray();int checkedCount=0;
        foreach(var patch in plugin.MainModule.Types.SelectMany(t=>t.CustomAttributes).Where(a=>a.AttributeType.FullName=="HarmonyLib.HarmonyPatch"))
        {
            var targetArg=patch.ConstructorArguments.FirstOrDefault(a=>a.Type.FullName=="System.Type");
            var target=Assert.IsType<TypeReference>(targetArg.Value);
            var type=Assert.Single(types,t=>t.FullName==target.FullName);
            var nameArg=patch.ConstructorArguments.FirstOrDefault(a=>a.Type.FullName=="System.String");
            string name=nameArg.Value as string??".ctor";
            var argsArg=patch.ConstructorArguments.FirstOrDefault(a=>a.Type.FullName=="System.Type[]");
            var parameters=argsArg.Value as CustomAttributeArgument[];
            Assert.Contains(type.Methods,m=>m.Name==name && (parameters==null || m.Parameters.Select(p=>p.ParameterType.FullName).SequenceEqual(parameters.Select(p=>((TypeReference)p.Value).FullName))));
            checkedCount++;
        }
        Assert.True(checkedCount>=10);
    }
    [Fact] public void DependencyAndPluginGameTypeReferencesResolve()
    {
        var root=Root();var game=Environment.GetEnvironmentVariable("GameManaged")??"/home/bunta/.local/share/Steam/steamapps/common/Valheim/valheim_Data/Managed";
        using var resolver=new DefaultAssemblyResolver();resolver.AddSearchDirectory(game);resolver.AddSearchDirectory(Path.Combine(root,".deps"));
        foreach(var path in new[]{Path.Combine(root,".deps/MultiUserChest.dll"),Path.Combine(root,"src/BlueDepot.Plugin/bin/Release/net472/BlueDepot.dll")})
        {
            using var assembly=AssemblyDefinition.ReadAssembly(path,new ReaderParameters{AssemblyResolver=resolver});
            foreach(var type in assembly.MainModule.GetTypeReferences().Where(t=>t.Scope.Name.StartsWith("assembly_",StringComparison.Ordinal)))
                Assert.True(type.Resolve()!=null,"Missing game type: "+type.FullName+" in "+path);
        }
    }
    [Fact] public void DepotUsesGameFontsAndNativeInventoryInsteadOfStandaloneOverlay()
    {
        using var assembly=AssemblyDefinition.ReadAssembly(Path.Combine(Root(),"src/BlueDepot.Plugin/bin/Release/net472/BlueDepot.dll"));
        var ui=Assert.Single(assembly.MainModule.Types,t=>t.FullName=="BlueDepot.DepotUi");
        var calls=ui.Methods.Where(m=>m.HasBody).SelectMany(m=>m.Body.Instructions)
            .Select(i=>i.Operand).OfType<MethodReference>().Select(m=>m.DeclaringType.FullName+"."+m.Name).ToArray();
        Assert.Contains("InventoryGui.Show",calls);
        Assert.Contains("Jotunn.Managers.GUIManager.CreateWoodpanel",calls);
        Assert.Contains("Jotunn.Managers.GUIManager.CreateButton",calls);
        Assert.Contains("Jotunn.Managers.GUIManager.CreateText",calls);
        Assert.Contains("Jotunn.Managers.GUIManager.get_AveriaSerifBold",calls);
        Assert.DoesNotContain("TMPro.TMP_Settings.get_defaultFontAsset",calls);
        Assert.DoesNotContain("Jotunn.Managers.GUIManager.BlockInput",calls);
        Assert.Contains(ui.Methods.Where(m=>m.HasBody).SelectMany(m=>m.Body.Instructions),i=>i.Operand is FieldReference f && f.DeclaringType.FullName=="InventoryGui" && f.Name=="m_player");
    }
}
