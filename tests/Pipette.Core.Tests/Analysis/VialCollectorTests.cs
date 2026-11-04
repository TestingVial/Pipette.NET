using Microsoft.CodeAnalysis;
using Pipette.Core.Model;
using static Pipette.Core.Tests.Analysis.TestCompilation;

namespace Pipette.Core.Tests.Analysis;

public class VialCollectorTests
{
    private const string Usings = "using TestingVial.NET;\n";

    private const string CartVial = "public class CartVial : IVial { public string Name => \"Cart\"; }\n";

    [Fact]
    public async Task BuiltInAttributes_GenericAndStringForms_OnOneMethod()
    {
        var (vials, diagnostics) = await CollectAsync(Usings + CartVial + """
            namespace Shop.Tests
            {
                public class CartTests
                {
                    [UnitTestVial<CartVial>(Description = "Carts can have products added")]
                    [IntegrationTestVial("Product", Description = "Products can be added to a cart")]
                    public void AddToCart_AddsProduct() { }
                }
            }
            """);

        Assert.Empty(diagnostics.Messages);
        Assert.Equal(2, vials.Count);
        Assert.Equal(
            new Vial("Cart", "Carts can have products added", "unit", "csharp",
                new SourceLocation("Shop.Tests.CartTests", "AddToCart_AddsProduct")),
            vials[0]);
        Assert.Equal(
            new Vial("Product", "Products can be added to a cart", "integration", "csharp",
                new SourceLocation("Shop.Tests.CartTests", "AddToCart_AddsProduct")),
            vials[1]);
    }

    [Fact]
    public async Task ClassLevelVial_IsEmittedOnceWithStarMethod()
    {
        var (vials, _) = await CollectAsync(Usings + CartVial + """
            [IntegrationTestVial<CartVial>(Description = "Cart persistence")]
            public class CartTests
            {
                public void A() { }
                public void B() { }
            }
            """);

        var vial = Assert.Single(vials);
        Assert.Equal(SourceLocation.ClassLevel, vial.Location.MethodName);
        Assert.Equal("CartTests", vial.Location.ClassName);
        Assert.Equal("integration", vial.Type);
    }

    [Fact]
    public async Task ClassName_IsFullyQualified_ForNestedAndGenericTypes()
    {
        var (vials, _) = await CollectAsync(Usings + """
            namespace A.B;
            public class Outer
            {
                public class Inner
                {
                    [UnitTestVial("X", Description = "d")] public void M() { }
                }
            }
            public class Generic<T>
            {
                [UnitTestVial("Y", Description = "d")] public void M() { }
            }
            """);

        Assert.Equal(["A.B.Generic<T>", "A.B.Outer.Inner"], vials.Select(v => v.Location.ClassName).Order());
    }

    [Theory]
    [InlineData("")]
    [InlineData("(Description = \"\")")]
    [InlineData("(Description = null)")]
    [InlineData("(Description = \"   \")")]
    public async Task MissingOrEmptyDescription_SkipsEntryWithWarning(string arguments)
    {
        var (vials, diagnostics) = await CollectAsync(Usings + CartVial +
            $"public class T {{ [UnitTestVial<CartVial>{arguments}] public void M() {{ }} }}");

        Assert.Empty(vials);
        var warning = Assert.Single(diagnostics.Warnings());
        Assert.Contains("Description", warning);
        Assert.Contains("Tests/SampleTests.cs(3)", warning);
    }

    [Theory]
    [InlineData("public string Name => \"Cart\";")]
    [InlineData("public string Name { get { return \"Cart\"; } }")]
    [InlineData("public string Name { get => \"Cart\"; }")]
    [InlineData("public string Name { get; } = \"Cart\";")]
    [InlineData("private const string N = \"Cart\"; public string Name => N;")]
    [InlineData("public string Name => Names.Cart;")]
    [InlineData("public string Name => \"Ca\" + \"rt\";")]
    [InlineData("public string Name => $\"{Names.Cart}\";")]
    [InlineData("string IVial.Name => \"Cart\";")]
    public async Task GenericName_IsResolvedFromSourceConstant(string member)
    {
        var (vials, diagnostics) = await CollectAsync(Usings + $$"""
            public static class Names { public const string Cart = "Cart"; }
            public class CartVial : IVial { {{member}} }
            public class T { [UnitTestVial<CartVial>(Description = "d")] public void M() { } }
            """);

        Assert.Empty(diagnostics.Messages);
        Assert.Equal("Cart", Assert.Single(vials).Name);
    }

