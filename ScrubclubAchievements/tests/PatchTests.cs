#nullable disable
using System;
using System.Linq;
using System.Reflection.Emit;
using HarmonyLib;
using Mono.Cecil;
using ScrubclubAchievements;
using Xunit;
// Fixture deliberately has the same field identity as the game's class.
public static class Game {public static bool isModded;}
public class PatchTests
{
    static CodeInstruction Modded()=>new CodeInstruction(OpCodes.Ldsfld,typeof(Game).GetField(nameof(Game.isModded)));
    [Fact] public void OnlyModdedInputIsRemovedAndItsGlobalValueStaysTrue()
    {
        Game.isModded=true;
        var original=new[]{new CodeInstruction(OpCodes.Ldarg_0),new CodeInstruction(OpCodes.Ldarg_1),new CodeInstruction(OpCodes.Or),new CodeInstruction(OpCodes.Ldarg_2),new CodeInstruction(OpCodes.Or),Modded(),new CodeInstruction(OpCodes.Or),new CodeInstruction(OpCodes.Ret)};
        var patched=AchievementPatch.Transpiler(original).ToArray();
        var method=new DynamicMethod("Eligibility",typeof(bool),new[]{typeof(bool),typeof(bool),typeof(bool)});
        var il=method.GetILGenerator();foreach(var i in patched)il.Emit(i.opcode);
        var run=(Func<bool,bool,bool,bool>)method.CreateDelegate(typeof(Func<bool,bool,bool,bool>));
        for(int flags=0;flags<8;flags++)Assert.Equal(flags!=0,run((flags&1)!=0,(flags&2)!=0,(flags&4)!=0));
        Assert.True(Game.isModded);Assert.Equal(OpCodes.Ldsfld,original[5].opcode);
    }
    [Theory][InlineData(0)][InlineData(2)]
    public void UnknownOrAmbiguousLayoutFailsClosed(int checks)
        =>Assert.Throws<InvalidOperationException>(()=>AchievementPatch.Transpiler(Enumerable.Range(0,checks).Select(_=>Modded())).ToArray());
    [Fact] public void BranchLabelsAndExceptionMetadataArePreserved()
    {
        var instruction=Modded();var label=new DynamicMethod("Labels",typeof(void),Type.EmptyTypes).GetILGenerator().DefineLabel();
        instruction.labels.Add(label);instruction.blocks.Add(new ExceptionBlock(ExceptionBlockType.BeginExceptionBlock));
        var result=Assert.Single(AchievementPatch.Transpiler(new[]{instruction}));
        Assert.Contains(label,result.labels);Assert.Single(result.blocks);Assert.Null(result.operand);
    }
    [Fact] public void CurrentValheimGateMatchesAndRetainsNativeRestrictions()
    {
        using var assembly=AssemblyDefinition.ReadAssembly("/home/bunta/.local/share/Steam/steamapps/common/Valheim/valheim_Data/Managed/assembly_valheim.dll");
        var type=assembly.MainModule.Types.Single(t=>t.Name=="Achievements");var method=type.Methods.Single(m=>m.Name=="IsCheatedAtAll");
        var il=method.Body.Instructions;
        Assert.Single(il,i=>i.OpCode.Code==Mono.Cecil.Cil.Code.Ldsfld && i.Operand is FieldReference f && f.DeclaringType.Name=="Game" && f.Name=="isModded");
        Assert.Contains(il,i=>i.Operand is FieldReference f && f.Name=="m_usedCheats");
        Assert.Contains(il,i=>i.Operand is MethodReference m && m.Name=="IsWorldCheated");
        Assert.Contains(il,i=>i.Operand is MethodReference m && m.Name=="AnyCheatedItem");
        Assert.Contains(type.Fields,f=>f.Name=="m_cheatCheckFrame"&&f.FieldType.FullName=="System.Int32");
    }
}
