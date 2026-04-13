using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Routing;

namespace RaceManagementService.Routing;

public partial class KebabCaseParameterTransformer : IOutboundParameterTransformer
{
    private static readonly Regex WordBoundary = new("([a-z])([A-Z])", RegexOptions.Compiled);

    public string? TransformOutbound(object? value)
    {
        if (value is null) return null;
        return WordBoundary.Replace(value.ToString()!, "$1-$2").ToLowerInvariant();
    }
}
