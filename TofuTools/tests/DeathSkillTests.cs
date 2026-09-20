using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using HarmonyLib;
using Mono.Cecil;
using TofuTools;
using Xunit;

// Test doubles execute the production-transformed death flow without loading Unity.
public static class Game { public static float m_skillReductionRate = 1f; }
public class Skills
{
    public sealed class Skill { public float m_level; public float m_accumulator; }
    public float m_DeathLowerFactor = .25f;
    public readonly List<Skill> Data = new List<Skill>();
    public List<Skill> GetSkillList() => Data.ToList();
    public void Clear() => Data.Clear();
    public void OnDeath() => LowerAllSkills(m_DeathLowerFactor * Game.m_skillReductionRate);
    public void LowerAllSkills(float factor)
    {
        foreach (var skill in Data) { skill.m_level *= 1f - factor; skill.m_accumulator = 0f; }
    }
}
public class Player
{
    public bool Owner = true, HardDeath = true, ResetSkills;
    public int Graves, Respawns;
    public readonly Skills Skills = new Skills();
    public void OnDeath() => DeathSkillTests.RunDeath(this);
}

public class DeathSkillTests
{
    public static readonly Action<Player> RunDeath = BuildDeath();
    static Action<Player> BuildDeath()
    {
        var method = new DynamicMethod("PatchedDeath", typeof(void), new[] { typeof(Player) });
        var il = method.GetILGenerator();
        var done = il.DefineLabel(); var ordinary = il.DefineLabel(); var respawn = il.DefineLabel();
        var code = new List<CodeInstruction>();
        void Field(string name) { code.Add(new CodeInstruction(OpCodes.Ldarg_0)); code.Add(new CodeInstruction(OpCodes.Ldfld, typeof(Player).GetField(name))); }
        void Increment(string name)
        {
            code.Add(new CodeInstruction(OpCodes.Ldarg_0)); Field(name);
            code.Add(new CodeInstruction(OpCodes.Ldc_I4_1)); code.Add(new CodeInstruction(OpCodes.Add));
            code.Add(new CodeInstruction(OpCodes.Stfld, typeof(Player).GetField(name)));
        }
        Field("Owner"); code.Add(new CodeInstruction(OpCodes.Brfalse, done)); Increment("Graves");
        Field("ResetSkills"); code.Add(new CodeInstruction(OpCodes.Brfalse, ordinary));
        Field("Skills"); code.Add(Call("Clear")); code.Add(new CodeInstruction(OpCodes.Br, respawn));
        var mark = new CodeInstruction(OpCodes.Nop); mark.labels.Add(ordinary); code.Add(mark);
        Field("HardDeath"); code.Add(new CodeInstruction(OpCodes.Brfalse, respawn));
        Field("Skills"); code.Add(Call("OnDeath"));
        mark = new CodeInstruction(OpCodes.Nop); mark.labels.Add(respawn); code.Add(mark); Increment("Respawns");
        mark = new CodeInstruction(OpCodes.Ret); mark.labels.Add(done); code.Add(mark);
        foreach (var instruction in DeathSkillPatch.Transpiler(code))
        {
            foreach (var label in instruction.labels) il.MarkLabel(label);
            switch (instruction.operand)
            {
                case System.Reflection.MethodInfo m: il.Emit(instruction.opcode, m); break;
                case System.Reflection.FieldInfo f: il.Emit(instruction.opcode, f); break;
                case Label l: il.Emit(instruction.opcode, l); break;
                case null: il.Emit(instruction.opcode); break;
                default: throw new InvalidOperationException();
            }
        }
        return (Action<Player>)method.CreateDelegate(typeof(Action<Player>));
    }
    static Player Make(float level, float progress)
    {
        var player = new Player();
        player.Skills.Data.Add(new Skills.Skill { m_level = level, m_accumulator = progress });
        return player;
    }
    [Theory]
    [InlineData(0f, 0f)][InlineData(0f, .4f)][InlineData(1f, 1f)]
    [InlineData(25f, 60f)][InlineData(42.75f, 90f)][InlineData(99f, 400f)][InlineData(100f, 0f)]
    public void DeathKeepsExactLevelAndClearsOnlyProgress(float level, float progress)
    {
        var player = Make(level, progress);
        player.OnDeath();
        var skill = Assert.Single(player.Skills.Data);
        Assert.Equal(level, skill.m_level); Assert.Equal(0f, skill.m_accumulator);
        Assert.Equal(1, player.Graves); Assert.Equal(1, player.Respawns);
    }
    [Theory][InlineData(false)][InlineData(true)]
    public void OrdinaryAndHardcoreDeathsPreserveEverySkillAcrossRepeatedDeaths(bool reset)
    {
        var player = Make(35f, 50f); player.ResetSkills = reset;
        player.Skills.Data.Add(new Skills.Skill { m_level = 78.125f, m_accumulator = 321f });
        for (int i = 0; i < 20; i++) player.OnDeath();
        Assert.Equal(new[] { 35f, 78.125f }, player.Skills.Data.Select(s => s.m_level));
        Assert.All(player.Skills.Data, s => Assert.Equal(0f, s.m_accumulator));
        Assert.Equal(20, player.Respawns);
    }
    [Fact] public void RepeatDeathGraceRetainsProgress()
    {
        var player = Make(12f, 9f); player.HardDeath = false; player.OnDeath();
        Assert.Equal(9f, player.Skills.Data[0].m_accumulator); Assert.Equal(1, player.Respawns);
    }
    [Fact] public void NonOwnerDoesNothing()
    {
        var player = Make(12f, 9f); player.Owner = false; player.OnDeath();
        Assert.Equal(9f, player.Skills.Data[0].m_accumulator); Assert.Equal(0, player.Graves);
    }
    [Theory][InlineData(0f)][InlineData(-1f)]
    public void DisabledPenaltyRetainsProgress(float factor)
    {
        var player = Make(12f, 9f); player.Skills.m_DeathLowerFactor = factor;
        player.OnDeath(); Assert.Equal(9f, player.Skills.Data[0].m_accumulator);
    }
    [Fact] public void ZeroWorldMultiplierRetainsProgress()
    {
        try { Game.m_skillReductionRate = 0f; var player = Make(12f, 9f); player.OnDeath(); Assert.Equal(9f, player.Skills.Data[0].m_accumulator); }
        finally { Game.m_skillReductionRate = 1f; }
    }
    [Fact] public void EmptySkillListStillCompletesDeath()
    {
        var player = new Player(); player.OnDeath(); Assert.Empty(player.Skills.Data); Assert.Equal(1, player.Respawns);
    }
    [Fact] public void ExplicitNonDeathSkillChangesStillWork()
    {
        var player = Make(20f, 9f); player.Skills.LowerAllSkills(.5f);
        Assert.Equal(10f, player.Skills.Data[0].m_level);
        player.Skills.Clear(); Assert.Empty(player.Skills.Data);
    }
    static CodeInstruction Call(string name) => new CodeInstruction(OpCodes.Callvirt, typeof(Skills).GetMethod(name));
    [Theory][InlineData(0, 1)][InlineData(1, 0)][InlineData(2, 1)][InlineData(1, 2)]
    public void ChangedGameLayoutIsRejected(int ordinary, int reset)
        => Assert.Throws<InvalidOperationException>(() => DeathSkillPatch.Transpiler(
            Enumerable.Range(0, ordinary).Select(_ => Call("OnDeath")).Concat(Enumerable.Range(0, reset).Select(_ => Call("Clear")))).ToArray());
    [Fact] public void TranspilerPreservesLabelsBlocksAndInput()
    {
        var input = new[] { Call("OnDeath"), Call("Clear") };
        var label = new DynamicMethod("Labels", typeof(void), Type.EmptyTypes).GetILGenerator().DefineLabel();
        input[0].labels.Add(label); input[0].blocks.Add(new ExceptionBlock(ExceptionBlockType.BeginExceptionBlock));
        var output = DeathSkillPatch.Transpiler(input).ToArray();
        Assert.Contains(label, output[0].labels); Assert.Single(output[0].blocks);
        Assert.Equal(OpCodes.Callvirt, input[0].opcode);
        Assert.Equal(OpCodes.Call, output[0].opcode);
    }
    [Fact] public void InstalledGameHasExpectedDeathGatesAndSkillStorage()
    {
        using var game = AssemblyDefinition.ReadAssembly("/home/bunta/.local/share/Steam/steamapps/common/Valheim/valheim_Data/Managed/assembly_valheim.dll");
        var player = game.MainModule.Types.Single(t => t.Name == "Player");
        var calls = player.Methods.Single(m => m.Name == "OnDeath").Body.Instructions.Select(i => i.Operand).OfType<MethodReference>().ToArray();
        Assert.Single(calls, m => m.DeclaringType.Name == "Skills" && m.Name == "OnDeath");
        Assert.Single(calls, m => m.DeclaringType.Name == "Skills" && m.Name == "Clear");
        foreach (string name in new[] { "IsOwner", "HardDeath", "CreateTombStone", "RequestRespawn" }) Assert.Contains(calls, m => m.Name == name);
        var skills = game.MainModule.Types.Single(t => t.Name == "Skills");
        Assert.Contains(skills.Methods, m => m.Name == "GetSkillList" && m.IsPublic);
        var skill = skills.NestedTypes.Single(t => t.Name == "Skill");
        foreach (string name in new[] { "m_level", "m_accumulator" }) Assert.Contains(skill.Fields, f => f.Name == name && f.IsPublic && f.FieldType.FullName == "System.Single");
    }
}
