namespace Server.Commands;

public static class PvpDamageTestCommand
{
    public static void Configure()
    {
        CommandSystem.Register("PvpDamageTest", AccessLevel.GameMaster, PvpDamageTest_OnCommand);
    }

    [Usage("PvpDamageTest")]
    [Description("Toggles a per-hit damage report (total/HP/shield split) for the caster: every melee, ranged, or spell hit the caster lands prints who it hit and how the damage split between real HP and any magic shield.")]
    public static void PvpDamageTest_OnCommand(CommandEventArgs arg)
    {
        var enabled = Custom.PvpDamageTest.Toggle(arg.Mobile);
        arg.Mobile.SendMessage(enabled ? "PvP damage test ENABLED." : "PvP damage test DISABLED.");
    }
}
