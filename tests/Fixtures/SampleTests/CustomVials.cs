using TestingVial.NET;

namespace SampleTests;

public class PerformanceTestVial<T> : TestingVialAttribute<T> where T : IVial, new()
{
    public PerformanceTestVial() : base("Performance") { }
}

public class PerformanceTestVial : VialAttribute
{
    public PerformanceTestVial(string name) : base(name, "Performance") { }
}
