using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using HarmonyLib;

namespace TofuTools;

// Intercept only the two skill-penalty calls in player death. Keep vanilla
// ownership, repeat-death protection, graves, respawn and notifications intact.
[HarmonyPatch(typeof(Player), nameof(Player.OnDeath))]
public static class DeathSkillPatch
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var code = instructions.Select(i => new CodeInstruction(i)).ToArray();
        var ordinary = typeof(Skills).GetMethod(nameof(Skills.OnDeath));
        var reset = typeof(Skills).GetMethod(nameof(Skills.Clear));
        if (code.Count(i => i.Calls(ordinary)) != 1 || code.Count(i => i.Calls(reset)) != 1)
            throw new InvalidOperationException("TofuTools: unexpected Player.OnDeath skill-penalty layout. Refusing a partial patch.");

        foreach (var instruction in code)
        {
            if (instruction.Calls(ordinary) || instruction.Calls(reset))
            {
                bool isReset = instruction.Calls(reset);
                instruction.opcode = OpCodes.Call;
                instruction.operand = typeof(DeathSkillPatch).GetMethod(
                    isReset ? nameof(ResetDeath) : nameof(OrdinaryDeath));
            }
        }
        return code;
    }

    public static void OrdinaryDeath(Skills skills)
    {
        // Respect a world with death penalties disabled. Do not impose a new loss.
        if (skills.m_DeathLowerFactor * Game.m_skillReductionRate > 0f)
            ResetDeath(skills);
    }

    public static void ResetDeath(Skills skills)
    {
        foreach (var skill in skills.GetSkillList())
            skill.m_accumulator = 0f;
        // Never write m_level: preserve fractional levels from old vanilla deaths too.
        // No vanilla "skills lowered" notification, because no levels were lowered.
    }
}
