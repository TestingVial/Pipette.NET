using Microsoft.CodeAnalysis;

namespace Pipette.Core.Analysis;

/// <summary>
/// Returns semantic models for syntax trees of the analyzed compilation and of the compilations it references
/// (MSBuildWorkspace exposes project-to-project references as <see cref="CompilationReference"/>), so vial types
/// and custom attributes declared in another project of the solution can still be read from source.
/// </summary>
internal sealed class SemanticModelProvider(Compilation compilation)
{
    private readonly Dictionary<SyntaxTree, SemanticModel?> _models = [];
    private List<Compilation>? _compilations;

    public SemanticModel? Get(SyntaxTree tree)
    {
        if (!_models.TryGetValue(tree, out var model))
        {
            var owner = Compilations().FirstOrDefault(c => c.ContainsSyntaxTree(tree));
            model = owner?.GetSemanticModel(tree);
            _models[tree] = model;
        }

        return model;
    }

    private List<Compilation> Compilations()
    {
        if (_compilations is null)
        {
            _compilations = [];
            var pending = new Queue<Compilation>([compilation]);
            var seen = new HashSet<Compilation>();
            while (pending.TryDequeue(out var current))
            {
                if (!seen.Add(current))
                {
                    continue;
                }

                _compilations.Add(current);
                foreach (var reference in current.References.OfType<CompilationReference>())
                {
                    pending.Enqueue(reference.Compilation);
                }
            }
        }

        return _compilations;
    }
}
