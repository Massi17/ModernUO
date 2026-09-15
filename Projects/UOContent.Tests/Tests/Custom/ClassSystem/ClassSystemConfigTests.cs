using System.Linq;
using Server;
using Server.Custom.ClassSystem;
using Xunit;

namespace UOContent.Tests;

public class ClassSystemConfigTests
{
    [Fact]
    public void CreateDefault_BuildsTestClassWithThreeEvolutionsAndSevenAbilitiesEach()
    {
        var config = ClassSystemConfig.CreateDefault();

        var testClass = Assert.Single(config.Classes);
        Assert.Equal("Test", testClass.Id);
        Assert.Equal(3, testClass.Evolutions.Count);
        Assert.Contains(SkillName.Swords, testClass.AllowedSkills);
        Assert.DoesNotContain(SkillName.Magery, testClass.AllowedSkills);
        Assert.Contains("Katana", testClass.ForbiddenItemTypeNames);

        foreach (var evolution in testClass.Evolutions)
        {
            Assert.Equal(7, evolution.Abilities.Count);
            Assert.Equal(2, evolution.Abilities.Count(a => a.Category == AbilityCategory.Passive));
            Assert.Equal(2, evolution.Abilities.Count(a => a.Category == AbilityCategory.Pvp));
            Assert.Equal(2, evolution.Abilities.Count(a => a.Category == AbilityCategory.Pve));
            Assert.Equal(1, evolution.Abilities.Count(a => a.Category == AbilityCategory.Ultimate));
        }
    }

    [Fact]
    public void AbilityCostData_IsAffordable_RequiresEveryCurrencyTheOptionSpecifies()
    {
        var comboCost = new AbilityCostData { Exp = 1000, Honor = 1000 };

        Assert.False(comboCost.IsAffordable(999, 1000));
        Assert.False(comboCost.IsAffordable(1000, 999));
        Assert.True(comboCost.IsAffordable(1000, 1000));

        var honorOnlyCost = new AbilityCostData { Honor = 200 };
        Assert.True(honorOnlyCost.IsAffordable(0, 200));
    }
}
