#nullable disable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
namespace ScrubclubAchievements;
[HarmonyPatch]
internal static class AchievementPatch
{
    static MethodBase TargetMethod()=>AccessTools.Method("Achievements:IsCheatedAtAll")??throw new MissingMethodException("Achievements.IsCheatedAtAll was not found; achievement support was not modified.");
    internal static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var copy=instructions.Select(i=>new CodeInstruction(i)).ToList();
        var matches=copy.Where(i=>i.opcode==OpCodes.Ldsfld && i.operand is FieldInfo field &&
            field.DeclaringType?.FullName=="Game" && field.Name=="isModded" && field.FieldType==typeof(bool)).ToArray();
        if(matches.Length!=1)throw new InvalidOperationException("Expected exactly one Game.isModded check in achievement eligibility; refusing to alter an unknown game version.");
        // Replace this one value, preserving the original branches, exception
        // blocks, labels, cache, profile/world/item checks and all their side effects.
        matches[0].opcode=OpCodes.Ldc_I4_0;matches[0].operand=null;
        return copy;
    }
    internal static void InvalidateCache()
    {
        var field=AccessTools.Field(AccessTools.TypeByName("Achievements"),"m_cheatCheckFrame");
        if(field!=null && field.FieldType==typeof(int))field.SetValue(null,-1);
    }
}
