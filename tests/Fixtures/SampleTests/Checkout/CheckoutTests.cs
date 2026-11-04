using TestingVial.NET;
using Xunit;

namespace SampleTests.Checkout;

[IntegrationTestVial<CheckoutVial>(Description = "Checkout talks to the payment gateway")]
public class CheckoutTests
{
    [Fact]
    [EndToEndTestVial("Checkout", Description = "A customer can pay for a cart")]
    public void Pay_CompletesOrder()
    {
    }

    [Fact]
    [PerformanceTestVial<CheckoutVial>(Description = "Checkout completes in under a second")]
    [PerformanceTestVial("Search", Description = "Search answers within 100 ms")]
    public void Checkout_IsFast()
    {
    }
}
