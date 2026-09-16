using System;
using ModernUO.Serialization;
using Server.Items;

namespace Server.Mobiles;

/// <summary>
/// Shared base for stationary combat test dummies: never moves, huge HP pool with fast
/// natural regen, always deals exactly 1 damage per hit, only fights back when attacked
/// (FightMode.Aggressor), and stands down to peace 15 seconds after it last landed a hit.
/// </summary>
[SerializationGenerator(0, false)]
public abstract partial class BaseTestDummy : BaseCreature
{
    private const int DummyHits = 10000;
    private const int DummyRegenHits = 30;
    private const int PeaceTimeoutMs = 15000;

    private long _lastDamageDealt;
    private TimerExecutionToken _peaceCheckToken;

    protected BaseTestDummy(AIType aiType) : base(aiType, FightMode.Aggressor)
    {
        Name = "a test dummy";
        SpeechHue = Utility.RandomDyedHue();
        Hue = Race.Human.RandomSkinHue();
        Body = Utility.RandomBool() ? 0x190 : 0x191;

        SetStr(100);
        SetDex(80);
        SetInt(30);

        SetHits(DummyHits);
        SetDamage(1);

        SetSkill(SkillName.Tactics, 100.0);
        SetSkill(SkillName.Anatomy, 100.0);
        SetSkill(SkillName.MagicResist, 120.0);

        Utility.AssignRandomHair(this);

        var ring = new GoldRing();
        ring.Attributes.RegenHits = DummyRegenHits;
        EquipItem(ring);

        Karma = 0;
        Fame = 0;

        _lastDamageDealt = Core.TickCount;
        StartPeaceCheckTimer();
    }

    // Never moves, regardless of why (AI chase, being pushed, anything else).
    public override bool Move(Direction d) => false;

    // Covers both melee and ranged hits - BaseRanged.OnHit chains into the same
    // BaseWeapon.OnHit call site that invokes this hook.
    public override void AlterMeleeDamageTo(Mobile to, ref int damage) => damage = 1;

    public override void OnGaveMeleeAttack(Mobile defender, int damage)
    {
        _lastDamageDealt = Core.TickCount;
        base.OnGaveMeleeAttack(defender, damage);
    }

    private void StartPeaceCheckTimer()
    {
        Timer.StartTimer(TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(2), CheckPeace, out _peaceCheckToken);
    }

    private void CheckPeace()
    {
        if (Deleted)
        {
            _peaceCheckToken.Cancel();
            return;
        }

        if (Combatant != null && Core.TickCount - (_lastDamageDealt + PeaceTimeoutMs) >= 0)
        {
            Combatant = null;
            Warmode = false;
        }

        MaintainAmmo();
    }

    // Overridden by ranged variants that need to keep their ammo stocked.
    protected virtual void MaintainAmmo()
    {
    }

    [AfterDeserialization]
    private void AfterDeserialization()
    {
        _lastDamageDealt = Core.TickCount;
        StartPeaceCheckTimer();
    }

    public override void OnDelete()
    {
        _peaceCheckToken.Cancel();
        base.OnDelete();
    }
}

[SerializationGenerator(0, false)]
public partial class TestDummyMelee : BaseTestDummy
{
    [Constructible]
    public TestDummyMelee() : base(AIType.AI_Melee)
    {
        Name = "a test dummy (melee)";

        SetSkill(SkillName.Swords, 100.0);

        AddItem(new Katana());
    }
}

[SerializationGenerator(0, false)]
public partial class TestDummyArcher : BaseTestDummy
{
    private const int ArrowRestockThreshold = 50;
    private const int ArrowRestockAmount = 500;

    [Constructible]
    public TestDummyArcher() : base(AIType.AI_Archer)
    {
        Name = "a test dummy (archer)";

        SetSkill(SkillName.Archery, 100.0);

        AddItem(new Bow());
        PackItem(new Arrow(ArrowRestockAmount));
    }

    protected override void MaintainAmmo()
    {
        var pack = Backpack;

        if (pack != null && pack.GetAmount(typeof(Arrow)) < ArrowRestockThreshold)
        {
            pack.DropItem(new Arrow(ArrowRestockAmount));
        }
    }
}
