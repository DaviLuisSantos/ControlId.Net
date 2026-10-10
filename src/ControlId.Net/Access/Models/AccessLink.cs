using System.Collections.Generic;

namespace ControlId.Access;

/// <summary>
/// Associação entre dois objetos (tabelas <c>user_groups</c>, <c>group_access_rules</c>...).
/// Use com <see cref="IAccessClient.LinkAsync"/> e <see cref="IAccessClient.UnlinkAsync"/>.
/// </summary>
public sealed class AccessLink
{
    private AccessLink(string @object, string leftField, long leftId, string rightField, long rightId)
    {
        Object = @object;
        Values = new Dictionary<string, long> { [leftField] = leftId, [rightField] = rightId };
    }

    /// <summary>Nome do objeto de associação no equipamento.</summary>
    public string Object { get; }

    /// <summary>Campos e IDs da associação.</summary>
    public IReadOnlyDictionary<string, long> Values { get; }

    /// <summary>Usuário pertence ao grupo (<c>user_groups</c>).</summary>
    public static AccessLink UserGroup(long userId, long groupId) =>
        new("user_groups", "user_id", userId, "group_id", groupId);

    /// <summary>Regra de acesso vale para o grupo (<c>group_access_rules</c>).</summary>
    public static AccessLink GroupAccessRule(long groupId, long accessRuleId) =>
        new("group_access_rules", "group_id", groupId, "access_rule_id", accessRuleId);

    /// <summary>Regra de acesso vale para o usuário (<c>user_access_rules</c>).</summary>
    public static AccessLink UserAccessRule(long userId, long accessRuleId) =>
        new("user_access_rules", "user_id", userId, "access_rule_id", accessRuleId);

    /// <summary>Regra de acesso vale no horário (<c>access_rule_time_zones</c>).</summary>
    public static AccessLink AccessRuleTimeZone(long accessRuleId, long timeZoneId) =>
        new("access_rule_time_zones", "access_rule_id", accessRuleId, "time_zone_id", timeZoneId);

    /// <summary>Regra de acesso vale no portal (<c>portal_access_rules</c>).</summary>
    public static AccessLink PortalAccessRule(long portalId, long accessRuleId) =>
        new("portal_access_rules", "portal_id", portalId, "access_rule_id", accessRuleId);

    internal object ToWhere() => new Dictionary<string, object> { [Object] = Values };
}
