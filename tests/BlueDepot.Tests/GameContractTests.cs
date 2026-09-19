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
    [Fact] public void NearbyDiscoveryChecksOriginPrivacyBeforeEnumeratingDestinations()
    {
        using var plugin=AssemblyDefinition.ReadAssembly(Path.Combine(Root(),"src/BlueDepot.Plugin/bin/Release/net472/BlueDepot.dll"));
        var nearby=plugin.MainModule.Types.Single(t=>t.Name=="Storage").Methods.Single(m=>m.Name=="Nearby");
        var calls=nearby.Body.Instructions.Where(i=>i.Operand is MethodReference).Select(i=>(MethodReference)i.Operand).ToList();
        int privacy=calls.FindIndex(m=>m.DeclaringType.Name=="ChestPrivacy" && m.Name=="IsPrivate");
        Assert.True(privacy>=0 && privacy<calls.FindIndex(m=>m.Name=="Where"));
    }
    [Fact] public void DiscoveryUsesNetworkRootsWithoutFilteringByBuilder()
    {
        using var plugin=AssemblyDefinition.ReadAssembly(Path.Combine(Root(),"src/BlueDepot.Plugin/bin/Release/net472/BlueDepot.dll"));
        var storage=plugin.MainModule.Types.Single(t=>t.Name=="Storage");
        var view=storage.Methods.Single(m=>m.Name=="View");
        Assert.Contains(view.Body.Instructions,i=>i.Operand is FieldReference f && f.Name=="m_rootObjectOverride");
        var methods=storage.Methods.Concat(storage.NestedTypes.SelectMany(t=>t.Methods)).Where(m=>m.HasBody);
        Assert.DoesNotContain(methods.SelectMany(m=>m.Body.Instructions),i=>i.Operand is MethodReference m && m.Name=="GetCreator");
    }
    [Fact] public void AutomationTransfersRecheckPrivacyOnTheReceivingOwner()
    {
        using var plugin=AssemblyDefinition.ReadAssembly(Path.Combine(Root(),"src/BlueDepot.Plugin/bin/Release/net472/BlueDepot.dll"));
        foreach(var name in new[]{"GuardAdd","GuardRemove","GuardMove"})
        {
            var prefix=plugin.MainModule.Types.Single(t=>t.Name==name).Methods.Single(m=>m.Name=="Prefix");
            Assert.Contains(prefix.Body.Instructions,i=>i.Operand is MethodReference m && m.DeclaringType.Name=="TransferGuards" && m.Name=="Permitted");
        }
    }
    [Fact] public void PrivateOptOutDoesNotPatchNativeChestAccess()
    {
        using var resolver=new DefaultAssemblyResolver();resolver.AddSearchDirectory(Path.Combine(Root(),".deps"));
        using var plugin=AssemblyDefinition.ReadAssembly(Path.Combine(Root(),"src/BlueDepot.Plugin/bin/Release/net472/BlueDepot.dll"),new ReaderParameters{AssemblyResolver=resolver});
        var privacy=plugin.MainModule.Types.Single(t=>t.Name=="ChestPrivacy");
        Assert.DoesNotContain(privacy.Methods.Where(m=>m.HasBody).SelectMany(m=>m.Body.Instructions),i=>i.OpCode.Code==Mono.Cecil.Cil.Code.Stfld && i.Operand is FieldReference f && f.Name=="m_privacy");
        Assert.DoesNotContain(plugin.MainModule.Types.SelectMany(t=>t.CustomAttributes).Where(a=>a.AttributeType.Name=="HarmonyPatch"),a=>a.ConstructorArguments.Any(v=>v.Value is string name && name=="CheckAccess"));
    }

    [Fact] public void StationReachUsesVanillaHoverInsteadOfRootDistance()
    {
        using var plugin=AssemblyDefinition.ReadAssembly(Path.Combine(Root(),"src/BlueDepot.Plugin/bin/Release/net472/BlueDepot.dll"));
        var valid=plugin.MainModule.Types.Single(t=>t.Name=="StationSupplies").Methods.Single(m=>m.Name=="Valid");
        var calls=valid.Body.Instructions.Where(i=>i.Operand is MethodReference).Select(i=>(MethodReference)i.Operand).ToArray();
        Assert.Contains(calls,m=>m.Name=="GetHoverObject");
        Assert.Contains(calls,m=>m.Name=="IsChildOf");
        Assert.DoesNotContain(calls,m=>m.DeclaringType.Name=="Vector3"&&m.Name=="Distance");
    }
    [Fact] public void StationCarriedPathPrecedesTransferGateAndRemoteCounting()
    {
        using var plugin=AssemblyDefinition.ReadAssembly(Path.Combine(Root(),"src/BlueDepot.Plugin/bin/Release/net472/BlueDepot.dll"));
        var supply=plugin.MainModule.Types.Single(t=>t.Name=="StationSupplies").Methods.Single(m=>m.Name=="Supply");
        var calls=supply.Body.Instructions.Where(i=>i.Operand is MethodReference).Select(i=>(MethodReference)i.Operand).ToList();
        int carried=calls.FindIndex(m=>m.DeclaringType.Name=="StationSupplyRules" && m.Name=="UseCarriedFirst");
        int busy=calls.FindIndex(m=>m.DeclaringType.Name=="Transfers" && m.Name=="get_Busy");
        Assert.True(carried>=0 && busy>carried);
        var issue=plugin.MainModule.Types.Single(t=>t.Name=="Transfers").Methods.Single(m=>m.Name=="Issue");
        Assert.Contains(issue.Body.Instructions,i=>i.Operand is MethodReference m && m.DeclaringType.Name=="TransferGate" && m.Name=="EndDispatch");
        Assert.DoesNotContain(issue.Body.Instructions,i=>i.Operand is MethodReference m && m.DeclaringType.Name=="TransferGate" && m.Name=="Begin");
    }

    [Fact] public void WorkbenchRangeUsesTheCurrentVanillaBuildRangeApi()
    {
        var game=Environment.GetEnvironmentVariable("GameManaged")??"/home/bunta/.local/share/Steam/steamapps/common/Valheim/valheim_Data/Managed";
        using var valheim=AssemblyDefinition.ReadAssembly(Path.Combine(game,"assembly_valheim.dll"));
        var station=Assert.Single(valheim.MainModule.Types,t=>t.Name=="CraftingStation");
        Assert.Contains(station.Methods,m=>m.Name=="GetStationBuildRange" && m.IsPublic && m.Parameters.Count==0 && m.ReturnType.FullName=="System.Single");
        Assert.Contains(station.Properties,p=>p.Name=="Instances" && p.GetMethod.IsPublic && p.GetMethod.IsStatic);
        using var plugin=AssemblyDefinition.ReadAssembly(Path.Combine(Root(),"src/BlueDepot.Plugin/bin/Release/net472/BlueDepot.dll"));
        var storage=Assert.Single(plugin.MainModule.Types,t=>t.Name=="Storage");
        var lookup=Assert.Single(storage.Methods,m=>m.Name=="CraftingChests");
        Assert.Contains(lookup.Body.Instructions,i=>i.Operand is MethodReference m && m.DeclaringType.Name=="WorkbenchReach" && m.Name=="Connected");
        var nearby=Assert.Single(storage.Methods,m=>m.Name=="Nearby");
        Assert.DoesNotContain(nearby.Body.Instructions,i=>i.Operand is MethodReference m && m.DeclaringType.Name=="WorkbenchReach");
    }

    [Fact] public void CraftingHarmonyArgumentsAndReflectedSelectionStillMatchGame()
    {
        var root=Root();
        var game=Environment.GetEnvironmentVariable("GameManaged")??"/home/bunta/.local/share/Steam/steamapps/common/Valheim/valheim_Data/Managed";
        using var plugin=AssemblyDefinition.ReadAssembly(Path.Combine(root,"src/BlueDepot.Plugin/bin/Release/net472/BlueDepot.dll"));
        using var valheim=AssemblyDefinition.ReadAssembly(Path.Combine(game,"assembly_valheim.dll"));
        var patchNames=new[]{"CraftRequirementScope","BuildRequirementScope","ChestIngredientCount","RequirementDisplayScope","CraftFromChests","PreventConcurrentCraft","BuildFromChests",
            "ResumeDeferredBuild","SupplyFireplace","SupplySmelterFuel","SupplySmelterInput","SupplyCookingFuel","SupplyCookingInput",
            "RegisterPileStorage","PileRefund","PileItemRestriction","DepotPlayerClick","DepotPlayerRelease","DepotSplitAccepted","DepotSplitCancelled","DepotOutsideDrop"};
        foreach(var name in patchNames)
        {
            var type=Assert.Single(plugin.MainModule.Types,t=>t.Name==name);
            var patch=Assert.Single(type.CustomAttributes,a=>a.AttributeType.Name=="HarmonyPatch");
            var target=(TypeReference)patch.ConstructorArguments.First(a=>a.Type.FullName=="System.Type").Value;
            var targetType=Assert.Single(valheim.MainModule.Types,t=>t.FullName==target.FullName);
            var methodName=(string)patch.ConstructorArguments.First(a=>a.Type.FullName=="System.String").Value;
            foreach(var parameter in type.Methods.Where(m=>m.Name=="Prefix" || m.Name=="Postfix").SelectMany(m=>m.Parameters))
            {
                if(parameter.Name.StartsWith("___",StringComparison.Ordinal))
                    Assert.Contains(targetType.Fields,f=>f.Name==parameter.Name.Substring(3) && f.FieldType.FullName==parameter.ParameterType.FullName.TrimEnd('&'));
                else if(!parameter.Name.StartsWith("__",StringComparison.Ordinal))
                    Assert.Contains(targetType.Methods.Where(m=>m.Name==methodName).SelectMany(m=>m.Parameters),p=>p.Name==parameter.Name && p.ParameterType.FullName==parameter.ParameterType.FullName);
            }
        }
        var gui=Assert.Single(valheim.MainModule.Types,t=>t.Name=="InventoryGui");
        foreach(var field in new[]{"m_selectedRecipe","m_selectedVariant","m_craftVariant","m_multiCrafting","m_craftRecipe","m_craftUpgradeItem"})
            Assert.Contains(gui.Fields,f=>f.Name==field);
        var pair=Assert.Single(gui.NestedTypes,t=>t.Name=="RecipeDataPair");
        Assert.Contains(pair.Properties,p=>p.Name=="Recipe");
        Assert.Contains(pair.Properties,p=>p.Name=="ItemData");
    }
    [Fact] public void DeferredActionsReflectOnlyExistingGameMembers()
    {
        var game=Environment.GetEnvironmentVariable("GameManaged")??"/home/bunta/.local/share/Steam/steamapps/common/Valheim/valheim_Data/Managed";
        using var valheim=AssemblyDefinition.ReadAssembly(Path.Combine(game,"assembly_valheim.dll"));
        foreach(var (type,name,result,args) in new[]{
            ("InventoryGui","UpdateCraftingPanel","System.Void",new[]{"System.Boolean"}),
            ("InventoryGui","ShowSplitDialog","System.Void",new[]{"ItemDrop/ItemData","Inventory"}),
            ("InventoryGui","SetupDragItem","System.Void",new[]{"ItemDrop/ItemData","Inventory","System.Int32"}),
            ("InventoryGrid","CreateItemTooltip","System.Void",new[]{"ItemDrop/ItemData","UITooltip"}),
            ("Player","UpdatePlacementGhost","System.Void",new[]{"System.Boolean"}),
            ("Humanoid","GetRightItem","ItemDrop/ItemData",Array.Empty<string>()),
            ("Smelter","GetFuel","System.Single",Array.Empty<string>()),
            ("Smelter","GetQueueSize","System.Int32",Array.Empty<string>()),
            ("CookingStation","GetFuel","System.Single",Array.Empty<string>()),
            ("CookingStation","GetFreeSlot","System.Int32",Array.Empty<string>()),
            ("CookingStation","HaveDoneItem","System.Boolean",Array.Empty<string>()),
            ("CookingStation","IsFireLit","System.Boolean",Array.Empty<string>())})
            Assert.Contains(Assert.Single(valheim.MainModule.Types,t=>t.Name==type).Methods,
                m=>m.Name==name && m.ReturnType.FullName==result && m.Parameters.Select(p=>p.ParameterType.FullName).SequenceEqual(args));
        var player=Assert.Single(valheim.MainModule.Types,t=>t.Name=="Player");
        Assert.Contains(player.Fields,f=>f.Name=="m_placementGhost" && f.FieldType.FullName=="UnityEngine.GameObject");
    }
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
