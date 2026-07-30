using System.Text.Json;

namespace Loom.Templates.Tests;

/// <summary>
/// The template manifests, checked against the content they claim to rewrite.
/// </summary>
/// <remarks>
/// Scaffolding a solution and building it is CI's job, because it needs a pack, an install and a
/// restore from nuget.org. What is checked here is everything that can be wrong <em>before</em> that
/// point and would otherwise only show up as generated output that does not compile.
/// <para>
/// The token check exists because a substitution that silently does nothing is the failure mode this
/// package actually had: a symbol declared with the wrong mechanism left <c>HostProject</c> in the
/// generated file, and nothing failed until a scaffolded solution was compiled.
/// </para>
/// </remarks>
public sealed class TemplateManifestTests
{
    private static readonly string TemplatesRoot = LocateTemplatesRoot();

    public static IEnumerable<string> Templates()
    {
        foreach (string directory in Directory.EnumerateDirectories(TemplatesRoot))
        {
            yield return Path.GetFileName(directory);
        }
    }

    [Test]
    [MethodDataSource(nameof(Templates))]
    public async Task The_Manifest_Is_Valid_Json_With_The_Members_Dotnet_New_Requires(string template)
    {
        JsonElement manifest = await ReadManifestAsync(template);

        foreach (string member in (string[])["identity", "name", "shortName", "sourceName"])
        {
            await Assert.That(manifest.TryGetProperty(member, out JsonElement value) &&
                value.ValueKind is JsonValueKind.String &&
                !string.IsNullOrWhiteSpace(value.GetString()))
                .IsTrue();
        }
    }

    [Test]
    [MethodDataSource(nameof(Templates))]
    public async Task The_Short_Name_Matches_The_Directory(string template)
    {
        JsonElement manifest = await ReadManifestAsync(template);

        await Assert.That(manifest.GetProperty("shortName").GetString()).IsEqualTo(template);
    }

    [Test]
    [MethodDataSource(nameof(Templates))]
    public async Task The_Source_Name_Occurs_In_The_Content_It_Renames(string template)
    {
        JsonElement manifest = await ReadManifestAsync(template);
        string sourceName = manifest.GetProperty("sourceName").GetString()!;

        await Assert.That(await OccurrencesAsync(template, sourceName)).IsGreaterThan(0);
    }

    [Test]
    [MethodDataSource(nameof(Templates))]
    public async Task Every_Token_A_Symbol_Rewrites_Actually_Occurs_In_The_Content(string template)
    {
        JsonElement manifest = await ReadManifestAsync(template);

        if (!manifest.TryGetProperty("symbols", out JsonElement symbols))
        {
            return;
        }

        foreach (JsonProperty symbol in symbols.EnumerateObject())
        {
            if (!symbol.Value.TryGetProperty("replaces", out JsonElement replaces))
            {
                continue;
            }

            string token = replaces.GetString()!;

            // A symbol whose token appears nowhere rewrites nothing. That is not a harmless typo: the
            // generated file keeps whatever placeholder was written instead.
            await Assert.That(await OccurrencesAsync(template, token)).IsGreaterThan(0);
        }
    }

    [Test]
    [MethodDataSource(nameof(Templates))]
    public async Task The_Assembled_Guidance_Is_Present_And_Carries_No_Internal_Marker(string template)
    {
        string guidance = Path.Combine(TemplatesRoot, template, "AGENTS.md");

        await Assert.That(File.Exists(guidance)).IsTrue();

        string content = await File.ReadAllTextAsync(guidance);

        await Assert.That(content).StartsWith("# AGENTS.md");
        await Assert.That(content).DoesNotContain("LOOM-TEMPLATE");
    }

    private static async Task<JsonElement> ReadManifestAsync(string template)
    {
        string path = Path.Combine(TemplatesRoot, template, ".template.config", "template.json");
        await using FileStream stream = File.OpenRead(path);
        using JsonDocument document = await JsonDocument.ParseAsync(stream);

        return document.RootElement.Clone();
    }

    private static async Task<int> OccurrencesAsync(string template, string token)
    {
        int found = 0;

        foreach (string file in Directory.EnumerateFiles(
            Path.Combine(TemplatesRoot, template), "*", SearchOption.AllDirectories))
        {
            // The manifest is excluded on purpose. Every token appears there as the "replaces" value,
            // so counting it would make this test pass for a token that occurs nowhere else — which is
            // exactly the case it exists to catch.
            if (file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                || file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                || file.Contains($"{Path.DirectorySeparatorChar}.template.config{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            {
                continue;
            }

            if (file.Contains(token, StringComparison.Ordinal)
                || (await File.ReadAllTextAsync(file)).Contains(token, StringComparison.Ordinal))
            {
                found++;
            }
        }

        return found;
    }

    // Walks up rather than assuming a build layout: the tests run from bin/, and the content being
    // checked is source that is never copied there.
    private static string LocateTemplatesRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);

        while (directory is not null)
        {
            string candidate = Path.Combine(directory.FullName, "src", "Loom.Templates", "templates");

            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate src/Loom.Templates/templates.");
    }
}
