using Server.Mobiles;

namespace Server.Custom.ClassSystem;

public static class PlayerClassRespec
{
    private static void ApplyRefund(PlayerClassContext context, int refundPercent)
    {
        var totalExp = 0;
        var totalHonor = 0;

        for (var i = 0; i < context.UnlockedAbilityIds.Count; i++)
        {
            totalExp += context.UnlockedAbilityExpCosts[i];
            totalHonor += context.UnlockedAbilityHonorCosts[i];
        }

        context.ExpBalance += totalExp * refundPercent / 100;
        context.HonorBalance += totalHonor * refundPercent / 100;
    }

    private static void ClearUnlockedAbilities(PlayerClassContext context)
    {
        context.UnlockedAbilityIds.Clear();
        context.UnlockedAbilityExpCosts.Clear();
        context.UnlockedAbilityHonorCosts.Clear();
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

        ApplyRefund(context, PlayerClassSystem.EvolutionRespecRefundPercent);
        ClearUnlockedAbilities(context);
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
            ApplyRefund(context, PlayerClassSystem.ClassRespecRefundPercent);
        }

        ClearUnlockedAbilities(context);
        context.EvolutionId = null;
        context.ClassId = newClassId;

        PlayerClassAssignment.ApplySkillCaps(pm, newClassDef);
        PlayerClassEquipment.UnequipForbiddenItems(pm);

        failureReason = null;
        return true;
    }
}
