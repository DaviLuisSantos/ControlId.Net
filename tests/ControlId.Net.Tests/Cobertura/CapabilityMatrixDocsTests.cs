using System.Text.RegularExpressions;
using ControlId.Internal;

namespace ControlId.Tests.Cobertura;

/// <summary>
/// A matriz de comandos ausentes por firmware (<see cref="FirmwareCapabilityMatrix"/>) vem das seções "Observado num
/// iDFace real" de <c>docs/COBERTURA.md</c>. Ao observar outro firmware, atualize o documento e a matriz juntos.
/// </summary>
public class CapabilityMatrixDocsTests
{
    [Fact]
    public void Matrix_for_6_9_12_matches_the_documented_endpoints()
    {
        var section = Section("## Observado num iDFace real (firmware 6.9.12)");
        var text = Between(section, "- Endpoints:", "- Objetos:");

        // "upgrade_*_face_templates" só falha acima de 10 mil modelos: não é um comando ausente.
        var documented = Backticked(text).Where(t => t.IndexOf('*') < 0).ToHashSet(StringComparer.Ordinal);

        Assert.Equal(documented.OrderBy(c => c, StringComparer.Ordinal), FirmwareCapabilityMatrix.Firmware6912.OrderBy(c => c, StringComparer.Ordinal));
    }

    [Fact]
    public void Matrix_for_6_13_1_matches_the_documented_endpoints()
    {
        var section = Section("## Observado num iDFace real (firmware 6.13.1)");
        var text = Between(section, "**Não existem neste modelo e firmware:**", ", os objetos");

        var documented = Backticked(text);

        Assert.Equal(documented.OrderBy(c => c, StringComparer.Ordinal), FirmwareCapabilityMatrix.Firmware61301.OrderBy(c => c, StringComparer.Ordinal));
    }

    [Fact]
    public void Every_firmware_in_the_matrix_has_a_documented_section()
    {
        var doc = File.ReadAllText(Path.Combine(FindRoot(), "docs", "COBERTURA.md"));

        foreach (var (firmware, _) in FirmwareCapabilityMatrix.All)
        {
            Assert.Contains("## Observado num iDFace real (firmware " + firmware + ")", doc);
        }
    }

    private static string Section(string heading)
    {
        var doc = File.ReadAllText(Path.Combine(FindRoot(), "docs", "COBERTURA.md")).Replace("\r\n", "\n");
        var start = doc.IndexOf(heading, StringComparison.Ordinal);
        Assert.True(start >= 0, "Seção não encontrada em docs/COBERTURA.md: " + heading);
        var end = doc.IndexOf("\n## ", start + heading.Length, StringComparison.Ordinal);
        return end < 0 ? doc.Substring(start) : doc.Substring(start, end - start);
    }

    private static string Between(string text, string from, string to)
    {
        var start = text.IndexOf(from, StringComparison.Ordinal);
        Assert.True(start >= 0, "Trecho não encontrado em docs/COBERTURA.md: " + from);
        start += from.Length;
        var end = text.IndexOf(to, start, StringComparison.Ordinal);
        Assert.True(end > start, "Trecho não encontrado em docs/COBERTURA.md: " + to);
        return text.Substring(start, end - start);
    }

    private static HashSet<string> Backticked(string text) =>
        Regex.Matches(text, "`([^`]+)`").Cast<Match>().Select(m => m.Groups[1].Value).ToHashSet(StringComparer.Ordinal);

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
}
