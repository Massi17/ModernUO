using System;
using Server.Targeting;

namespace Server.Commands;

public static class MagicShieldCommand
{
    public static void Configure()
    {
        CommandSystem.Register("MagicShield", AccessLevel.GameMaster, MagicShield_OnCommand);
    }

    [Usage("MagicShield <points> [durationSeconds]")]
    [Description("Applies a magic-shield damage-absorb pool to the targeted mobile, for testing. Omit durationSeconds for a shield that only expires when fully absorbed.")]
    public static void MagicShield_OnCommand(CommandEventArgs arg)
    {
        if (arg.Length < 1)
        {
            arg.Mobile.SendMessage("Format: MagicShield <points> [durationSeconds]");
            return;
        }

        var points = arg.GetInt32(0);

        if (points <= 0)
        {
            arg.Mobile.SendMessage("Points must be a positive number.");
            return;
        }

        TimeSpan? duration = arg.Length >= 2 ? TimeSpan.FromSeconds(arg.GetInt32(1)) : null;

        arg.Mobile.Target = new MagicShieldTarget(points, duration);
    }

    private class MagicShieldTarget : Target
    {
        private readonly int _points;
        private readonly TimeSpan? _duration;

        public MagicShieldTarget(int points, TimeSpan? duration) : base(-1, false, TargetFlags.None)
        {
            _points = points;
            _duration = duration;
        }

        protected override void OnTarget(Mobile from, object targeted)
        {
            if (targeted is not Mobile target)
            {
                from.SendMessage("That is not a mobile.");
                return;
            }

            Custom.MagicShield.Apply(target, _points, _duration);

            var durationText = _duration is { } d ? $" for {d.TotalSeconds:0}s" : "";
            from.SendMessage($"Applied a {_points}-point magic shield to {target.Name}{durationText}.");
            target.SendMessage($"You have been shielded ({_points} points).");

            CommandLogging.LogChangeProperty(from, target, "MagicShield", $"{_points}{durationText}");
        }
    }
}
