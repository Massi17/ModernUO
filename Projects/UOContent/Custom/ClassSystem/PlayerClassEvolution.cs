using Server.Mobiles;

namespace Server.Custom.ClassSystem;

public static class PlayerClassEvolution
{
    public static bool IsEvolutionEligible(PlayerClassContext context) =>
        context != null &&
        !string.IsNullOrEmpty(context.ClassId) &&
        string.IsNullOrEmpty(context.EvolutionId) &&
        context.ExpLifetime + context.HonorLifetime >= PlayerClassSystem.EvolutionThreshold;

    public static bool TryChooseEvolution(PlayerMobile pm, string evolutionId, out string failureReason)
    {
        var context = PlayerClassSystem.GetContext(pm);
        if (context == null || string.IsNullOrEmpty(context.ClassId))
        {
            failureReason = $"{pm.Name} has no class yet.";
            return false;
        }

        if (!string.IsNullOrEmpty(context.EvolutionId))
        {
            failureReason = $"{pm.Name} already chose evolution '{context.EvolutionId}'. Use a respec to change it.";
            return false;
        }

        if (context.ExpLifetime + context.HonorLifetime < PlayerClassSystem.EvolutionThreshold)
        {
            failureReason = $"{pm.Name} has not reached the evolution threshold yet.";
            return false;
        }

        var evolution = PlayerClassSystem.GetEvolution(context.ClassId, evolutionId);
        if (evolution == null)
        {
            failureReason = $"No evolution '{evolutionId}' exists for class '{context.ClassId}'.";
            return false;
        }

        context.EvolutionId = evolutionId;
        failureReason = null;
        return true;
    }
}
