using Server.Mobiles;

namespace Server.Custom.ClassSystem;

public static class PlayerClassCurrency
{
    public static void AwardExp(PlayerMobile pm, int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        var context = PlayerClassSystem.GetOrCreateContext(pm);
        context.ExpBalance += amount;
        context.ExpLifetime += amount;
    }

    public static void AwardHonor(PlayerMobile pm, int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        var context = PlayerClassSystem.GetOrCreateContext(pm);
        context.HonorBalance += amount;
        context.HonorLifetime += amount;
    }
}
