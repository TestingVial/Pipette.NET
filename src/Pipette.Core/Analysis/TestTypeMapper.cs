namespace Pipette.Core.Analysis;

/// <summary>Maps a TestingVial.NET test-type string to the vial <c>type</c> value.</summary>
public static class TestTypeMapper
{
    public static string Map(string testType)
    {
        var trimmed = testType.Trim();
        return trimmed.ToUpperInvariant() switch
        {
            "UNIT" => "unit",
            "INTEGRATION" => "integration",
            "E2E" or "ENDTOEND" => "e2e",
            _ => trimmed.ToLowerInvariant(),
        };
    }
}
