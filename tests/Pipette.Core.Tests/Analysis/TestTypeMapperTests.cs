using Pipette.Core.Analysis;

namespace Pipette.Core.Tests.Analysis;

public class TestTypeMapperTests
{
    [Theory]
    [InlineData("Unit", "unit")]
    [InlineData("Integration", "integration")]
    [InlineData("E2E", "e2e")]
    [InlineData("EndToEnd", "e2e")]
    [InlineData("endtoend", "e2e")]
    [InlineData("Performance", "performance")]
    public void Map_NormalizesKnownTypes_AndLowercasesOthers(string testType, string expected) =>
        Assert.Equal(expected, TestTypeMapper.Map(testType));
}
