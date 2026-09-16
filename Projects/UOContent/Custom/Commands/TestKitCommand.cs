using Server.Items;
using Server.Targeting;

namespace Server.Commands;

public static class TestKitCommand
{
    private const int StatValue = 125;
    private const double SkillValue = 100.0;
    private const int ReagentAmount = 600;
    private const int RegenHitsBonus = 20;

    public static void Configure()
    {
        CommandSystem.Register("TestKit", AccessLevel.GameMaster, TestKit_OnCommand);
    }

    [Usage("TestKit")]
    [Description("Applies a test-friendly loadout to the targeted player: raised stats, maxed skills, a bag of reagents, a full spellbook, and a high-regen ring. Does not change AccessLevel.")]
    public static void TestKit_OnCommand(CommandEventArgs arg)
    {
        arg.Mobile.Target = new TestKitTarget();
    }

    private class TestKitTarget : Target
    {
        public TestKitTarget() : base(-1, false, TargetFlags.None)
        {
        }

        protected override void OnTarget(Mobile from, object targeted)
        {
            if (targeted is not Mobile target)
            {
                from.SendMessage("That is not a mobile.");
                return;
            }

            target.RawStr = StatValue;
            target.RawDex = StatValue;
            target.RawInt = StatValue;

            var skills = target.Skills;
            for (var i = 0; i < skills.Length; i++)
            {
                skills[i].Base = SkillValue;
            }

            var pack = target.Backpack;
            pack?.DropItem(new BagOfReagents(ReagentAmount));
            pack?.DropItem(new Spellbook(ulong.MaxValue));

            var ring = new GoldRing();
            ring.Attributes.RegenHits = RegenHitsBonus;

            if (!target.EquipItem(ring))
            {
                pack?.DropItem(ring);
            }

            from.SendMessage($"Test kit applied to {target.Name}.");
            target.SendMessage("You have been outfitted with a test kit.");

            CommandLogging.LogChangeProperty(from, target, "TestKit", "applied");
        }
    }
}