    [Theory]
    [InlineData("public string Name => GetType().Name;")]
    [InlineData("public string Name => $\"Cart{System.Environment.ProcessorCount}\";")]
    [InlineData("public string Name { get { if (System.Environment.ProcessorCount > 1) return \"A\"; return \"B\"; } }")]
    public async Task GenericName_FallsBackToTypeNameWithWarning(string member)
    {
        var (vials, diagnostics) = await CollectAsync(Usings + $$"""
            public class CartVial : IVial { {{member}} }
            public class T { [UnitTestVial<CartVial>(Description = "d")] public void M() { } }
            """);

        Assert.Equal("CartVial", Assert.Single(vials).Name);
        Assert.Contains("CartVial", Assert.Single(diagnostics.Warnings()));
    }

    private const string LibraryVial = "namespace Lib; public class LibVial : IVial { public string Name => \"Lib\"; }";

    private const string LibraryAttribute = """
        namespace Lib;
        public class PerfVial : VialAttribute { public PerfVial(string name) : base(name, "Perf") { } }
        """;
    [Fact]
    public async Task GenericName_FromMetadataVialType_FallsBackToTypeName()
    {
        var library = Create([("Lib/Vials.cs", Usings + LibraryVial)]).EmitToImageReference();
        var compilation = Create(
            [("Tests/T.cs", Usings + "public class T { [UnitTestVial<Lib.LibVial>(Description = \"d\")] public void M() { } }")],
            [library]);

        var (vials, diagnostics) = await CollectAsync(compilation);

        Assert.Equal("LibVial", Assert.Single(vials).Name);
        Assert.Single(diagnostics.Warnings());
    }

    [Fact]
    public async Task ProjectReference_VialTypeAndCustomAttribute_AreReadFromReferencedSource()
    {
        var vialLibrary = Create([("Lib/Vials.cs", Usings + LibraryVial)]).ToMetadataReference();
        var attributeLibrary = Create([("Lib/Attr.cs", Usings + LibraryAttribute)]).ToMetadataReference();
        var compilation = Create(
            [("Tests/T.cs", Usings + """
                public class T
                {
                    [UnitTestVial<Lib.LibVial>(Description = "d")] public void A() { }
                    [Lib.PerfVial("X", Description = "d")] public void B() { }
                }
                """)],
            [vialLibrary, attributeLibrary]);

        var (vials, diagnostics) = await CollectAsync(compilation);

        Assert.Empty(diagnostics.Messages);
        Assert.Equal(["A|Lib|unit", "B|X|perf"], vials.Select(v => $"{v.Location.MethodName}|{v.Name}|{v.Type}"));
    }

    [Fact]
    public async Task CustomAttributes_ReadTestTypeFromBaseInitializer()
    {
        var (vials, diagnostics) = await CollectAsync(Usings + CartVial + """
            public static class Types { public const string EndToEnd = "EndToEnd"; }

            public class PerformanceTestVial<T> : TestingVialAttribute<T> where T : IVial, new()
            { public PerformanceTestVial() : base("Performance") { } }

            public class PerformanceTestVial : VialAttribute
            { public PerformanceTestVial(string name) : base(name, "Performance") { } }

            public class E2EVial : VialAttribute
            { public E2EVial(string name) : base(testType: Types.EndToEnd, name: name) { } }

            public class SmokeVial : PerformanceTestVial
            { public SmokeVial(string name) : base(name) { } }

            public class ChainedVial : VialAttribute
            {
                public ChainedVial(string name) : this(name, "E2E") { }
                private ChainedVial(string name, string type) : base(name, type) { }
            }

            public class FixedNameVial : VialAttribute
            { public FixedNameVial(string kind = "Contract") : base("Fixed", kind) { } }

            public class LoadVial(string name) : VialAttribute(name, "Load");

            public class MyUnitVial : UnitTestVial
            { public MyUnitVial() : base("Mine") { } }

            public class FixedCartVial : TestingVialAttribute<CartVial>
            { public FixedCartVial() : base("Smoke") { } }

            public class T
            {
                [PerformanceTestVial<CartVial>(Description = "d")] public void GenericCustom() { }
                [PerformanceTestVial("Checkout", Description = "d")] public void StringCustom() { }
                [E2EVial("Journey", Description = "d")] public void NamedArgs() { }
                [SmokeVial("Smoke", Description = "d")] public void DerivedCustom() { }
                [ChainedVial("Chain", Description = "d")] public void ThisInitializer() { }
                [FixedNameVial(Description = "d")] public void DefaultParameter() { }
                [LoadVial("Load", Description = "d")] public void PrimaryConstructor() { }
                [MyUnitVial(Description = "d")] public void DerivedFromBuiltIn() { }
                [FixedCartVial(Description = "d")] public void ClosedGenericBase() { }
            }
            """);

        Assert.Empty(diagnostics.Messages);
        Assert.Equal(
            [
                "GenericCustom|Cart|performance",
                "StringCustom|Checkout|performance",
                "NamedArgs|Journey|e2e",
                "DerivedCustom|Smoke|performance",
                "ThisInitializer|Chain|e2e",
                "DefaultParameter|Fixed|contract",
                "PrimaryConstructor|Load|load",
                "DerivedFromBuiltIn|Mine|unit",
                "ClosedGenericBase|Cart|smoke",
            ],
            vials.Select(v => $"{v.Location.MethodName}|{v.Name}|{v.Type}"));
    }

