using System.Text;
using ControlId.Access;

namespace ControlId.Tests;

public class CustomSchemaTests
{
    private static readonly ControlIdDeviceOptions Options = new()
    {
        BaseAddress = new Uri("http://10.0.0.5"),
        Login = "admin",
        Password = "secret",
    };

    private static FakeDeviceHandler LoggedDevice() => new FakeDeviceHandler().OnJson("login.fcgi", """{"session":"s"}""");

    [Fact]
    public async Task GetObjectMetadata_parses_fields_foreign_keys_and_joins()
    {
        var device = LoggedDevice().OnJson(
            "object_metadata.fcgi",
            """
            {"custom_departments":{"fields":{
              "id":{"type":"INTEGER","primary key":true,"not null":true,"default value":"","unique":false},
              "user_id":{"type":"INTEGER","primary key":false,"not null":true,"default value":0,"unique":false,"foreign key":{"object":"users","field":"id"}}},
             "joins":{"users":[{"object":"users","field":"id"}]}}}
            """);
        using var client = new AccessClient(Options, device.CreateClient());

        var metadata = await client.GetObjectMetadataAsync(ObjectMetadataFilter.Custom);

        var table = Assert.Single(metadata);
        Assert.Equal("custom_departments", table.Key);
        Assert.True(table.Value.Fields["id"].PrimaryKey);
        Assert.Equal("INTEGER", table.Value.Fields["id"].Type);
        var userId = table.Value.Fields["user_id"];
        Assert.True(userId.NotNull);
        Assert.Equal("users", userId.ForeignKey!.Object);
        Assert.Equal("id", userId.ForeignKey.Field);
        Assert.Equal("users", Assert.Single(table.Value.Joins["users"]).Object);
        Assert.Equal("""{"filter":"custom"}""", device.RequestsTo("object_metadata.fcgi").Single().Body);
    }

    [Fact]
    public async Task GetObjectMetadata_without_filter_sends_empty_body_and_default_filter_is_sent_by_name()
    {
        var device = LoggedDevice().OnJson("object_metadata.fcgi", "{}");
        using var client = new AccessClient(Options, device.CreateClient());

        Assert.Empty(await client.GetObjectMetadataAsync());
        await client.GetObjectMetadataAsync(ObjectMetadataFilter.Default);

        var bodies = device.RequestsTo("object_metadata.fcgi").Select(r => r.Body).ToList();
        Assert.Equal(new[] { "{}", """{"filter":"default"}""" }, bodies);
    }

    [Fact]
    public async Task CreateCustomTable_sends_fields_with_constraint_and_foreign_key_and_returns_the_id()
    {
        var device = LoggedDevice().OnJson("object_add.fcgi", """{"ids":[5]}""");
        using var client = new AccessClient(Options, device.CreateClient());

        var id = await client.CreateCustomTableAsync(
            "custom_departments",
            "Departamentos",
            new[]
            {
                new CustomFieldDefinition { ColumnName = "name", Name = "Nome", Type = CustomFieldType.Text },
                new CustomFieldDefinition
                {
                    ColumnName = "user_id",
                    Name = "Usuário",
                    Type = CustomFieldType.Integer,
                    NotNull = true,
                    ForeignKey = new ForeignKey { Object = "users", Field = "id" },
                },
                new CustomFieldDefinition { ColumnName = "code", Name = "Código", Type = CustomFieldType.BigInt, Unique = true },
            });

        Assert.Equal(5, id);
        Assert.Equal(
            """{"object":"custom_departments","name":"Departamentos","fields":[""" +
            """{"column_name":"name","name":"Nome","type":"TEXT","constraint":"NONE"},""" +
            """{"column_name":"user_id","name":"Usuário","type":"INTEGER","constraint":"NOT_NULL","foreign_key":{"object":"users","field":"id"}},""" +
            """{"column_name":"code","name":"Código","type":"BIGINT","constraint":"NONE","unique":true}]}""",
            device.RequestsTo("object_add.fcgi").Single().Body);
    }

    [Fact]
    public async Task CreateCustomTable_with_custom_id_and_without_fields()
    {
        var device = LoggedDevice().OnJson("object_add.fcgi", """{"ids":[9]}""");
        using var client = new AccessClient(Options, device.CreateClient());

        await Assert.ThrowsAsync<ArgumentException>(() => client.CreateCustomTableAsync("t", "T", Array.Empty<CustomFieldDefinition>()));
        Assert.Empty(device.RequestsTo("object_add.fcgi"));

        await client.CreateCustomTableAsync("t", "T", new[] { new CustomFieldDefinition { ColumnName = "c", Name = "C", Type = CustomFieldType.Blob } }, id: 9);

        Assert.Equal(
            """{"object":"t","name":"T","id":9,"fields":[{"column_name":"c","name":"C","type":"BLOB","constraint":"NONE"}]}""",
            device.RequestsTo("object_add.fcgi").Single().Body);
    }

    [Fact]
    public async Task DeleteCustomTables_requires_confirmation_and_sends_ids()
    {
        var device = LoggedDevice().OnJson("object_remove.fcgi", "");
        using var client = new AccessClient(Options, device.CreateClient());

        var ex = await Assert.ThrowsAsync<ArgumentException>(() => client.DeleteCustomTablesAsync(default, new long[] { 5 }));
        Assert.Contains("DataLossConfirmation.Confirmed", ex.Message);
        Assert.Empty(device.RequestsTo("object_remove.fcgi"));

        await client.DeleteCustomTablesAsync(DataLossConfirmation.Confirmed, new long[] { 5, 6 });

        Assert.Equal("""{"ids":[5,6]}""", device.RequestsTo("object_remove.fcgi").Single().Body);
    }

