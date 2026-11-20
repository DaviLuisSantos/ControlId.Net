using System.Text.Json;
using System.Text.RegularExpressions;
using Xunit;

namespace ControlId.Net.Tests;

/// <summary>
/// Guarda de cobertura da Access API: todo endpoint do catálogo oficial (<c>docs/controlid-api/endpoints.json</c>)
/// precisa aparecer como literal de string em <c>src/</c> ou estar em <c>Cobertura/pendentes.txt</c> (ainda por fazer)
/// ou <c>Cobertura/nao-cobrir.txt</c> (decisão de não cobrir). A lista de pendentes só encolhe.
/// </summary>
public sealed class EndpointCoverageGuardTests
{
    // Literal que é só o nome do endpoint, com ou sem "/" inicial e ".fcgi" (como o código guarda as rotas).
    private static readonly Regex EndpointLiteral = new(@"""/?([A-Za-z0-9_]+)(?:\.fcgi)?""", RegexOptions.Compiled);

    [Fact]
    public void Every_documented_endpoint_is_covered_or_listed()
    {
        var root = FindRoot();
        var documented = ReadDocumentedEndpoints(root);
        var covered = ReadCoveredNames(root);
        var pending = ReadList(root, "pendentes.txt");
        var skipped = ReadList(root, "nao-cobrir.txt");

        var uncovered = documented
            .Where(e => !covered.Contains(e) && !pending.Contains(e) && !skipped.Contains(e))
            .OrderBy(e => e, StringComparer.Ordinal)
            .ToList();
        Assert.True(uncovered.Count == 0,
            "Endpoints documentados sem cobertura em src/ e fora de pendentes.txt/nao-cobrir.txt: " + string.Join(", ", uncovered));
    }

    [Fact]
    public void Listed_endpoints_are_not_already_covered()
    {
        var root = FindRoot();
        var covered = ReadCoveredNames(root);

        var stale = ReadList(root, "pendentes.txt").Concat(ReadList(root, "nao-cobrir.txt"))
            .Where(covered.Contains)
            .OrderBy(e => e, StringComparer.Ordinal)
            .ToList();
        Assert.True(stale.Count == 0,
            "Endpoints já cobertos em src/ que ainda constam em pendentes.txt/nao-cobrir.txt (remova da lista): " + string.Join(", ", stale));
    }

    [Fact]
    public void Listed_endpoints_exist_in_the_documentation()
    {
        var root = FindRoot();
        var documented = ReadDocumentedEndpoints(root);

        var unknown = ReadList(root, "pendentes.txt").Concat(ReadList(root, "nao-cobrir.txt"))
            .Where(e => !documented.Contains(e))
            .OrderBy(e => e, StringComparer.Ordinal)
            .ToList();
        Assert.True(unknown.Count == 0,
            "Itens das listas que não existem em endpoints.json: " + string.Join(", ", unknown));
    }

    private static string FindRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "ControlId.Net.sln")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException("ControlId.Net.sln não encontrado acima de " + AppContext.BaseDirectory);
    }

    private static HashSet<string> ReadDocumentedEndpoints(string root)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "docs", "controlid-api", "endpoints.json")));
        var set = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in doc.RootElement.EnumerateObject())
        {
            set.Add(Normalize(property.Name));
        }

        return set;
    }

    private static HashSet<string> ReadCoveredNames(string root)
    {
        var set = new HashSet<string>(StringComparer.Ordinal);
        var src = Path.Combine(root, "src");
        foreach (var file in Directory.EnumerateFiles(src, "*.cs", SearchOption.AllDirectories))
        {
            var relative = file.Substring(src.Length);
            var parts = relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (parts.Any(p => p.Equals("bin", StringComparison.OrdinalIgnoreCase) || p.Equals("obj", StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            foreach (Match match in EndpointLiteral.Matches(File.ReadAllText(file)))
            {
                set.Add(match.Groups[1].Value);
            }
        }

        return set;
    }

    private static HashSet<string> ReadList(string root, string fileName)
    {
        var path = Path.Combine(root, "tests", "ControlId.Net.Tests", "Cobertura", fileName);
        var set = new HashSet<string>(StringComparer.Ordinal);
        foreach (var raw in File.ReadAllLines(path))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line[0] == '#')
            {
                continue;
            }

            set.Add(Normalize(line));
        }

        return set;
    }

    private static string Normalize(string name)
    {
        name = name.Trim().TrimStart('/');
        return name.EndsWith(".fcgi", StringComparison.Ordinal) ? name.Substring(0, name.Length - 5) : name;
    }
}