    [Fact]
    public async Task CustomAttribute_WithNonConstantTestType_IsSkippedWithWarning()
    {
        var (vials, diagnostics) = await CollectAsync(Usings + """
            public static class Helper { public static string Kind() => "Perf"; }
            public class DynamicVial : VialAttribute
            { public DynamicVial(string name) : base(name, Helper.Kind()) { } }
            public class T { [DynamicVial("X", Description = "d")] public void M() { } }
            """);

        Assert.Empty(vials);
        Assert.Contains("test type", Assert.Single(diagnostics.Warnings()));
    }

    [Fact]
    public async Task CustomAttribute_FromMetadata_IsSkippedWithWarning()
    {
        var library = Create([("Lib/Attr.cs", Usings + LibraryAttribute)]).EmitToImageReference();
        var compilation = Create(
            [("Tests/T.cs", Usings + "public class T { [Lib.PerfVial(\"X\", Description = \"d\")] public void M() { } }")],
            [library]);

        var (vials, diagnostics) = await CollectAsync(compilation);

        Assert.Empty(vials);
        Assert.Contains("not available in source", Assert.Single(diagnostics.Warnings()));
    }

    [Fact]
    public async Task StringForm_WithNonConstantName_IsSkippedWithWarning()
    {
        var compilation = Create(
            [("Tests/T.cs", Usings + "public class T { [UnitTestVial(Description = \"d\")] public void M() { } }")],
            allowErrors: true);

        var (vials, diagnostics) = await CollectAsync(compilation);

        Assert.Empty(vials);
        Assert.Single(diagnostics.Warnings());
    }

    [Fact]
    public async Task NonVialAttributesAndPlainCode_ProduceNothing()
    {
        var (vials, diagnostics) = await CollectAsync(Usings + """
            [System.Obsolete] public class T { [System.Obsolete] public void M() { } }
            """);

        Assert.Empty(vials);
        Assert.Empty(diagnostics.Messages);
    }

    [Fact]
    public async Task SourceDefinedPackage_IsRecognisedByMetadataName()
    {
        var source = """
            namespace TestingVial.NET
            {
                public interface IVial { string Name { get; } }
                [System.AttributeUsage(System.AttributeTargets.Class | System.AttributeTargets.Method, AllowMultiple = true)]
                public abstract class VialAttribute : System.Attribute
                {
                    protected VialAttribute(string name, string testType) { }
                    public string? Description { get; init; }
                }
                public class UnitTestVial : VialAttribute { public UnitTestVial(string name) : base(name, "Unit") { } }
            }
            public class T { [TestingVial.NET.UnitTestVial("Src", Description = "d")] public void M() { } }
            """;
        var tree = Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree.ParseText(source, path: Path.Combine(BaseDirectory, "T.cs"), cancellationToken: TestContext.Current.CancellationToken);
        var compilation = Microsoft.CodeAnalysis.CSharp.CSharpCompilation.Create(
            "NoPackage",
            [tree],
            FrameworkReferences,
            new(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));

        var (vials, diagnostics) = await CollectAsync(compilation);

        Assert.Empty(diagnostics.Messages);
        var vial = Assert.Single(vials);
        Assert.Equal(("Src", "unit"), (vial.Name, vial.Type));
    }
}
