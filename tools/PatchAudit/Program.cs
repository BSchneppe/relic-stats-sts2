using System.Reflection;
using System.Text.RegularExpressions;

namespace RelicStats.Tools.PatchAudit;

/// <summary>
/// Verifies that every Harmony patch target in the mod actually resolves against a given
/// version of the game assembly.
/// </summary>
/// <remarks>
/// Harmony resolves patch targets with DeclaredMethod, which returns null when the type only
/// inherits the method. A renamed hook therefore still compiles — the base-class virtual keeps the
/// name valid — but throws at patch time, leaving the relic recording nothing. This checks
/// declaration, not just compilation.
///
/// Usage: dotnet run --project tools/PatchAudit -- &lt;mod-source-dir&gt; &lt;sts2.dll&gt; [more.dll...]
/// </remarks>
internal static class Program
{
    private static int Main(string[] args)
    {
        // --allow-missing exempts targets a reference assembly cannot show: refasmer keeps only the
        // public surface, so patches on private methods look missing there but resolve in game.
        var allowMissing = new HashSet<string>(StringComparer.Ordinal);
        var positional = new List<string>();
        for (var i = 0; i < args.Length; i++)
        {
            if (args[i] == "--allow-missing" && i + 1 < args.Length)
                allowMissing.Add(args[++i]);
            else
                positional.Add(args[i]);
        }

        if (positional.Count < 2)
        {
            Console.Error.WriteLine(
                "usage: PatchAudit <mod-source-dir> <sts2.dll> [additional-assembly...] " +
                "[--allow-missing Type.Member]...");
            return 2;
        }

        var sourceDir = positional[0];
        var gameAssemblyPath = positional[1];

        var sites = PatchSite.ScanDirectory(sourceDir);
        if (sites.Count == 0)
        {
            Console.Error.WriteLine($"No patch sites found under {sourceDir} — the scanner is probably broken.");
            return 2;
        }

        using var resolver = new GameAssembly(gameAssemblyPath, positional.Skip(2));
        var failures = new List<string>();

        // Rule 1: every declaration site must be able to bind.
        foreach (var site in sites)
        {
            var type = resolver.FindType(site.TypeName);
            if (type == null)
            {
                failures.Add($"{site.Where}: type '{site.TypeName}' does not exist");
                continue;
            }

            if (site.Optional) continue;
            if (site.CandidateMethods.Any(name => allowMissing.Contains($"{site.TypeName}.{name}"))) continue;

            if (!site.CandidateMethods.Any(name => resolver.Declares(type, name)))
            {
                var tried = string.Join(", ", site.CandidateMethods);
                failures.Add($"{site.Where}: {site.TypeName} declares none of [{tried}]");
            }
        }

        // Rule 2: every relic keeps at least one live target across all its sites, optional ones
        // included — catches a relic whose per-version alternatives have all gone stale.
        foreach (var group in sites.GroupBy(site => site.TypeName))
        {
            var type = resolver.FindType(group.Key);
            if (type == null || !resolver.IsRelicModel(type)) continue;

            var anyLive = group.SelectMany(site => site.CandidateMethods)
                               .Any(name => resolver.Declares(type, name));
            if (!anyLive)
                failures.Add($"relic {group.Key}: no patch target resolves — its stats would stay empty");
        }

        var relicCount = sites.Select(s => s.TypeName)
                              .Distinct()
                              .Count(name => resolver.FindType(name) is { } t && resolver.IsRelicModel(t));

        Console.WriteLine($"Audited {sites.Count} patch sites across {relicCount} relics " +
                          $"against {Path.GetFileName(gameAssemblyPath)}.");

        if (failures.Count == 0)
        {
            Console.WriteLine("All patch targets resolve.");
            return 0;
        }

        Console.Error.WriteLine($"\n{failures.Count} unresolved patch target(s):");
        foreach (var failure in failures.OrderBy(f => f))
            Console.Error.WriteLine($"  {failure}");
        return 1;
    }
}

/// <summary>One place in the mod source that names a method to patch.</summary>
internal sealed record PatchSite(string Where, string TypeName, string[] CandidateMethods, bool Optional)
{
    // [HarmonyPatch(typeof(Relic), nameof(Relic.Hook))]
    private static readonly Regex AttributeRegex = new(
        @"\[HarmonyPatch\(\s*typeof\(\s*(?<type>[\w.]+)\s*\)\s*,\s*(?<names>(?:nameof\([^)]*\)|""\w+""))",
        RegexOptions.Compiled);

    // PatchTarget.FirstDeclared(typeof(Relic), nameof(Relic.New), nameof(Relic.Old))
    private static readonly Regex ResolverRegex = new(
        @"PatchTarget\.(?<kind>FirstDeclared|DeclaredOrNone)\(\s*typeof\(\s*(?<type>[\w.]+)\s*\)\s*,(?<names>.*?)\)\s*;",
        RegexOptions.Compiled | RegexOptions.Singleline);