    [Fact]
    public async Task AddCustomField_sends_table_and_definition_and_returns_the_id()
    {
        var device = LoggedDevice().OnJson("object_add_field.fcgi", """{"ids":[12]}""");
        using var client = new AccessClient(Options, device.CreateClient());

        var id = await client.AddCustomFieldAsync(
            "custom_departments",
            new CustomFieldDefinition { ColumnName = "description", Name = "Descrição", Type = CustomFieldType.Text });
        await client.AddCustomFieldAsync(
            "custom_departments",
            new CustomFieldDefinition { ColumnName = "level", Name = "Nível", Type = CustomFieldType.Integer, NotNull = true, DefaultValue = 1, Unique = true });

        Assert.Equal(12, id);
        var bodies = device.RequestsTo("object_add_field.fcgi").Select(r => r.Body).ToList();
        Assert.Equal(
            """{"object":"custom_departments","column_name":"description","name":"Descrição","type":"TEXT","constraint":"NONE"}""",
            bodies[0]);
        Assert.Equal(
            """{"object":"custom_departments","column_name":"level","name":"Nível","type":"INTEGER","constraint":"NOT_NULL","unique":true,"default_value":1}""",
            bodies[1]);
    }

    [Fact]
    public async Task AddCustomField_not_null_without_default_value_is_refused_before_sending()
    {
        var device = LoggedDevice().OnJson("object_add_field.fcgi", """{"ids":[1]}""");
        using var client = new AccessClient(Options, device.CreateClient());

        await Assert.ThrowsAsync<ArgumentException>(() => client.AddCustomFieldAsync(
            "t", new CustomFieldDefinition { ColumnName = "c", Name = "C", Type = CustomFieldType.Text, NotNull = true }));
        await Assert.ThrowsAsync<ArgumentException>(() => client.AddCustomFieldAsync(
            "t", new CustomFieldDefinition { ColumnName = "c", Type = CustomFieldType.Text }));

        Assert.Empty(device.RequestsTo("object_add_field.fcgi"));
    }

    [Fact]
    public async Task UpdateCustomField_sends_id_constraint_and_optional_name()
    {
        var device = LoggedDevice().OnJson("object_update_field.fcgi", "");
        using var client = new AccessClient(Options, device.CreateClient());

        await client.UpdateCustomFieldAsync(12, new CustomFieldDefinition { ColumnName = "description", Type = CustomFieldType.Text, Name = "Descrição Completa" });
        await client.UpdateCustomFieldAsync(12, new CustomFieldDefinition { ColumnName = "code", Type = CustomFieldType.Integer, Unique = true, DefaultValue = 0 });

        var bodies = device.RequestsTo("object_update_field.fcgi").Select(r => r.Body).ToList();
        Assert.Equal(
            """{"id":12,"column_name":"description","name":"Descrição Completa","type":"TEXT","constraint":"NONE","unique":false}""",
            bodies[0]);
        Assert.Equal(
            """{"id":12,"column_name":"code","type":"INTEGER","constraint":"NONE","unique":true,"default_value":0}""",
            bodies[1]);
    }

    [Fact]
    public async Task UpdateCustomField_unique_without_default_value_is_refused()
    {
        var device = LoggedDevice().OnJson("object_update_field.fcgi", "");
        using var client = new AccessClient(Options, device.CreateClient());

        await Assert.ThrowsAsync<ArgumentException>(() => client.UpdateCustomFieldAsync(
            12, new CustomFieldDefinition { ColumnName = "code", Type = CustomFieldType.Integer, Unique = true }));

        Assert.Empty(device.RequestsTo("object_update_field.fcgi"));
    }

    [Fact]
    public async Task RemoveCustomFields_requires_confirmation_and_returns_removed_ids()
    {
        var device = LoggedDevice().OnJson("object_remove_fields.fcgi", """{"ids":[12,13]}""");
        using var client = new AccessClient(Options, device.CreateClient());

        await Assert.ThrowsAsync<ArgumentException>(() => client.RemoveCustomFieldsAsync(default, new long[] { 12 }));
        Assert.Empty(device.RequestsTo("object_remove_fields.fcgi"));

        var removed = await client.RemoveCustomFieldsAsync(DataLossConfirmation.Confirmed, new long[] { 12, 13 });

        Assert.Equal(new long[] { 12, 13 }, removed);
        Assert.Equal("""{"ids":[12,13]}""", device.RequestsTo("object_remove_fields.fcgi").Single().Body);
    }

    [Fact]
    public async Task ExportObjectCsv_sends_object_and_optional_columns_and_returns_bytes()
    {
        var csv = Encoding.UTF8.GetBytes("id,name\n1,Ana\n");
        var device = LoggedDevice().OnBytes("export_object.fcgi", csv, "text/csv");
        using var client = new AccessClient(Options, device.CreateClient());

        Assert.Equal(csv, await client.ExportObjectCsvAsync("users", new[] { "id", "name" }));
        await client.ExportObjectCsvAsync("users");

        var bodies = device.RequestsTo("export_object.fcgi").Select(r => r.Body).ToList();
        Assert.Equal(new[] { """{"object":"users","columns":["id","name"]}""", """{"object":"users"}""" }, bodies);
    }

    [Fact]
    public async Task ExportCustomTablesMetadataCsv_uses_get_and_returns_bytes()
    {
        var csv = Encoding.UTF8.GetBytes("cid_metadata\n");
        var device = LoggedDevice().OnBytes("export_custom_tables_metadata.fcgi", csv, "text/csv");
        using var client = new AccessClient(Options, device.CreateClient());

        var result = await client.ExportCustomTablesMetadataCsvAsync();

        Assert.Equal(csv, result);
        Assert.Equal(HttpMethod.Get, device.RequestsTo("export_custom_tables_metadata.fcgi").Single().Method);
    }
}
