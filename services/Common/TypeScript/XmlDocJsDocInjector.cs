using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Common.TypeScript;

/// <summary>
/// Reads C# XML documentation files and injects JSDoc comments into TypeGen-generated TypeScript files.
/// </summary>
internal static class XmlDocJsDocInjector
{
    // Matches: export interface ChannelDefinition {
    //          export enum MathType {
    private static readonly Regex TypeDeclarationRegex =
        new(@"^export\s+(interface|enum)\s+(\w+)\s*\{", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // Matches a property line in an interface, e.g.:   id: string;
    // or an enum member line, e.g.:   None = 0,
    private static readonly Regex MemberLineRegex =
        new(@"^\s{4}(\w+)\s*[=:?]", RegexOptions.Compiled);

    internal static void InjectJsDocs(IEnumerable<Type> types, string outputDirectory)
    {
        // Build lookup: assemblyLocation -> XDocument
        var xmlDocCache = new Dictionary<string, XDocument?>(StringComparer.OrdinalIgnoreCase);

        // summary lookup keyed by "Namespace.TypeName" and "Namespace.TypeName.MemberName"
        var summaries = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var type in types)
        {
            var xmlPath = Path.ChangeExtension(type.Assembly.Location, ".xml");
            if (!xmlDocCache.TryGetValue(xmlPath, out var xdoc))
            {
                xdoc = File.Exists(xmlPath) ? XDocument.Load(xmlPath) : null;
                xmlDocCache[xmlPath] = xdoc;
            }

            if (xdoc is null)
                continue;

            LoadSummaries(xdoc, type, summaries);
        }

        if (summaries.Count == 0)
            return;

        // Build a map from kebab-case filename (without extension) -> Type
        var typeByFileName = types.ToDictionary(
            t => ToKebabCase(t.Name),
            StringComparer.OrdinalIgnoreCase);

        foreach (var tsFile in Directory.EnumerateFiles(outputDirectory, "*.ts"))
        {
            var key = Path.GetFileNameWithoutExtension(tsFile);
            if (!typeByFileName.TryGetValue(key, out var type))
                continue;

            var typeKey = $"{type.FullName}";
            InjectIntoFile(tsFile, type, typeKey, summaries);
        }
    }

    private static void LoadSummaries(XDocument xdoc, Type type, Dictionary<string, string> summaries)
    {
        var members = xdoc.Descendants("member");
        var typeFullName = type.FullName!;

        foreach (var member in members)
        {
            var name = (string?)member.Attribute("name");
            if (name is null)
                continue;

            var summaryEl = member.Element("summary");
            if (summaryEl is null)
                continue;

            var text = CleanSummaryText(summaryEl);
            if (string.IsNullOrWhiteSpace(text))
                continue;

            // T:Namespace.TypeName  -> store as "Namespace.TypeName"
            if (name.StartsWith("T:", StringComparison.Ordinal))
            {
                var key = name[2..];
                summaries[key] = text;
            }
            // P:Namespace.TypeName.PropertyName or F:Namespace.TypeName.FieldName
            else if (name.StartsWith("P:", StringComparison.Ordinal) || name.StartsWith("F:", StringComparison.Ordinal))
            {
                var key = name[2..];
                summaries[key] = text;
            }
        }
    }

    private static void InjectIntoFile(string tsFile, Type type, string typeKey, Dictionary<string, string> summaries)
    {
        var lines = File.ReadAllLines(tsFile);
        var result = new List<string>(lines.Length + 10);
        string? currentTypeName = null;

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];

            // Check for type declaration
            var typeMatch = TypeDeclarationRegex.Match(line);
            if (typeMatch.Success)
            {
                currentTypeName = typeMatch.Groups[2].Value;

                // Find matching C# type by name (across all known types)
                if (summaries.TryGetValue(typeKey, out var typeSummary) &&
                    string.Equals(currentTypeName, type.Name, StringComparison.Ordinal) &&
                    !LastLineIsJsDocClose(result))
                {
                    result.AddRange(BuildJsDocBlock(typeSummary, indent: string.Empty));
                }

                result.Add(line);
                continue;
            }

            // Check for member line inside a type block
            if (currentTypeName is not null)
            {
                var memberMatch = MemberLineRegex.Match(line);
                if (memberMatch.Success)
                {
                    var tsMemberName = memberMatch.Groups[1].Value;
                    // Convert camelCase TS name back to PascalCase to match C# member name
                    var csharpMemberName = ToPascalCase(tsMemberName);
                    var memberKey = $"{typeKey}.{csharpMemberName}";

                    if (summaries.TryGetValue(memberKey, out var memberSummary) && !LastLineIsJsDocClose(result))
                    {
                        var indent = GetIndent(line);
                        result.AddRange(BuildJsDocBlock(memberSummary, indent));
                    }
                }

                if (line == "}")
                    currentTypeName = null;
            }

            result.Add(line);
        }

        File.WriteAllLines(tsFile, result, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    private static IEnumerable<string> BuildJsDocBlock(string summary, string indent)
    {
        var lines = summary.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (lines.Length == 1)
        {
            yield return $"{indent}/** {lines[0]} */";
        }
        else
        {
            yield return $"{indent}/**";
            foreach (var l in lines)
                yield return $"{indent} * {l}";
            yield return $"{indent} */";
        }
    }

    private static string CleanSummaryText(XElement summaryEl)
    {
        // Resolve <see cref="..."/> to just the type/member short name
        foreach (var see in summaryEl.Descendants("see").ToList())
        {
            var cref = (string?)see.Attribute("cref");
            if (cref is not null)
            {
                // e.g. "T:System.Guid" or "P:Channels.ChannelDefinition.Id" -> take last segment
                var shortName = cref.Contains('.')
                    ? cref[(cref.LastIndexOf('.') + 1)..]
                    : cref.TrimStart('T', 'P', 'F', 'M', ':');
                see.ReplaceWith(new XText($"`{shortName}`"));
            }
        }

        // Collapse whitespace
        var raw = summaryEl.Value;
        var normalized = string.Join(" ", raw.Split(['\r', '\n', '\t', ' '], StringSplitOptions.RemoveEmptyEntries));
        return normalized;
    }

    private static bool LastLineIsJsDocClose(List<string> lines)
        {
            for (var i = lines.Count - 1; i >= 0; i--)
            {
                var trimmed = lines[i].TrimEnd();
                if (trimmed.Length == 0)
                    continue;
                return trimmed.EndsWith("*/", StringComparison.Ordinal);
            }
            return false;
        }

        private static string GetIndent(string line)
    {
        var count = 0;
        foreach (var c in line)
        {
            if (c == ' ') count++;
            else break;
        }
        return new string(' ', count);
    }

    private static string ToPascalCase(string camelCase) =>
        string.IsNullOrEmpty(camelCase) ? camelCase : char.ToUpperInvariant(camelCase[0]) + camelCase[1..];

    private static string ToKebabCase(string name) =>
        string.Concat(name.Select((c, i) => i > 0 && char.IsUpper(c) ? $"-{char.ToLower(c)}" : $"{char.ToLower(c)}"));
}
