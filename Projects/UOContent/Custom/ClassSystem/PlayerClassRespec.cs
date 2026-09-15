using Server.Mobiles;

namespace Server.Custom.ClassSystem;

public static class PlayerClassRespec
{
    private static void ApplyRefund(PlayerClassContext context, EvolutionDefinitionData evolution, int refundPercent)
    {
        var totalExp = 0;
        var totalHonor = 0;

        foreach (var abilityId in context.UnlockedAbilityIds)
        {
            var ability = evolution?.Abilities.Find(a => a.Id == abilityId);
            if (ability == null || ability.PaymentOptions.Count == 0)
            {
                continue;
            }

            var canonicalCost = ability.PaymentOptions[0];
            totalExp += canonicalCost.Exp;
            totalHonor += canonicalCost.Honor;
        }

        context.ExpBalance += totalExp * refundPercent / 100;
        context.HonorBalance += totalHonor * refundPercent / 100;
    }

    public static bool RespecEvolution(PlayerMobile pm, string newEvolutionId, out string failureReason)
    {
        var context = PlayerClassSystem.GetContext(pm);
        if (context == null || string.IsNullOrEmpty(context.EvolutionId))
        {
            failureReason = $"{pm.Name} has no evolution to respec.";
            return false;
        }

        var newEvolution = PlayerClassSystem.GetEvolution(context.ClassId, newEvolutionId);
        if (newEvolution == null)
        {
            failureReason = $"No evolution '{newEvolutionId}' exists for class '{context.ClassId}'.";
            return false;
        }

        var currentEvolution = PlayerClassSystem.GetEvolution(context.ClassId, context.EvolutionId);
        ApplyRefund(context, currentEvolution, PlayerClassSystem.EvolutionRespecRefundPercent);

        context.UnlockedAbilityIds.Clear();
        context.EvolutionId = newEvolutionId;

        failureReason = null;
        return true;
    }

    public static bool RespecClass(PlayerMobile pm, string newClassId, out string failureReason)
    {
        var context = PlayerClassSystem.GetContext(pm);
        if (context == null || string.IsNullOrEmpty(context.ClassId))
        {
            failureReason = $"{pm.Name} has no class to respec.";
            return false;
        }

        var newClassDef = PlayerClassSystem.GetClass(newClassId);
        if (newClassDef == null)
        {
            failureReason = $"No class '{newClassId}' exists.";
            return false;
        }

        if (!string.IsNullOrEmpty(context.EvolutionId))
        {
            var currentEvolution = PlayerClassSystem.GetEvolution(context.ClassId, context.EvolutionId);
            ApplyRefund(context, currentEvolution, PlayerClassSystem.ClassRespecRefundPercent);
        }

        context.UnlockedAbilityIds.Clear();
        context.EvolutionId = null;
        context.ClassId = newClassId;

        PlayerClassAssignment.ApplySkillCaps(pm, newClassDef);

        failureReason = null;
        return true;
    }
}