    private static readonly Regex NameRegex = new(
        @"nameof\(\s*[\w.]*?(?<name>\w+)\s*\)|""(?<name>\w+)""",
        RegexOptions.Compiled);

    // Documentation comments spell out patch attributes as examples; those are not real sites.
    private static readonly Regex CommentRegex = new(
        @"/\*.*?\*/|//[^\n]*",
        RegexOptions.Compiled | RegexOptions.Singleline);

    public static List<PatchSite> ScanDirectory(string sourceDir)
    {
        var sites = new List<PatchSite>();

        foreach (var file in Directory.EnumerateFiles(sourceDir, "*.cs", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(sourceDir, file);
            // Skip the audit's own sources and anything not shipped in the mod.
            if (relative.StartsWith("tools") || relative.StartsWith("tests") || relative.StartsWith("obj"))
                continue;

            var text = StripComments(File.ReadAllText(file));

            foreach (Match match in AttributeRegex.Matches(text))
                sites.Add(Create(relative, text, match, optional: false));

            foreach (Match match in ResolverRegex.Matches(text))
                sites.Add(Create(relative, text, match, optional: match.Groups["kind"].Value == "DeclaredOrNone"));
        }

        return sites;
    }

    /// <summary>Blanks comments while preserving offsets, so reported line numbers stay correct.</summary>
    private static string StripComments(string text) =>
        CommentRegex.Replace(text, match =>
            new string(match.Value.Select(c => c == '\n' ? '\n' : ' ').ToArray()));

    private static PatchSite Create(string relativePath, string text, Match match, bool optional)
    {
        var line = text.Take(match.Index).Count(c => c == '\n') + 1;
        var names = NameRegex.Matches(match.Groups["names"].Value)
                             .Select(m => m.Groups["name"].Value)
                             .Where(n => n.Length > 0)
                             .ToArray();
        var typeName = match.Groups["type"].Value.Split('.').Last();
        return new PatchSite($"{relativePath}:{line}", typeName, names, optional);
    }
}

/// <summary>Metadata-only view of the game assembly, so no game runtime is needed.</summary>
internal sealed class GameAssembly : IDisposable
{
    private readonly MetadataLoadContext _context;
    private readonly Assembly _assembly;
    private readonly Dictionary<string, Type?> _typeCache = new();

    public GameAssembly(string gameAssemblyPath, IEnumerable<string> additionalAssemblies)
    {
        var candidates = new List<string> { Path.GetFullPath(gameAssemblyPath) };
        candidates.AddRange(additionalAssemblies);
        // The framework, ahead of the game directory: a Godot install ships its own copies of
        // mscorlib and friends, and MetadataLoadContext rejects a duplicate assembly identity.
        candidates.AddRange(Directory.GetFiles(Path.GetDirectoryName(typeof(object).Assembly.Location)!, "*.dll"));
        // Siblings of the game assembly (GodotSharp and friends), which its base types reference.
        var gameDir = Path.GetDirectoryName(Path.GetFullPath(gameAssemblyPath));
        if (gameDir != null) candidates.AddRange(Directory.GetFiles(gameDir, "*.dll"));

        var paths = candidates
            .GroupBy(Path.GetFileName)
            .Select(group => group.First())
            .ToList();

        _context = new MetadataLoadContext(new PathAssemblyResolver(paths));
        _assembly = _context.LoadFromAssemblyPath(Path.GetFullPath(gameAssemblyPath));
    }

    public Type? FindType(string simpleName)
    {
        if (_typeCache.TryGetValue(simpleName, out var cached)) return cached;

        // Prefer the relic namespace when a simple name is used by more than one type
        // (LostWisp, for instance, is both a relic and an event).
        var matches = _assembly.GetTypes().Where(t => t.Name == simpleName).ToList();
        var type = matches.FirstOrDefault(t => t.Namespace?.Contains(".Relics") == true)
                   ?? matches.FirstOrDefault();

        _typeCache[simpleName] = type;
        return type;
    }

    private const BindingFlags Declared =
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance |
        BindingFlags.Static | BindingFlags.DeclaredOnly;

    /// <summary>
    /// Declared on the type itself; inherited does not count, matching Harmony. Properties count,
    /// since patches can target an accessor via MethodType.Getter/Setter.
    /// </summary>
    public bool Declares(Type type, string memberName) =>
        type.GetMethods(Declared).Any(method => method.Name == memberName) ||
        type.GetProperties(Declared).Any(property => property.Name == memberName);

    public bool IsRelicModel(Type type)
    {
        try
        {
            for (var current = type.BaseType; current != null; current = current.BaseType)
                if (current.Name == "RelicModel") return true;
        }
        catch (FileNotFoundException)
        {
            // A base type living in an assembly we cannot resolve is not RelicModel.
        }
        return false;
    }

    public void Dispose() => _context.Dispose();
}
