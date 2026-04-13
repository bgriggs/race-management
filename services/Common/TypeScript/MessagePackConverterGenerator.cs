using System.Reflection;
using System.Text;

namespace Common.TypeScript;

internal static class MessagePackConverterGenerator
{
    private const string OutputFileName = "message-pack-converter.ts";

    internal static void Generate(IEnumerable<Type> types, string outputDirectory)
    {
        var classTypes = types.Where(t => t.IsClass && !t.IsAbstract).Distinct().ToList();
        if (classTypes.Count == 0)
            return;

        var decodableTypes = classTypes.ToHashSet();

        var sb = new StringBuilder();
        sb.AppendLine("/**");
        sb.AppendLine(" * This is a TypeGen auto-generated file.");
        sb.AppendLine(" * Any changes made to this file can be lost when this file is regenerated.");
        sb.AppendLine(" */");
        sb.AppendLine();

        sb.AppendLine("import { decode, encode } from \"@msgpack/msgpack\";");

        foreach (var type in classTypes)
            sb.AppendLine($"import {{ {type.Name} }} from \"./{ToKebabCase(type.Name)}\";");

        var referencedTypes = classTypes
            .SelectMany(t => t.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .SelectMany(p => CollectReferencedTypes(p.PropertyType)))
            .Distinct()
            .Where(t => !classTypes.Contains(t))
            .Where(t => t.IsEnum)
            .OrderBy(t => t.Name)
            .ToList();

        foreach (var type in referencedTypes)
            sb.AppendLine($"import {{ {type.Name} }} from \"./{ToKebabCase(type.Name)}\";");

        sb.AppendLine();

        sb.AppendLine("export function decodeMessagePack<T>(bytes: Uint8Array): T {");
        sb.AppendLine("    return decode(bytes) as T;");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("export function encodeMessagePack<T>(value: T): Uint8Array {");
        sb.AppendLine("    return encode(value);");
        sb.AppendLine("}");
        sb.AppendLine();

        foreach (var type in classTypes)
        {
            var fn = ToCamelCase(type.Name);
            sb.AppendLine($"export function {fn}FromMessagePack(obj: Record<string, unknown>): {type.Name} {{");
            sb.AppendLine("    return {");

            var props = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanRead)
                .ToList();

            var nullabilityContext = new NullabilityInfoContext();

            foreach (var prop in props)
            {
                var tsName = ToCamelCase(prop.Name);
                var source = $"obj[\"{prop.Name}\"]";
                bool isNullable = Nullable.GetUnderlyingType(prop.PropertyType) != null
                                  || nullabilityContext.Create(prop).WriteState == NullabilityState.Nullable;
                var expr = GetMessagePackExpression(prop.PropertyType, source, decodableTypes, isNullable);
                sb.AppendLine($"        {tsName}: {expr},");
            }

            sb.AppendLine("    };");
            sb.AppendLine("}");
            sb.AppendLine();

            sb.AppendLine($"export function decode{type.Name}MessagePack(bytes: Uint8Array): {type.Name} {{");
            sb.AppendLine($"    return {fn}FromMessagePack(decode(bytes) as Record<string, unknown>);");
            sb.AppendLine("}");
            sb.AppendLine();
        }

