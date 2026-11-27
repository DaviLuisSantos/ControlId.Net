using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace ControlId.Access;

public sealed partial class AccessClient
{
    // Quantos IDs vão num "IN" por leitura (o equipamento recusa mais de 999).
    private const int SyncReadChunk = 500;

    // Marcas em memória para comparar o que o equipamento não devolve em claro: senha e foto (ver DesiredUser).
    private readonly ConcurrentDictionary<long, string> _syncPasswordMarks = new();
    private readonly ConcurrentDictionary<long, SyncPhotoMark> _syncPhotoMarks = new();
    private readonly byte[] _syncMarkKey = NewSyncMarkKey();

    private readonly struct SyncPhotoMark
    {
        public SyncPhotoMark(string sha256, long deviceTimestamp)
        {
            Sha256 = sha256;
            DeviceTimestamp = deviceTimestamp;
        }

        public string Sha256 { get; }

        public long DeviceTimestamp { get; }
    }

    // Linha de user_groups (tabela de associação, sem id).
    private sealed class UserGroupRow
    {
        [JsonPropertyName("user_id")]
        public long UserId { get; set; }

        [JsonPropertyName("group_id")]
        public long GroupId { get; set; }
    }

    /// <inheritdoc/>
    public async Task<UserSyncResult> SyncUserAsync(DesiredUser desired, CancellationToken cancellationToken = default)
    {
        if (desired is null)
        {
            throw new ArgumentNullException(nameof(desired));
        }

        var results = await SyncUsersAsync(new[] { desired }, null, cancellationToken).ConfigureAwait(false);
        return results[0];
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<UserSyncResult>> SyncUsersAsync(
        IEnumerable<DesiredUser> desired, UserSyncOptions? options = null, CancellationToken cancellationToken = default)
    {
        if (desired is null)
        {
            throw new ArgumentNullException(nameof(desired));
        }

        var list = desired.ToList();
        ValidateDesiredUsers(list);
        var removeMissing = options?.RemoveMissing == true;
        var removeUnlisted = options?.RemoveUnlisted == true;
        var dryRun = options?.DryRun == true;
        if (removeMissing && !dryRun)
        {
            DataLoss.Require(options!.DataLossConfirmation, "Sincronizar com RemoveMissing");
        }

        var active = list.Select(d => new SyncBinding(d, new UserSyncResult(d.Id))).ToList();

        await ReadSyncStateAsync(active, cancellationToken).ConfigureAwait(false);
        await SyncUsersStepAsync(active, dryRun, cancellationToken).ConfigureAwait(false);

        var removed = new List<UserSyncResult>();
        var removedUserIds = new HashSet<long>();
        if (removeMissing)
        {
            await RemoveMissingUsersAsync(new HashSet<long>(active.Select(a => a.Desired.Id)), removed, dryRun, cancellationToken).ConfigureAwait(false);
            if (dryRun)
            {
                // A execução real remove essas pessoas antes de ler os cartões, então os cartões delas ficam livres.
                removedUserIds.UnionWith(removed.Where(r => r.Removed).Select(r => r.UserId));
            }
        }

        await SyncCardsAsync(active, removeUnlisted, dryRun, removedUserIds, cancellationToken).ConfigureAwait(false);
        await SyncGroupsAsync(active, removeUnlisted, dryRun, cancellationToken).ConfigureAwait(false);
        await SyncPhotosAsync(active, dryRun, cancellationToken).ConfigureAwait(false);

        var all = new List<UserSyncResult>(active.Count + removed.Count);
        all.AddRange(active.Select(a => a.Result));
        all.AddRange(removed);
        return all;
    }

    // Pessoa em andamento, com o estado lido do equipamento.
    private sealed class SyncBinding
    {
        public SyncBinding(DesiredUser desired, UserSyncResult result)
        {
            Desired = desired;
            Result = result;
        }

        public DesiredUser Desired { get; }

        public UserSyncResult Result { get; }

        public AccessUser? Existing { get; set; }

        public List<AccessCard> DeviceCards { get; set; } = new();

        public List<long> DeviceGroups { get; set; } = new();

        public bool Failed => Result.ErrorList.Count > 0;
    }

#pragma warning disable CA2208 // "desired" é o parâmetro do método público que chamou este validador.
    private static void ValidateDesiredUsers(List<DesiredUser> list)
    {
        var seen = new HashSet<long>();
        foreach (var user in list)
        {
            if (user is null)
            {
                throw new ArgumentException("Há uma pessoa nula na lista.", "desired");
            }

            if (user.Id <= 0)
            {
                throw new ArgumentException("Cada pessoa precisa de um Id maior que zero.", "desired");
            }

            if (string.IsNullOrWhiteSpace(user.Name))
            {
                throw new ArgumentException($"A pessoa {user.Id} precisa de um nome.", "desired");
            }

            if (!seen.Add(user.Id))
            {
                throw new ArgumentException($"O Id {user.Id} aparece mais de uma vez na lista.", "desired");
            }

            if (user.Photo is { Length: 0 })
            {
                throw new ArgumentException($"A foto da pessoa {user.Id} está vazia; use null para não gerenciar a foto.", "desired");
            }
        }
    }
#pragma warning restore CA2208

    // Lê users, cards e user_groups só das pessoas desejadas (e só as tabelas que alguma pessoa gerencia).
    private async Task ReadSyncStateAsync(List<SyncBinding> active, CancellationToken cancellationToken)
    {
        if (active.Count == 0)
        {
            return;
        }

        var byId = active.ToDictionary(a => a.Desired.Id);
        foreach (var user in await LoadByIdsAsync<AccessUser>("users", "id", active.Select(a => a.Desired.Id), cancellationToken).ConfigureAwait(false))
        {
            if (user.Id is { } id && byId.TryGetValue(id, out var binding))
            {
                binding.Existing = user;
            }
        }

        var withCards = active.Where(a => a.Desired.Cards is not null && a.Existing is not null).Select(a => a.Desired.Id).ToList();
        foreach (var card in await LoadByIdsAsync<AccessCard>("cards", "user_id", withCards, cancellationToken).ConfigureAwait(false))
        {
            if (byId.TryGetValue(card.UserId, out var binding))
            {
                binding.DeviceCards.Add(card);
            }
        }

        var withGroups = active.Where(a => a.Desired.GroupIds is not null && a.Existing is not null).Select(a => a.Desired.Id).ToList();
        foreach (var row in await LoadByIdsAsync<UserGroupRow>("user_groups", "user_id", withGroups, cancellationToken).ConfigureAwait(false))
        {
            if (byId.TryGetValue(row.UserId, out var binding))
            {
                binding.DeviceGroups.Add(row.GroupId);
            }
        }
    }

    private async Task<List<T>> LoadByIdsAsync<T>(string @object, string field, IEnumerable<long> ids, CancellationToken cancellationToken, IList<string>? fields = null)
    {
        var all = new List<T>();
        var pending = ids.ToList();
        for (var offset = 0; offset < pending.Count; offset += SyncReadChunk)
        {
            var chunk = pending.GetRange(offset, Math.Min(SyncReadChunk, pending.Count - offset));
            var request = new LoadObjectsRequest(@object) { Where = Where.In(@object, field, chunk), Fields = fields };
            all.AddRange(await LoadObjectsAsync<T>(request, cancellationToken).ConfigureAwait(false));
        }

        return all;
    }

    // Etapa 1: cria ou atualiza os usuários (nome, matrícula, validade, senha). No dry-run só compara: não cifra a senha.
    private async Task SyncUsersStepAsync(List<SyncBinding> active, bool dryRun, CancellationToken cancellationToken)
    {
        var writes = new List<(SyncBinding Binding, Dictionary<string, object?> Values, List<string> Fields, string? PasswordMark)>();
        foreach (var binding in active)
        {
            var desired = binding.Desired;
            var existing = binding.Existing;
            var values = new Dictionary<string, object?> { ["id"] = desired.Id };
            var fields = new List<string>();

            if (existing is null || !string.Equals(existing.Name, desired.Name, StringComparison.Ordinal))
            {
                values["name"] = desired.Name;
                fields.Add("name");
            }

            if (desired.Registration is not null && (existing is null || !string.Equals(existing.Registration ?? string.Empty, desired.Registration, StringComparison.Ordinal)))
            {
                values["registration"] = desired.Registration;
                fields.Add("registration");
            }

            var begin = ToDeviceEpoch(desired.BeginTime);
            if (existing is null ? begin != 0 : (existing.BeginTime ?? 0) != begin)
            {
                values["begin_time"] = begin;
                fields.Add("begin_time");
            }

            var end = ToDeviceEpoch(desired.EndTime);
            if (existing is null ? end != 0 : (existing.EndTime ?? 0) != end)
            {
                values["end_time"] = end;
                fields.Add("end_time");
            }

            string? passwordMark = null;
            if (!string.IsNullOrEmpty(desired.Password))
            {
                var mark = PasswordMark(desired.Id, desired.Password!);
                var known = _syncPasswordMarks.TryGetValue(desired.Id, out var stored) && stored == mark;
                if (existing is null || string.IsNullOrEmpty(existing.Password) || !known)
                {
                    if (dryRun)
                    {
                        // Sem HashPasswordAsync e sem gravar a marca: a execução real volta a ver a senha como desconhecida.
                        fields.Add("password");
                    }
                    else
                    {
                        try
                        {
                            var hash = await HashPasswordAsync(desired.Password!, cancellationToken).ConfigureAwait(false);
                            values["password"] = hash.Hash;
                            values["salt"] = hash.Salt;
                            fields.Add("password");
                            passwordMark = mark;
                        }
                        catch (ControlIdException ex)
                        {
                            binding.Result.Fail("password", ex.Message, ex);
                            continue;
                        }
                    }
                }
            }

            if (existing is null || fields.Count > 0)
            {
                if (dryRun)
                {
                    if (existing is null)
                    {
                        binding.Result.Created = true;
                    }

                    binding.Result.ChangedFieldList.AddRange(fields);
                }
                else
                {
                    writes.Add((binding, values, fields, passwordMark));
                }
            }
        }

        if (dryRun)
        {
            return;
        }

        await ApplyIsolatedAsync(
            writes,
            batch => CreateOrModifyObjectsAsync("users", batch.Select(w => w.Values).ToList(), cancellationToken),
            single => CreateOrModifyObjectsAsync("users", new[] { single.Values }, cancellationToken),
            write =>
            {
                var (binding, _, fields, passwordMark) = write;
                if (binding.Existing is null)
                {
                    binding.Result.Created = true;
                    _syncPhotoMarks.TryRemove(binding.Desired.Id, out _);
                }

                binding.Result.ChangedFieldList.AddRange(fields);
                if (passwordMark is not null)
                {
                    _syncPasswordMarks[binding.Desired.Id] = passwordMark;
                }
            },
            (write, ex) => write.Binding.Result.Fail("user", ex.Message, ex)).ConfigureAwait(false);
    }

    // Remove do equipamento quem não está na lista desejada. A foto vai junto com o usuário.
    // No dry-run só enumera e relata, sem apagar pessoas nem marcas em memória.
    private async Task RemoveMissingUsersAsync(HashSet<long> wanted, List<UserSyncResult> removed, bool dryRun, CancellationToken cancellationToken)
    {
        var extra = new List<long>();
        await foreach (var user in EnumerateUsersAsync(DefaultPageSize, cancellationToken).ConfigureAwait(false))
        {
            if (user.Id is { } id && !wanted.Contains(id))
            {
                extra.Add(id);
            }
        }

        foreach (var id in extra)
        {
            var result = new UserSyncResult(id);
            if (dryRun)
            {
                result.Removed = true;
                removed.Add(result);
                continue;
            }

            try
            {
                if (await DeleteUserAsync(id, cancellationToken).ConfigureAwait(false))
                {
                    result.Removed = true;
                }
            }
            catch (ControlIdException ex)
            {
                result.Fail("user", ex.Message, ex);
            }

            _syncPasswordMarks.TryRemove(id, out _);
            _syncPhotoMarks.TryRemove(id, out _);
            removed.Add(result);
        }
    }

    // Etapa 2: cartões. Com RemoveUnlisted, remove os que sobram; depois cria os que faltam (recusando o que é de outra pessoa).
    // No dry-run nada é destruído nem criado: o resultado recebe o diff e a checagem de dono continua sendo feita por leitura.
    private async Task SyncCardsAsync(
        List<SyncBinding> active,
        bool removeUnlisted,
        bool dryRun,
        IReadOnlyCollection<long> removedUserIds,
        CancellationToken cancellationToken)
    {
        var managed = active.Where(a => !a.Failed && a.Desired.Cards is not null).ToList();
        HashSet<long>? ignoredCardIds = dryRun ? new HashSet<long>() : null;

        if (removeUnlisted)
        {
            foreach (var binding in managed)
            {
                var wanted = new HashSet<long>(binding.Desired.Cards!);
                var surplus = binding.DeviceCards.Where(c => c.Id is not null && !wanted.Contains(c.Value)).ToList();
                if (surplus.Count == 0)
                {
                    continue;
                }

                if (dryRun)
                {
                    // Esses cartões seriam apagados antes da leitura de donos da execução real: não contam mais para ninguém.
                    binding.Result.CardsRemovedList.AddRange(surplus.Select(c => c.Value));
                    ignoredCardIds!.UnionWith(surplus.Select(c => c.Id!.Value));
                    continue;
                }

                try
                {
                    await DestroyObjectsAsync("cards", Where.In("cards", "id", surplus.Select(c => c.Id!.Value).ToList()), cancellationToken).ConfigureAwait(false);
                    binding.Result.CardsRemovedList.AddRange(surplus.Select(c => c.Value));
                }
                catch (ControlIdException ex)
                {
                    binding.Result.Fail("cards", ex.Message, ex);
                }
            }
        }

        var toAdd = new List<(SyncBinding Binding, List<long> Values)>();
        foreach (var binding in managed.Where(b => !b.Failed))
        {
            var current = new HashSet<long>(binding.DeviceCards.Select(c => c.Value));
            var missing = binding.Desired.Cards!.Distinct().Where(v => !current.Contains(v)).ToList();
            if (missing.Count > 0)
            {
                toAdd.Add((binding, missing));
            }
        }

        if (toAdd.Count == 0)
        {
            return;
        }

        var owners = await ReadCardOwnersAsync(
            toAdd.SelectMany(t => t.Values).Distinct().ToList(),
            ignoredCardIds,
            dryRun ? removedUserIds : null,
            cancellationToken).ConfigureAwait(false);
        foreach (var (binding, values) in toAdd)
        {
            var allowed = new List<long>();
            foreach (var value in values)
            {
                if (owners.TryGetValue(value, out var owner) && owner != binding.Desired.Id)
                {
                    binding.Result.Fail($"card:{value.ToString(CultureInfo.InvariantCulture)}", $"O cartão {value} já pertence à pessoa {owner}.");
                }
                else
                {
                    owners[value] = binding.Desired.Id;
                    allowed.Add(value);
                }
            }

            if (dryRun)
            {
                binding.Result.CardsAddedList.AddRange(allowed);
                continue;
            }

            await ApplyIsolatedAsync(
                allowed,
                batch => CreateObjectsAsync("cards", batch.Select(v => new AccessCard { UserId = binding.Desired.Id, Value = v }).ToList(), cancellationToken),
                single => CreateObjectsAsync("cards", new[] { new AccessCard { UserId = binding.Desired.Id, Value = single } }, cancellationToken),
                value => binding.Result.CardsAddedList.Add(value),
                (value, ex) =>
                {
                    owners.Remove(value);
                    binding.Result.Fail($"card:{value.ToString(CultureInfo.InvariantCulture)}", ex.Message, ex);
                }).ConfigureAwait(false);
        }
    }

    // Quem já tem cada valor de cartão. Se a leitura falha, segue sem a checagem e deixa o equipamento decidir.
    // No dry-run, os cartões que seriam apagados (ignoredCardIds) e os das pessoas que seriam removidas (ignoredUserIds)
    // são desconsiderados, como aconteceria depois das remoções da execução real.
    private async Task<Dictionary<long, long>> ReadCardOwnersAsync(
        List<long> values,
        HashSet<long>? ignoredCardIds,
        IReadOnlyCollection<long>? ignoredUserIds,
        CancellationToken cancellationToken)
    {
        var owners = new Dictionary<long, long>();
        try
        {
            foreach (var card in await LoadByIdsAsync<AccessCard>("cards", "value", values, cancellationToken).ConfigureAwait(false))
            {
                if (ignoredCardIds is not null && card.Id is { } id && ignoredCardIds.Contains(id))
                {
                    continue;
                }

                if (ignoredUserIds is not null && ignoredUserIds.Contains(card.UserId))
                {
                    continue;
                }

                owners[card.Value] = card.UserId;
            }
        }
        catch (ControlIdException)
        {
            owners.Clear();
        }

        return owners;
    }

    // Etapa 3: vínculos com grupos. No dry-run só relata os que seriam removidos e criados.
    private async Task SyncGroupsAsync(List<SyncBinding> active, bool removeUnlisted, bool dryRun, CancellationToken cancellationToken)
    {
        foreach (var binding in active.Where(a => !a.Failed && a.Desired.GroupIds is not null))
        {
            var wanted = new HashSet<long>(binding.Desired.GroupIds!);
            var current = new HashSet<long>(binding.DeviceGroups);

            if (removeUnlisted)
            {
                foreach (var groupId in current.Where(g => !wanted.Contains(g)).OrderBy(g => g))
                {
                    if (dryRun)
                    {
                        binding.Result.GroupsRemovedList.Add(groupId);
                        continue;
                    }

                    try
                    {
                        await UnlinkAsync(AccessLink.UserGroup(binding.Desired.Id, groupId), cancellationToken).ConfigureAwait(false);
                        binding.Result.GroupsRemovedList.Add(groupId);
                    }
                    catch (ControlIdException ex)
                    {
                        binding.Result.Fail($"group:{groupId.ToString(CultureInfo.InvariantCulture)}", ex.Message, ex);
                    }
                }
            }

            var missing = wanted.Where(g => !current.Contains(g)).OrderBy(g => g).ToList();
            if (dryRun)
            {
                binding.Result.GroupsAddedList.AddRange(missing);
                continue;
            }

            await ApplyIsolatedAsync(
                missing,
                batch => CreateObjectsAsync(
                    "user_groups",
                    batch.Select(g => AccessLink.UserGroup(binding.Desired.Id, g).Values).ToList(),
                    cancellationToken),
                single => LinkAsync(AccessLink.UserGroup(binding.Desired.Id, single), cancellationToken),
                groupId => binding.Result.GroupsAddedList.Add(groupId),
                (groupId, ex) => binding.Result.Fail($"group:{groupId.ToString(CultureInfo.InvariantCulture)}", ex.Message, ex)).ConfigureAwait(false);
        }
    }

    // Etapa 4: fotos. Só envia o que mudou (ver DesiredUser.Photo). No dry-run só lê e relata; não grava a marca da foto.
    private async Task SyncPhotosAsync(List<SyncBinding> active, bool dryRun, CancellationToken cancellationToken)
    {
        var toSend = new List<(SyncBinding Binding, string Sha256)>();
        foreach (var binding in active.Where(a => !a.Failed && a.Desired.Photo is not null))
        {
            var photo = binding.Desired.Photo!;
            var sha = Sha256Hex(photo);
            var id = binding.Desired.Id;
            var deviceTimestamp = binding.Existing?.ImageTimestamp ?? 0;

            if (binding.Existing is null)
            {
                toSend.Add((binding, sha));
                continue;
            }

            var hasMark = _syncPhotoMarks.TryGetValue(id, out var mark) && mark.DeviceTimestamp == deviceTimestamp;
            if (hasMark && mark.Sha256 == sha)
            {
                continue;
            }

            if (hasMark || deviceTimestamp == 0)
            {
                // A foto da marca é outra, ou o equipamento não tem foto: precisa enviar.
                toSend.Add((binding, sha));
                continue;
            }

            try
            {
                var onDevice = await GetUserImageAsync(id, cancellationToken).ConfigureAwait(false);
                if (Sha256Hex(onDevice) == sha)
                {
                    if (!dryRun)
                    {
                        _syncPhotoMarks[id] = new SyncPhotoMark(sha, deviceTimestamp);
                    }

                    continue;
                }
            }
            catch (ControlIdDeviceErrorException)
            {
                // Sem foto legível no equipamento: envia.
            }
            catch (ControlIdException ex)
            {
                binding.Result.Fail("photo", ex.Message, ex);
                continue;
            }

            toSend.Add((binding, sha));
        }

        if (toSend.Count == 0)
        {
            return;
        }

        if (dryRun)
        {
            foreach (var (binding, _) in toSend)
            {
                binding.Result.PhotoUpdated = true;
            }

            return;
        }

        IReadOnlyList<FaceImageResult> results;
        try
        {
            results = await SetUserImagesAsync(
                toSend.Select(t => new UserImage(t.Binding.Desired.Id, t.Binding.Desired.Photo!)).ToList(),
                new UserImageUploadOptions { RejectExistingFace = false },
                cancellationToken).ConfigureAwait(false);
        }
        catch (ControlIdException ex)
        {
            foreach (var (binding, _) in toSend)
            {
                binding.Result.Fail("photo", ex.Message, ex);
            }

            return;
        }

        var failures = results.Where(r => !r.Success && r.UserId is not null).ToDictionary(r => r.UserId!.Value, r => r);
        var sent = new List<(SyncBinding Binding, string Sha256)>();
        foreach (var (binding, sha) in toSend)
        {
            if (failures.TryGetValue(binding.Desired.Id, out var failure))
            {
                var reason = failure.Errors.Count == 0
                    ? "foto recusada pelo equipamento."
                    : string.Join("; ", failure.Errors.Select(e => e.Message));
                binding.Result.Fail("photo", reason);
            }
            else
            {
                binding.Result.PhotoUpdated = true;
                sent.Add((binding, sha));
            }
        }

        await RememberPhotosAsync(sent, cancellationToken).ConfigureAwait(false);
    }

    // Guarda o SHA-256 enviado junto com o image_timestamp que o equipamento passou a ter.
    private async Task RememberPhotosAsync(List<(SyncBinding Binding, string Sha256)> sent, CancellationToken cancellationToken)
    {
        if (sent.Count == 0)
        {
            return;
        }

        try
        {
            var users = await LoadByIdsAsync<AccessUser>(
                "users", "id", sent.Select(s => s.Binding.Desired.Id), cancellationToken, new List<string> { "id", "image_timestamp" }).ConfigureAwait(false);
            var timestamps = users.Where(u => u.Id is not null).ToDictionary(u => u.Id!.Value, u => u.ImageTimestamp ?? 0);
            foreach (var (binding, sha) in sent)
            {
                var id = binding.Desired.Id;
                if (timestamps.TryGetValue(id, out var timestamp))
                {
                    _syncPhotoMarks[id] = new SyncPhotoMark(sha, timestamp);
                }
                else
                {
                    _syncPhotoMarks.TryRemove(id, out _);
                }
            }
        }
        catch (ControlIdException)
        {
            // Sem a marca, a próxima sincronização baixa a foto do equipamento e compara.
            foreach (var (binding, _) in sent)
            {
                _syncPhotoMarks.TryRemove(binding.Desired.Id, out _);
            }
        }
    }

    // Aplica um lote; se o equipamento recusar, repete item a item para isolar o erro em quem o causou.
    private static async Task ApplyIsolatedAsync<T>(
        IReadOnlyList<T> items,
        Func<IReadOnlyList<T>, Task> batch,
        Func<T, Task> single,
        Action<T> onSuccess,
        Action<T, ControlIdException> onFailure)
    {
        if (items.Count == 0)
        {
            return;
        }

        try
        {
            await batch(items).ConfigureAwait(false);
            foreach (var item in items)
            {
                onSuccess(item);
            }

            return;
        }
        catch (ControlIdDeviceErrorException) when (items.Count > 1)
        {
            // Segue para o envio item a item.
        }
        catch (ControlIdException ex)
        {
            foreach (var item in items)
            {
                onFailure(item, ex);
            }

            return;
        }

        foreach (var item in items)
        {
            try
            {
                await single(item).ConfigureAwait(false);
                onSuccess(item);
            }
            catch (ControlIdException ex)
            {
                onFailure(item, ex);
            }
        }
    }

    // Inverso de DeviceLocalTime.FromEpoch: a hora de parede vira o epoch, sem fuso. null = 0 (sem limite).
    private static long ToDeviceEpoch(DateTime? value) =>
        value is null
            ? 0
            : new DateTimeOffset(DateTime.SpecifyKind(value.Value, DateTimeKind.Unspecified), TimeSpan.Zero).ToUnixTimeSeconds();

    private string PasswordMark(long userId, string password)
    {
        using var hmac = new HMACSHA256(_syncMarkKey);
        return ToHex(hmac.ComputeHash(Encoding.UTF8.GetBytes(userId.ToString(CultureInfo.InvariantCulture) + ":" + password)));
    }

    private static string Sha256Hex(byte[] data)
    {
        using var sha = SHA256.Create();
        return ToHex(sha.ComputeHash(data));
    }

    #if NET5_0_OR_GREATER
    private static string ToHex(byte[] bytes) => Convert.ToHexString(bytes);
#else
    private static string ToHex(byte[] bytes) => BitConverter.ToString(bytes).Replace("-", string.Empty);
#endif

    private static byte[] NewSyncMarkKey()
    {
        var key = new byte[32];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(key);
        return key;
    }
}
