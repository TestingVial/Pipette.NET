using TestingVial.NET;
using Xunit;

namespace SampleTests;

public class CartTests
{
    [Fact]
    [UnitTestVial<CartVial>(Description = "Carts can have products added")]
    [UnitTestVial("Product", Description = "Products can be added to a cart")]
    public void AddToCart_AddsProduct()
    {
    }

    [Fact]
    [EndToEndTestVial<CartVial>(Description = "A customer can fill a cart and see its total")]
    public void FillCart_ShowsTotal()
    {
    }
}