        Directory.CreateDirectory(outputDirectory);
        File.WriteAllText(Path.Combine(outputDirectory, OutputFileName), sb.ToString());
    }

    private static string GetMessagePackExpression(Type type, string accessor, HashSet<Type> decodableTypes, bool isNullable)
    {
        var underlying = Nullable.GetUnderlyingType(type);
        if (underlying != null)
            return $"{accessor} != null ? {GetMessagePackExpression(underlying, accessor, decodableTypes, false)} : null";

        if (type == typeof(DateTime) || type == typeof(DateTimeOffset))
            return isNullable
                ? $"{accessor} != null ? new Date({accessor} as string | number | Date) : null"
                : $"new Date({accessor} as string | number | Date)";

        var collectionElementType = GetCollectionElementType(type);
        if (collectionElementType is not null)
        {
            var arrayAccessor = $"({accessor} as unknown[])";
            var mapped = $"{arrayAccessor}.map(v => {GetMessagePackExpression(collectionElementType, "v", decodableTypes, false)})";
            return isNullable ? $"{accessor} != null ? {mapped} : null" : mapped;
        }

        if (decodableTypes.Contains(type))
        {
            var call = $"{ToCamelCase(type.Name)}FromMessagePack({accessor} as Record<string, unknown>)";
            return isNullable ? $"{accessor} != null ? {call} : null" : call;
        }

        var cast = $"{accessor} as {GetTsType(type)}";
        return isNullable ? $"{accessor} != null ? {cast} : null" : cast;
    }

    private static Type? GetCollectionElementType(Type type)
    {
        if (type.IsArray)
            return type.GetElementType();

        if (type.IsGenericType)
        {
            var genericTypeDefinition = type.GetGenericTypeDefinition();
            if (genericTypeDefinition == typeof(List<>))
                return type.GetGenericArguments()[0];
            if (genericTypeDefinition == typeof(IEnumerable<>))
                return type.GetGenericArguments()[0];
        }

        return null;
    }

    private static IEnumerable<Type> CollectReferencedTypes(Type type)
    {
        var underlying = Nullable.GetUnderlyingType(type);
        if (underlying != null)
            return CollectReferencedTypes(underlying);

        if (type.IsArray)
            return CollectReferencedTypes(type.GetElementType()!);

        if (type.IsGenericType)
        {
            var genericTypeDefinition = type.GetGenericTypeDefinition();

            if (genericTypeDefinition == typeof(List<>) || genericTypeDefinition == typeof(IEnumerable<>))
                return CollectReferencedTypes(type.GetGenericArguments()[0]);

            if (genericTypeDefinition == typeof(Dictionary<,>))
                return CollectReferencedTypes(type.GetGenericArguments()[0])
                    .Concat(CollectReferencedTypes(type.GetGenericArguments()[1]));

            if (genericTypeDefinition == typeof(ValueTuple<,>))
                return CollectReferencedTypes(type.GetGenericArguments()[0])
                    .Concat(CollectReferencedTypes(type.GetGenericArguments()[1]));
        }

        if (IsPrimitiveLike(type))
            return [];

        return [type];
    }

    private static bool IsPrimitiveLike(Type type)
    {
        return type == typeof(int)
               || type == typeof(uint)
               || type == typeof(long)
               || type == typeof(ulong)
               || type == typeof(short)
               || type == typeof(ushort)
               || type == typeof(byte)
               || type == typeof(sbyte)
               || type == typeof(double)
               || type == typeof(float)
               || type == typeof(decimal)
               || type == typeof(string)
               || type == typeof(bool)
               || type == typeof(Guid)
               || type == typeof(TimeSpan)
               || type == typeof(DateTime)
               || type == typeof(DateTimeOffset);
    }

    private static string GetTsType(Type type)
    {
        var underlying = Nullable.GetUnderlyingType(type);
        if (underlying != null)
            return $"{GetTsType(underlying)} | null";

        if (type == typeof(int)
            || type == typeof(uint)
            || type == typeof(long)
            || type == typeof(ulong)
            || type == typeof(short)
            || type == typeof(ushort)
            || type == typeof(byte)
            || type == typeof(sbyte)
            || type == typeof(double)
            || type == typeof(float)
            || type == typeof(decimal))
            return "number";

        if (type == typeof(string) || type == typeof(Guid) || type == typeof(TimeSpan))
            return "string";

        if (type == typeof(bool))
            return "boolean";

        if (type == typeof(DateTime) || type == typeof(DateTimeOffset))
            return "Date";

        if (type.IsArray)
            return $"{GetTsType(type.GetElementType()!)}[]";

        if (type.IsGenericType)
        {
            var genericTypeDefinition = type.GetGenericTypeDefinition();
            if (genericTypeDefinition == typeof(List<>) || genericTypeDefinition == typeof(IEnumerable<>))
                return $"{GetTsType(type.GetGenericArguments()[0])}[]";

            if (genericTypeDefinition == typeof(Dictionary<,>))
            {
                var args = type.GetGenericArguments();
                return $"{{ [key: {GetTsType(args[0])}]: {GetTsType(args[1])}; }}";
            }

            if (genericTypeDefinition == typeof(ValueTuple<,>))
            {
                var args = type.GetGenericArguments();
                return $"[{GetTsType(args[0])}, {GetTsType(args[1])}]";
            }
        }

        return type.Name;
    }

    private static string ToCamelCase(string name) =>
        string.IsNullOrEmpty(name) ? name : char.ToLowerInvariant(name[0]) + name[1..];

    private static string ToKebabCase(string name) =>
        string.Concat(name.Select((c, i) => i > 0 && char.IsUpper(c) ? $"-{char.ToLower(c)}" : $"{char.ToLower(c)}"));
}
