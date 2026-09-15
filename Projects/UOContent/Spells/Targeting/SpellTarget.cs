using Server.Targeting;

namespace Server.Spells;

public class SpellTarget<T> : Target, ISpellTarget<T> where T : class, IPoint3D
{
    private static readonly bool _canTargetStatic = typeof(T).IsAssignableFrom(typeof(StaticTarget));
    private static readonly bool _canTargetMobile = typeof(T).IsAssignableFrom(typeof(Mobile));
    private static readonly bool _canTargetItem = typeof(T).IsAssignableFrom(typeof(Item));

    private readonly bool _retryOnLos;
    protected readonly ITargetingSpell<T> _spell;

    public SpellTarget(
        ITargetingSpell<T> spell,
        TargetFlags flags,
        bool retryOnLos = false
    ) : this(spell, false, flags, retryOnLos)
    {
    }

    public SpellTarget(
        ITargetingSpell<T> spell,
        bool allowGround = false,
        TargetFlags flags = TargetFlags.None,
        bool retryOnLos = false
    ) : base(spell.TargetRange, allowGround, flags)
    {
        _spell = spell;
        _retryOnLos = retryOnLos;
    }

    public ITargetingSpell<T> Spell => _spell;

    protected override bool CanTarget(Mobile from, StaticTarget staticTarget, ref Point3D loc, ref Map map)
        => base.CanTarget(from, staticTarget, ref loc, ref map) && _canTargetStatic;

    protected override bool CanTarget(Mobile from, Mobile mobile, ref Point3D loc, ref Map map) =>
        base.CanTarget(from, mobile, ref loc, ref map) && _canTargetMobile;

    protected override bool CanTarget(Mobile from, Item item, ref Point3D loc, ref Map map) =>
        base.CanTarget(from, item, ref loc, ref map) && _canTargetItem;

    protected override void OnCantSeeTarget(Mobile from, object o)
    {
        from.SendLocalizedMessage(500237); // Target can not be seen.
    }

    protected override void OnTarget(Mobile from, object o)
    {
        if (_spell is Spell { UsesDeferredCast: true } spell)
        {
            if (from.Spell != spell)
            {
                return; // this spell was disturbed/cancelled after the cursor appeared; ignore a stale click
            }

            if (!spell.ValidateTargetFirst(o))
            {
                return; // message already sent by ValidateTargetFirst; phase 1 stays free
            }

            spell.BeginTargetFirstDelay(() => ResolveTargetFirst(spell, from, o));
            return;
        }

        _spell.Target(o as T);
    }

    /// <summary>
    /// Runs when a TargetFirst spell's post-click cast delay finishes. Range, line of
    /// sight, and the spell's own validity check are re-checked - the target may have
    /// moved, broken LOS, or died during the delay - and any failure here still charges
    /// the cost, unlike a phase-1 rejection. On success, resolution goes through the
    /// spell's own Target(), exactly like every other spell - that already deducts
    /// mana/reagents via CheckSequence(), so ConsumeCastingResources() must NOT also be
    /// called on this path (it would double-charge). This method also owns the spell's
    /// teardown: OnTargetFinish() below deliberately skips FinishSequence() once the cast
    /// is committed, so every exit path here must run it.
    /// </summary>
    private void ResolveTargetFirst(Spell spell, Mobile from, object o)
    {
        try
        {
            // Resolution has begun - the cast is no longer "pending" for Disturb()'s or
            // OnTargetFinish()'s purposes.
            spell.EndTargetFirstCommitment();

            if (o is IPoint2D p && Range >= 0 && !from.InRange(p, Range))
            {
                spell.ConsumeCastingResources();
                from.SendLocalizedMessage(500446); // That is too far away.
                return;
            }

            if (CheckLOS && !from.InLOS(o))
            {
                spell.ConsumeCastingResources();
                from.SendLocalizedMessage(500237); // Target can not be seen.
                return;
            }

            if (!spell.ValidateTargetFirst(o))
            {
                spell.ConsumeCastingResources(); // message already sent by ValidateTargetFirst
                return;
            }

            _spell.Target(o as T);
        }
        finally
        {
            spell.FinishSequence();
        }
    }

    protected override void OnTargetOutOfLOS(Mobile from, object o)
    {
        if (!_retryOnLos)
        {
            return;
        }

        from.SendLocalizedMessage(501943); // Target cannot be seen. Try again.
        from.Target = new SpellTarget<T>(_spell, AllowGround, Flags, true);
        from.Target.BeginTimeout(from, TimeoutTime - Core.TickCount);
    }

    protected override void OnTargetFinish(Mobile from)
    {
        if (_spell is Spell { TargetFirstCommitted: true })
        {
            // Resolution is deferred to ResolveTargetFirst, which runs later when
            // the phase-2 cast delay finishes - calling FinishSequence() here would
            // tear the spell down before that can happen (Target.Invoke calls this
            // unconditionally, synchronously, right after OnTarget returns).
            return;
        }

        _spell?.FinishSequence();
    }
}
