using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using StructUnion.Generator.Emitting;
using StructUnion.Generator.Parsing;

namespace StructUnion.Generator;

[Generator]
public sealed class StructUnionGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var results = context.SyntaxProvider.ForAttributeWithMetadataName(
            fullyQualifiedMetadataName: "StructUnion.StructUnionAttribute",
            predicate: static (node, _) => node is StructDeclarationSyntax or RecordDeclarationSyntax or ClassDeclarationSyntax,
            transform: static (ctx, ct) => UnionParser.Parse(ctx, ct)
        );

        context.RegisterSourceOutput(results, static (spc, result) =>
        {
            foreach (var diag in result.Diagnostics)
            {
                spc.ReportDiagnostic(diag.ToDiagnostic());
            }

            if (result.Model is { } model)
            {
                var source = UnionEmitter.Emit(model);
                spc.AddSource($"{model.FullHintName}.g.cs", source);

                if (model.Mode == Models.GenerationMode.RecordTemplate
                    && model.TemplateTypeName.Length > 0)
                {
                    var templateDoc = UnionEmitter.EmitTemplateDoc(model);
                    spc.AddSource($"{model.FullHintName}.Template.g.cs", templateDoc);
                }
            }
        });
    }
}
