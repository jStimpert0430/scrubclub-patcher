using System;
using System.IO;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Xunit;
namespace RoadLights.Tests;
public class GameContractTests
{
    [Fact] public void RecipeIdentityDoesNotDependOnActiveHierarchy()
    {
        using var plugin=AssemblyDefinition.ReadAssembly(Path.Combine(Root(),"src/RoadLights.Plugin/bin/Release/net472/RoadLights.dll"));
        var target=plugin.MainModule.Types.Single(t=>t.Name=="Plugin").Methods.Single(m=>m.Name=="Target");
        var instructions=target.Body.Instructions;
        Assert.Contains(instructions,i=>i.OpCode==OpCodes.Isinst&&i.Operand is TypeReference t&&t.Name=="Piece");
        var searches=instructions.Where(i=>i.Operand is MethodReference m&&m.Name=="GetComponentInParent").ToArray();
        Assert.Single(searches);
        Assert.Equal(OpCodes.Ldc_I4_1,searches[0].Previous.OpCode);
        Assert.Equal("System.Boolean",((MethodReference)searches[0].Operand).Parameters.Single().ParameterType.FullName);
    }
    [Fact] public void BuildMenuAndPlacementConfigureRecipesWithoutBypassingVanillaChecks()
    {
        using var plugin=AssemblyDefinition.ReadAssembly(Path.Combine(Root(),"src/RoadLights.Plugin/bin/Release/net472/RoadLights.dll"));
        foreach(var name in new[]{"ConfigureBuildMenuLights","ConfigureLightRequirement"})
        {
            var prefix=plugin.MainModule.Types.Single(t=>t.Name==name).Methods.Single(m=>m.Name=="Prefix");
            Assert.Equal("System.Void",prefix.ReturnType.FullName);
            Assert.DoesNotContain(prefix.Parameters,p=>p.Name=="__result");
            Assert.Contains(prefix.Body.Instructions,i=>i.Operand is MethodReference m&&m.Name=="ApplyPiece");
        }
        var requirement=plugin.MainModule.Types.Single(t=>t.Name=="ConfigureLightRequirement");
        var hook=requirement.CustomAttributes.Single(a=>a.AttributeType.Name=="HarmonyPatch");
        var overload=(CustomAttributeArgument[])hook.ConstructorArguments.Last().Value;
        Assert.Equal(new[]{"Piece","Player/RequirementMode"},overload.Select(a=>((TypeReference)a.Value).FullName));
    }
    static string Root()
    {
        var dir=new DirectoryInfo(AppContext.BaseDirectory);
        while(dir!=null){if(File.Exists(Path.Combine(dir.FullName,"src/RoadLights.Plugin/RoadLights.Plugin.csproj")))return dir.FullName;dir=dir.Parent;}
        throw new DirectoryNotFoundException();
    }
    [Fact] public void HooksAndFieldsMatchInstalledGame()
    {
        var game=Environment.GetEnvironmentVariable("GameManaged")??"/home/bunta/.local/share/Steam/steamapps/common/Valheim/valheim_Data/Managed";
        using var engine=AssemblyDefinition.ReadAssembly(Path.Combine(game,"assembly_valheim.dll"));
        using var plugin=AssemblyDefinition.ReadAssembly(Path.Combine(Root(),"src/RoadLights.Plugin/bin/Release/net472/RoadLights.dll"));
        foreach(var type in plugin.MainModule.Types)
        foreach(var attr in type.CustomAttributes.Where(a=>a.AttributeType.Name=="HarmonyPatch"))
        {
            var target=(TypeReference)attr.ConstructorArguments.First(a=>a.Type.FullName=="System.Type").Value;
            string method=(string)attr.ConstructorArguments.First(a=>a.Type.FullName=="System.String").Value;
            var targetType=engine.MainModule.Types.Single(t=>t.FullName==target.FullName);
            Assert.Contains(targetType.Methods,m=>m.Name==method);
            foreach(var param in type.Methods.Where(m=>m.Name=="Prefix"||m.Name=="Postfix").SelectMany(m=>m.Parameters).Where(p=>!p.Name.StartsWith("__")))
                Assert.Contains(targetType.Methods.Where(m=>m.Name==method).SelectMany(m=>m.Parameters),p=>p.Name==param.Name&&p.ParameterType.FullName==param.ParameterType.FullName);
        }
        var fire=engine.MainModule.Types.Single(t=>t.Name=="Fireplace");
        Assert.Contains(engine.MainModule.Types.Single(t=>t.Name=="Humanoid").Fields,f=>f.Name=="m_rightItem");
        Assert.Contains(engine.MainModule.Types.Single(t=>t.Name=="BuildUi").Fields,f=>f.Name=="m_pieceView"&&f.FieldType.Name=="RectTransform");
        var placement=engine.MainModule.Types.Single(t=>t.Name=="Player").Methods.Single(m=>m.Name=="PlacePiece");
        Assert.DoesNotContain(placement.Body.Instructions,i=>i.Operand is MethodReference m&&m.Name=="ConsumeResources");
        Assert.Contains(placement.Body.Instructions,i=>i.Operand is MethodReference m&&m.Name=="SetCreator");
        Assert.Contains(fire.Methods,m=>m.Name=="RPC_ToggleOn"&&m.Parameters.Count==1);
        Assert.Contains(fire.Methods.Single(m=>m.Name=="IsBurning").Body.Instructions,i=>i.Operand is FieldReference f&&f.Name=="m_infiniteFuel");
    }
    [Fact] public void OnlyStationRequirementAndFuelFlagsAreMutated()
    {
        using var plugin=AssemblyDefinition.ReadAssembly(Path.Combine(Root(),"src/RoadLights.Plugin/bin/Release/net472/RoadLights.dll"));
        var instructions=plugin.MainModule.Types.SelectMany(t=>t.Methods).Where(m=>m.HasBody).SelectMany(m=>m.Body.Instructions).ToArray();
        var writes=instructions.Where(i=>i.OpCode==OpCodes.Stfld).Select(i=>i.Operand).OfType<FieldReference>().Where(f=>f.DeclaringType.Name=="Piece"||f.DeclaringType.Name=="Fireplace").Select(f=>f.Name).ToArray();
        Assert.Equal(new[]{"m_canRefill","m_craftingStation","m_infiniteFuel"},writes.OrderBy(x=>x));
        Assert.DoesNotContain(instructions,i=>i.Operand is MethodReference m &&
            (m.DeclaringType.Name=="Inventory" || m.Name=="SetGlobalKey"));
        var zdoWrites=plugin.MainModule.Types.SelectMany(t=>t.Methods).Where(m=>m.HasBody)
            .Where(m=>m.Body.Instructions.Any(i=>i.Operand is MethodReference call&&call.DeclaringType.Name=="ZDO"&&call.Name=="Set")).ToArray();
        Assert.Single(zdoWrites);
        Assert.Equal("MarkFreeRoadLight",zdoWrites[0].DeclaringType.Name);
        Assert.Contains(zdoWrites[0].Body.Instructions,i=>i.Operand is FieldReference f&&f.Name=="FreeKey");
        Assert.DoesNotContain(instructions,i=>i.OpCode==OpCodes.Stfld && i.Operand is FieldReference f &&
            new[]{"m_resources","m_amount","m_noPlacementCost","m_startFuel","m_maxFuel"}.Contains(f.Name));
    }
}
