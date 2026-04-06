using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using StructUnion.Generator;

namespace StructUnion.GeneratorTests;

public static class GeneratorTestHelper
{
    public static GeneratorDriver CreateDriver(string source)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source);

        var references = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && !string.IsNullOrWhiteSpace(a.Location))
            .Select(a => MetadataReference.CreateFromFile(a.Location))
            .Cast<MetadataReference>()
            .ToList();

        // Add System.Runtime for fundamental types
        var trustedAssemblies = ((string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") ?? "")
            .Split(Path.PathSeparator)
            .Where(p => !string.IsNullOrEmpty(p));

        foreach (var asm in trustedAssemblies)
        {
            if (!references.Any(r => r.Display == asm))
            {
                references.Add(MetadataReference.CreateFromFile(asm));
            }
        }

        var compilation = CSharpCompilation.Create(
            assemblyName: "TestAssembly",
            syntaxTrees: [syntaxTree],
            references: references,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var generator = new StructUnionGenerator();

        return CSharpGeneratorDriver
            .Create(generator)
            .RunGenerators(compilation);
    }

    public static (GeneratorDriver Driver, Compilation Compilation) CreateDriverWithCompilation(string source)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source);

        var references = new List<MetadataReference>();
        var trustedAssemblies = ((string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") ?? "")
            .Split(Path.PathSeparator)
            .Where(p => !string.IsNullOrEmpty(p));

        foreach (var asm in trustedAssemblies)
        {
            references.Add(MetadataReference.CreateFromFile(asm));
        }

        var compilation = CSharpCompilation.Create(
            assemblyName: "TestAssembly",
            syntaxTrees: [syntaxTree],
            references: references,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var generator = new StructUnionGenerator();

        var driver = CSharpGeneratorDriver
            .Create(generator)
            .RunGenerators(compilation);

        return (driver, compilation);
    }
}
