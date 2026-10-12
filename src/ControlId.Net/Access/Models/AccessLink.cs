using System.Collections.Generic;

namespace ControlId.Access;

/// <summary>
/// Associação entre dois objetos (tabelas <c>user_groups</c>, <c>group_access_rules</c>...).
/// Use com <see cref="IAccessClient.LinkAsync"/> e <see cref="IAccessClient.UnlinkAsync"/>.
/// </summary>
public sealed class AccessLink
{
    private AccessLink(string @object, string leftField, long leftId, string rightField, long rightId)
        : this(@object, new Dictionary<string, long> { [leftField] = leftId, [rightField] = rightId })
    {
    }

    private AccessLink(string @object, Dictionary<string, long> values)
    {
        Object = @object;
        Values = values;
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

    /// <summary>Regra de acesso vale para a área (<c>area_access_rules</c>).</summary>
    public static AccessLink AreaAccessRule(long areaId, long accessRuleId) =>
        new("area_access_rules", "area_id", areaId, "access_rule_id", accessRuleId);

    /// <summary>Script de ação executado no portal (<c>portal_actions</c>).</summary>
    public static AccessLink PortalAction(long portalId, long actionId) =>
        new("portal_actions", "portal_id", portalId, "action_id", actionId);

    /// <summary>Liberação agendada vale pela regra de acesso (<c>scheduled_unlock_access_rules</c>).</summary>
    public static AccessLink ScheduledUnlockAccessRule(long scheduledUnlockId, long accessRuleId) =>
        new("scheduled_unlock_access_rules", "scheduled_unlock_id", scheduledUnlockId, "access_rule_id", accessRuleId);

    /// <summary>Zona de alarme ativa no horário (<c>alarm_zone_time_zones</c>).</summary>
    public static AccessLink AlarmZoneTimeZone(long alarmZoneId, long timeZoneId) =>
        new("alarm_zone_time_zones", "alarm_zone_id", alarmZoneId, "time_zone_id", timeZoneId);

    /// <summary>
    /// Regra de acesso aplicada aos cartões de contingência (<c>contingency_card_access_rules</c>; por padrão, a regra 1).
    /// </summary>
    public static AccessLink ContingencyCardAccessRule(long accessRuleId) =>
        new("contingency_card_access_rules", new Dictionary<string, long> { ["access_rule_id"] = accessRuleId });

    /// <summary>Regra de acesso vale no horário (<c>access_rule_time_zones</c>).</summary>
    public static AccessLink AccessRuleTimeZone(long accessRuleId, long timeZoneId) =>
        new("access_rule_time_zones", "access_rule_id", accessRuleId, "time_zone_id", timeZoneId);

    /// <summary>Regra de acesso vale no portal (<c>portal_access_rules</c>).</summary>
    public static AccessLink PortalAccessRule(long portalId, long accessRuleId) =>
        new("portal_access_rules", "portal_id", portalId, "access_rule_id", accessRuleId);

    internal object ToWhere() => new Dictionary<string, object> { [Object] = Values };
}
