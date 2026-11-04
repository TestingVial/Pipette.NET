using System.Text.Json;
using Pipette.Core.Model;
using Pipette.Core.Output;

namespace Pipette.Core.Tests;

public class VialWriterTests
{
    private static Vial MakeVial(string cls, string method, string name, string type = "unit") =>
        new(name, $"{name} description", type, Vial.CSharpTechnology, new SourceLocation(cls, method));

    [Fact]
    public void Serialize_UsesCamelCaseAndSchemaVersion()
    {
        var json = VialWriter.Serialize(new VialDocument([MakeVial("Ns.A", "M", "Cart")]));

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        Assert.Equal("0.0.1", root.GetProperty("schemaVersion").GetString());

        var vial = root.GetProperty("vials")[0];
        Assert.Equal("Cart", vial.GetProperty("name").GetString());
        Assert.Equal("csharp", vial.GetProperty("technology").GetString());
        var location = vial.GetProperty("location");
        Assert.False(location.TryGetProperty("filePath", out _));
        Assert.Equal("Ns.A", location.GetProperty("className").GetString());
        Assert.Equal("M", location.GetProperty("methodName").GetString());
    }

    [Fact]
    public void Serialize_SortsByClassMethodName_AndLowercasesType()
    {
        var document = new VialDocument(
        [
            MakeVial("Ns.B", "M", "Z"),
            MakeVial("Ns.A", "Test2", "Cart"),
            MakeVial("Ns.A", "Test1", "Product", "Performance"),
            MakeVial("Ns.A", "Test1", "Cart"),
            MakeVial("Ns.A", SourceLocation.ClassLevel, "Cart"),
        ]);

        var normalized = VialWriter.Normalize(document).Vials;

        Assert.Equal(
            ["Ns.A|*|Cart", "Ns.A|Test1|Cart", "Ns.A|Test1|Product", "Ns.A|Test2|Cart", "Ns.B|M|Z"],
            normalized.Select(v => $"{v.Location.ClassName}|{v.Location.MethodName}|{v.Name}"));
        Assert.Equal("performance", normalized[2].Type);
    }

    [Fact]
    public void Serialize_IsDeterministic()
    {
        var vials = new[] { MakeVial("B", "M", "X"), MakeVial("A", "M", "Y") };

        Assert.Equal(
            VialWriter.Serialize(new VialDocument(vials)),
            VialWriter.Serialize(new VialDocument(vials.Reverse().ToArray())));
    }
}
