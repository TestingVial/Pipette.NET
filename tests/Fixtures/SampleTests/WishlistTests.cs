using TestingVial.NET;
using Xunit;

namespace SampleTests;

[TestingVial<CartVial>(Description = "Wishlists can be turned into carts")]
public class WishlistTests
{
    [Fact]
    [TestingVial("Wishlist", Description = "Wishlists keep products for later")]
    public void Add_KeepsProduct()
    {
    }
}
