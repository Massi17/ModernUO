using System;
using Server.Custom.ClassSystem;
using Server.Mobiles;
using Server.Targeting;

namespace Server.Commands;

public static class ClassSystemCommands
{
    public static void Configure()
    {
        CommandSystem.Register("SetClass", AccessLevel.GameMaster, SetClass_OnCommand);
        CommandSystem.Register("AwardExp", AccessLevel.GameMaster, AwardExp_OnCommand);
        CommandSystem.Register("AwardHonor", AccessLevel.GameMaster, AwardHonor_OnCommand);
        CommandSystem.Register("RespecEvolution", AccessLevel.GameMaster, RespecEvolution_OnCommand);
        CommandSystem.Register("RespecClass", AccessLevel.GameMaster, RespecClass_OnCommand);
        CommandSystem.Register("ChooseEvolution", AccessLevel.Player, ChooseEvolution_OnCommand);
        CommandSystem.Register("UnlockAbility", AccessLevel.Player, UnlockAbility_OnCommand);
        CommandSystem.Register("ClassStatus", AccessLevel.Player, ClassStatus_OnCommand);
    }

    [Usage("SetClass <classId>")]
    [Description("Assigns a class to the targeted player. Fails if that player already has a class.")]
    public static void SetClass_OnCommand(CommandEventArgs e)
    {
        if (e.Length < 1)
        {
            e.Mobile.SendMessage("Usage: SetClass <classId>");
            return;
        }

        e.Mobile.Target = new ClassTargetedValue(e.GetString(0), (pm, classId) =>
        {
            e.Mobile.SendMessage(
                PlayerClassAssignment.AssignClass(pm, classId, out var failureReason)
                    ? $"{pm.Name} is now class '{classId}'."
                    : failureReason
            );
        });
    }

    [Usage("AwardExp <amount>")]
    [Description("Awards EXP to the targeted player.")]
    public static void AwardExp_OnCommand(CommandEventArgs e)
    {
        if (e.Length < 1 || !int.TryParse(e.GetString(0), out var amount))
        {
            e.Mobile.SendMessage("Usage: AwardExp <amount>");
            return;
        }

        e.Mobile.Target = new ClassTargetedValue(null, (pm, _) =>
        {
            PlayerClassCurrency.AwardExp(pm, amount);
            e.Mobile.SendMessage($"Awarded {amount} EXP to {pm.Name}.");
        });
    }

    [Usage("AwardHonor <amount>")]
    [Description("Awards HONOR to the targeted player.")]
    public static void AwardHonor_OnCommand(CommandEventArgs e)
    {
        if (e.Length < 1 || !int.TryParse(e.GetString(0), out var amount))
        {
            e.Mobile.SendMessage("Usage: AwardHonor <amount>");
            return;
        }

        e.Mobile.Target = new ClassTargetedValue(null, (pm, _) =>
        {
            PlayerClassCurrency.AwardHonor(pm, amount);
            e.Mobile.SendMessage($"Awarded {amount} HONOR to {pm.Name}.");
        });
    }

    [Usage("RespecEvolution <newEvolutionId>")]
    [Description("Respecs the targeted player's evolution, refunding a configured percentage of spent currency.")]
    public static void RespecEvolution_OnCommand(CommandEventArgs e)
    {
        if (e.Length < 1)
        {
            e.Mobile.SendMessage("Usage: RespecEvolution <newEvolutionId>");
            return;
        }

        e.Mobile.Target = new ClassTargetedValue(e.GetString(0), (pm, evolutionId) =>
        {
            e.Mobile.SendMessage(
                PlayerClassRespec.RespecEvolution(pm, evolutionId, out var failureReason)
                    ? $"{pm.Name} respecced into evolution '{evolutionId}'."
                    : failureReason
            );
        });
    }

    [Usage("RespecClass <newClassId>")]
    [Description("Respecs the targeted player's class, refunding a configured percentage of spent currency.")]
    public static void RespecClass_OnCommand(CommandEventArgs e)
    {
        if (e.Length < 1)
        {
            e.Mobile.SendMessage("Usage: RespecClass <newClassId>");
            return;
        }

        e.Mobile.Target = new ClassTargetedValue(e.GetString(0), (pm, classId) =>
        {
            e.Mobile.SendMessage(
                PlayerClassRespec.RespecClass(pm, classId, out var failureReason)
                    ? $"{pm.Name} respecced into class '{classId}'."
                    : failureReason
            );
        });
    }

    [Usage("ChooseEvolution <evolutionId>")]
    [Description("Chooses an evolution for yourself, once your class is assigned and the EXP+HONOR threshold is met.")]
    public static void ChooseEvolution_OnCommand(CommandEventArgs e)
    {
        if (e.Mobile is not PlayerMobile pm || e.Length < 1)
        {
            e.Mobile.SendMessage("Usage: ChooseEvolution <evolutionId>");
            return;
        }

        var evolutionId = e.GetString(0);
        pm.SendMessage(
            PlayerClassEvolution.TryChooseEvolution(pm, evolutionId, out var failureReason)
                ? $"You have chosen evolution '{evolutionId}'."
                : failureReason
        );
    }

    [Usage("UnlockAbility <abilityId> [paymentOptionIndex]")]
    [Description("Unlocks an ability for yourself, spending EXP/HONOR from your current evolution's pool.")]
    public static void UnlockAbility_OnCommand(CommandEventArgs e)
    {
        if (e.Mobile is not PlayerMobile pm || e.Length < 1)
        {
            e.Mobile.SendMessage("Usage: UnlockAbility <abilityId> [paymentOptionIndex]");
            return;
        }

        var abilityId = e.GetString(0);
        int? optionIndex = e.Length >= 2 && int.TryParse(e.GetString(1), out var parsed) ? parsed : null;

        pm.SendMessage(
            PlayerClassAbilities.TryUnlockAbility(pm, abilityId, optionIndex, out var failureReason)
                ? $"You have unlocked '{abilityId}'."
                : failureReason
        );
    }

    [Usage("ClassStatus")]
    [Description("Shows your current class, evolution, currency balances, and unlocked abilities.")]
    public static void ClassStatus_OnCommand(CommandEventArgs e)
    {
        if (e.Mobile is not PlayerMobile pm)
        {
            return;
        }

        var context = PlayerClassSystem.GetContext(pm);
        if (context == null || string.IsNullOrEmpty(context.ClassId))
        {
            pm.SendMessage("You have no class yet.");
            return;
        }

        pm.SendMessage($"Class: {context.ClassId} | Evolution: {context.EvolutionId ?? "(none yet)"}");
        pm.SendMessage(
            $"EXP: {context.ExpBalance} (lifetime {context.ExpLifetime}) | " +
            $"HONOR: {context.HonorBalance} (lifetime {context.HonorLifetime})"
        );
        pm.SendMessage(
            $"Unlocked abilities: {(context.UnlockedAbilityIds.Count == 0 ? "(none)" : string.Join(", ", context.UnlockedAbilityIds))}"
        );
    }

    private class ClassTargetedValue : Target
    {
        private readonly string _value;
        private readonly Action<PlayerMobile, string> _onTarget;

        public ClassTargetedValue(string value, Action<PlayerMobile, string> onTarget) : base(-1, false, TargetFlags.None)
        {
            _value = value;
            _onTarget = onTarget;
        }

        protected override void OnTarget(Mobile from, object targeted)
        {
            if (targeted is not PlayerMobile pm)
            {
                from.SendMessage("That is not a player.");
                return;
            }

            _onTarget(pm, _value);
        }
    }
}
