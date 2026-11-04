using TestingVial.NET;
using Xunit;

namespace SampleTests;

[IntegrationTestVial("Catalog")]
public class SearchTests
{
    [Fact]
    [UnitTestVial<SearchVial>(Description = "Search matches products by name")]
    public void Search_MatchesByName()
    {
    }

    [Fact]
    [UnitTestVial<SearchVial>]
    public void Search_WithoutDescription()
    {
    }
}
