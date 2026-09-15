using System.Collections.Generic;

namespace Server.Custom.ClassSystem;

public enum AbilityCategory
{
    Passive,
    Pvp,
    Pve,
    Ultimate
}

public class AbilityCostData
{
    public int Exp { get; set; }
    public int Honor { get; set; }

    public bool IsAffordable(int expBalance, int honorBalance) => expBalance >= Exp && honorBalance >= Honor;
}

public class AbilityDefinitionData
{
    public string Id { get; set; }
    public AbilityCategory Category { get; set; }
    public List<AbilityCostData> PaymentOptions { get; set; } = new();
}

public class EvolutionDefinitionData
{
    public string Id { get; set; }
    public List<AbilityDefinitionData> Abilities { get; set; } = new();
}

public class PlayerClassDefinitionData
{
    public string Id { get; set; }
    public double SkillCap { get; set; } = 100.0;
    public List<SkillName> AllowedSkills { get; set; } = new();
    public List<string> ForbiddenItemTypeNames { get; set; } = new();
    public List<EvolutionDefinitionData> Evolutions { get; set; } = new();
}

public class ClassSystemConfig
{
    public int EvolutionThreshold { get; set; } = 1000;
    public int EvolutionRespecRefundPercent { get; set; } = 50;
    public int ClassRespecRefundPercent { get; set; } = 50;
    public List<PlayerClassDefinitionData> Classes { get; set; } = new();

    public static ClassSystemConfig CreateDefault()
    {
        var config = new ClassSystemConfig();

        config.Classes.Add(new PlayerClassDefinitionData
        {
            Id = "Test",
            SkillCap = 100.0,
            AllowedSkills = new List<SkillName>
            {
                SkillName.Swords, SkillName.Tactics, SkillName.Anatomy,
                SkillName.Healing, SkillName.Parry, SkillName.Focus
            },
            ForbiddenItemTypeNames = new List<string> { "Katana" },
            Evolutions = new List<EvolutionDefinitionData>
            {
                BuildTestEvolution("TestAlpha"),
                BuildTestEvolution("TestBeta"),
                BuildTestEvolution("TestGamma")
            }
        });

        return config;
    }

    private static EvolutionDefinitionData BuildTestEvolution(string evolutionId)
    {
        var evolution = new EvolutionDefinitionData { Id = evolutionId };

        for (var i = 1; i <= 2; i++)
        {
            evolution.Abilities.Add(new AbilityDefinitionData
            {
                Id = $"{evolutionId}_passive_{i}",
                Category = AbilityCategory.Passive,
                PaymentOptions = new List<AbilityCostData>
                {
                    new() { Exp = 100 * i },
                    new() { Honor = 200 * i }
                }
            });
        }

        for (var i = 1; i <= 2; i++)
        {
            evolution.Abilities.Add(new AbilityDefinitionData
            {
                Id = $"{evolutionId}_pvp_{i}",
                Category = AbilityCategory.Pvp,
                PaymentOptions = new List<AbilityCostData>
                {
                    new() { Honor = 200 * i },
                    new() { Exp = 400 * i }
                }
            });
        }

        for (var i = 1; i <= 2; i++)
        {
            evolution.Abilities.Add(new AbilityDefinitionData
            {
                Id = $"{evolutionId}_pve_{i}",
                Category = AbilityCategory.Pve,
                PaymentOptions = new List<AbilityCostData>
                {
                    new() { Exp = 200 * i },
                    new() { Honor = 400 * i }
                }
            });
        }

        evolution.Abilities.Add(new AbilityDefinitionData
        {
            Id = $"{evolutionId}_ultimate_1",
            Category = AbilityCategory.Ultimate,
            PaymentOptions = new List<AbilityCostData> { new() { Exp = 1000, Honor = 1000 } }
        });

        return evolution;
    }
}
