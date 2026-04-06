using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using StructUnion.Generator.Emitting;
using StructUnion.Generator.Infrastructure;
using StructUnion.Generator.Models;
using StructUnion.Generator.Parsing;

namespace StructUnion.Generator;

[Generator]
public sealed class StructUnionGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Assembly options: computed once, correctly invalidates all types when changed.
        var assemblyOptions = context.CompilationProvider.Select(
            static (compilation, _) => compilation.GetAssemblyOptions());

        // Per-type data: cached per attribute-bearing type. Does NOT read Compilation.
        var perTypeData = context.SyntaxProvider.ForAttributeWithMetadataName(
            fullyQualifiedMetadataName: typeof(StructUnionAttribute).FullName,
            predicate: static (node, _) => node is StructDeclarationSyntax or RecordDeclarationSyntax or ClassDeclarationSyntax,
            transform: static (ctx, ct) => UnionParser.ExtractTypeData(ctx, ct)
        );

        // Combine per-type data with assembly options.
        // When assembly options change, all types re-evaluate (correct).
        // When a single type changes, only that type re-evaluates (correct).
        var combined = perTypeData.Combine(assemblyOptions);

        context.RegisterSourceOutput(combined, static (spc, pair) =>
        {
            var (transform, asmOptions) = pair;

            // Report diagnostics from the transform phase (structural validation)
            foreach (var diag in transform.Diagnostics)
            {
                spc.ReportDiagnostic(diag.ToDiagnostic());
            }

            if (transform.Data is not { } data)
            {
                return;
            }

            // Resolve options cascade and build the final model
            var result = UnionParser.ResolveAndBuild(data, asmOptions);

            foreach (var diag in result.Diagnostics)
            {
                spc.ReportDiagnostic(diag.ToDiagnostic());
            }

            if (result.Model is not { } model)
            {
                return;
            }

            var source = UnionEmitter.Emit(model);
            spc.AddSource($"{model.FullHintName}.g.cs", source);

            if (model.Mode == GenerationMode.RecordTemplate
                && model.TemplateTypeName.Length > 0)
            {
                var templateDoc = UnionEmitter.EmitTemplateDoc(model);
                spc.AddSource($"{model.FullHintName}.Template.g.cs", templateDoc);
            }
        });
    }
}
