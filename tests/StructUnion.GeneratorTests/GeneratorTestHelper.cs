using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using StructUnion.Generator;

namespace StructUnion.GeneratorTests;

public static class GeneratorTestHelper
{
    /// <summary>
    /// A source-level stand-in for the .NET 11 union runtime types.
    /// </summary>
    /// <remarks>
    /// The compiler recognises a union type purely by attribute name — <c>UnionAttribute</c> is
    /// matched during early attribute decoding, never by identity — so a source declaration is
    /// indistinguishable from the real <c>System.Runtime</c> types as far as the generator is
    /// concerned. That keeps these tests on net10.0 with no preview-package dependency, and
    /// doubles as a regression test for the documented polyfill story.
    /// </remarks>
    public const string UnionRuntimePrelude = """
        namespace System.Runtime.CompilerServices
        {
            [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
            public sealed class UnionAttribute : System.Attribute { }

            public interface IUnion { object? Value { get; } }
        }
        """;

    /// <summary>
    /// Runs the generator over <paramref name="source"/>.
    /// </summary>
    /// <param name="source">The user source to generate from.</param>
    /// <param name="includeUnionRuntime">
    /// Adds <see cref="UnionRuntimePrelude"/> to the compilation, so <c>NativeUnion = true</c>
    /// resolves instead of reporting SU0014.
    /// </param>
    /// <param name="languageVersion">
    /// Defaults to <see cref="LanguageVersion.Preview"/>, matching the repo-wide
    /// <c>&lt;LangVersion&gt;preview&lt;/LangVersion&gt;</c>. Lower it to drive SU0015.
    /// </param>
    public static GeneratorDriver CreateDriver(
        string source,
        bool includeUnionRuntime = false,
        LanguageVersion languageVersion = LanguageVersion.Preview)
    {
        var parseOptions = new CSharpParseOptions(languageVersion);

        var syntaxTrees = new List<SyntaxTree> { CSharpSyntaxTree.ParseText(source, parseOptions) };
        if (includeUnionRuntime)
        {
            syntaxTrees.Add(CSharpSyntaxTree.ParseText(UnionRuntimePrelude, parseOptions));
        }

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
            syntaxTrees: syntaxTrees,
            references: references,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var generator = new StructUnionGenerator();

        return CSharpGeneratorDriver
            .Create([generator.AsSourceGenerator()], parseOptions: parseOptions)
            .RunGenerators(compilation);
    }
}
