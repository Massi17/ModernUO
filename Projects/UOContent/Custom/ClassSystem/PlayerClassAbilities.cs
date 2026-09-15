using System.Linq;
using Server.Mobiles;

namespace Server.Custom.ClassSystem;

public static class PlayerClassAbilities
{
    public static bool CanUnlock(PlayerClassContext context, EvolutionDefinitionData evolution, AbilityDefinitionData ability)
    {
        if (context.UnlockedAbilityIds.Contains(ability.Id))
        {
            return false;
        }

        return ability.Category switch
        {
            AbilityCategory.Passive => true,
            AbilityCategory.Pvp or AbilityCategory.Pve => evolution.Abilities
                .Where(a => a.Category == AbilityCategory.Passive)
                .All(a => context.UnlockedAbilityIds.Contains(a.Id)),
            AbilityCategory.Ultimate => evolution.Abilities
                .Where(a => a.Category != AbilityCategory.Ultimate)
                .All(a => context.UnlockedAbilityIds.Contains(a.Id)),
            _ => false
        };
    }

    public static bool TryUnlockAbility(PlayerMobile pm, string abilityId, int? paymentOptionIndex, out string failureReason)
    {
        var context = PlayerClassSystem.GetContext(pm);
        if (context == null || string.IsNullOrEmpty(context.EvolutionId))
        {
            failureReason = $"{pm.Name} has not chosen an evolution yet.";
            return false;
        }

        var evolution = PlayerClassSystem.GetEvolution(context.ClassId, context.EvolutionId);
        var ability = evolution?.Abilities.Find(a => a.Id == abilityId);
        if (ability == null)
        {
            failureReason = $"No ability '{abilityId}' exists for evolution '{context.EvolutionId}'.";
            return false;
        }

        if (!CanUnlock(context, evolution, ability))
        {
            failureReason = $"{pm.Name} cannot unlock '{abilityId}' yet — it is locked until earlier abilities are unlocked (or it is already unlocked).";
            return false;
        }

        AbilityCostData option;
        if (paymentOptionIndex.HasValue)
        {
            if (paymentOptionIndex.Value < 0 || paymentOptionIndex.Value >= ability.PaymentOptions.Count)
            {
                failureReason = $"Ability '{abilityId}' has no payment option {paymentOptionIndex.Value}.";
                return false;
            }

            option = ability.PaymentOptions[paymentOptionIndex.Value];
            if (!option.IsAffordable(context.ExpBalance, context.HonorBalance))
            {
                failureReason = $"{pm.Name} cannot afford that payment option for '{abilityId}'.";
                return false;
            }
        }
        else
        {
            option = ability.PaymentOptions.Find(o => o.IsAffordable(context.ExpBalance, context.HonorBalance));
            if (option == null)
            {
                failureReason = $"{pm.Name} cannot afford '{abilityId}' with either currency.";
                return false;
            }
        }

        context.ExpBalance -= option.Exp;
        context.HonorBalance -= option.Honor;
        context.UnlockedAbilityIds.Add(abilityId);

        failureReason = null;
        return true;
    }
}
