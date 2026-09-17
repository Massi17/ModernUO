using System;
using Server.Custom;
using Server.Targeting;

namespace Server.Spells.Fifth
{
    public class MagicReflectSpell : MagerySpell, ITargetingSpell<Mobile>
    {
        private static readonly SpellInfo _info = new(
            "Magic Reflection",
            "In Jux Sanct",
            242,
            9012,
            Reagent.Garlic,
            Reagent.MandrakeRoot,
            Reagent.SpidersSilk
        );

        public MagicReflectSpell(Mobile caster, Item scroll = null) : base(caster, scroll, _info)
        {
        }

        public override SpellCircle Circle => SpellCircle.Fifth;

        public override bool TargetFirst => true;

        public override bool BlocksMovement => false;

        public override bool BlocksWeaponSwing => TargetFirstCommitted;

        public override bool ValidateTargetFirst(object target)
        {
            if (target is Mobile { Player: true })
            {
                return true;
            }

            Caster.SendMessage("You can only cast this on a player.");
            return false;
        }

        public void Target(Mobile m)
        {
            if (CheckSequence())
            {
                SpellReflect.Apply(m, TimeSpan.FromMinutes(5));

                m.FixedParticles(0x375A, 10, 15, 5037, EffectLayer.Waist);
                m.PlaySound(0x1E9);
            }
        }

        public override void OnCast()
        {
            Caster.Target = new SpellTarget<Mobile>(this, TargetFlags.None, notifyOnLos: true);
        }
    }
}
