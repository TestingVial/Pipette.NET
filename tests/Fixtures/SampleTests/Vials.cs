using TestingVial.NET;

namespace SampleTests;

internal class CartVial : IVial
{
    public string Name => "Cart";
}

internal class CheckoutVial : IVial
{
    private const string VialName = "Checkout";

    public string Name
    {
        get { return VialName; }
    }
}

internal class SearchVial : IVial
{
    public string Name { get; } = "Search";
}
