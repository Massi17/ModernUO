using Server.Mobiles;

namespace Server.Custom.ClassSystem;

public static class PlayerClassAssignment
{
    public static bool AssignClass(PlayerMobile pm, string classId, out string failureReason)
    {
        var classDef = PlayerClassSystem.GetClass(classId);
        if (classDef == null)
        {
            failureReason = $"No class with id '{classId}' exists.";
            return false;
        }

        var context = PlayerClassSystem.GetOrCreateContext(pm);
        if (!string.IsNullOrEmpty(context.ClassId))
        {
            failureReason = $"{pm.Name} already belongs to class '{context.ClassId}'. Use a respec to change class.";
            return false;
        }

        context.ClassId = classId;
        ApplySkillCaps(pm, classDef);

        failureReason = null;
        return true;
    }

    public static void ApplySkillCaps(PlayerMobile pm, PlayerClassDefinitionData classDef)
    {
        var skills = pm.Skills;
        for (var i = 0; i < skills.Length; i++)
        {
            var skill = skills[i];
            skill.Cap = classDef.AllowedSkills.Contains(skill.SkillName) ? classDef.SkillCap : 0.0;
        }
    }
}
