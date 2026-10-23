using System.Collections.Immutable;
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
        #nullable enable
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

        return CreateDriver(parseOptions)
            .RunGenerators(BuildCompilation(source, includeUnionRuntime, parseOptions));
    }

    /// <summary>
    /// Runs the generator and returns the compiler errors in the <em>generated</em> output.
    /// </summary>
    /// <remarks>
    /// <para>The snapshot tests only diff text, so nothing else in the suite proves the generated
    /// code compiles. Whole classes of defect — a missing <c>ref</c> modifier, a generic type
    /// argument a ref struct cannot satisfy, ref-safety escapes — are invisible to a snapshot and
    /// show up only here.</para>
    /// <para>The reference set is scraped from the running process, which targets net10.0 and does
    /// <em>not</em> carry <c>UnionAttribute</c>; pass <paramref name="includeUnionRuntime"/> for any
    /// native-union source, or the errors will be about the missing attribute rather than the code
    /// under test.</para>
    /// </remarks>
    public static ImmutableArray<Diagnostic> GetGeneratedCompilationErrors(
        string source,
        bool includeUnionRuntime = false,
        LanguageVersion languageVersion = LanguageVersion.Preview)
    {
        var parseOptions = new CSharpParseOptions(languageVersion);

        CreateDriver(parseOptions)
            .RunGeneratorsAndUpdateCompilation(
                BuildCompilation(source, includeUnionRuntime, parseOptions),
                out var updated,
                out _);

        return updated.GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToImmutableArray();
    }

    /// <summary>
    /// Asserts that the user source plus everything the generator produced for it compiles clean.
    /// </summary>
    public static void AssertGeneratedCompiles(
        string source,
        bool includeUnionRuntime = false,
        LanguageVersion languageVersion = LanguageVersion.Preview)
    {
        var errors = GetGeneratedCompilationErrors(source, includeUnionRuntime, languageVersion);
        if (errors.Length == 0)
        {
            return;
        }

        throw new InvalidOperationException(
            $"Generated code did not compile ({errors.Length} error(s)):{Environment.NewLine}"
            + string.Join(Environment.NewLine, errors.Select(d => $"  {d.Id}: {d.GetMessage()} @ {d.Location.GetLineSpan()}")));
    }

    /// <summary>
    /// Runs the generator and returns the compiler warnings in the result.
    /// </summary>
    /// <remarks>
    /// For defects that compile but are still wrong, such as a generated partial implementation
    /// whose nullability does not match its declaration (CS8611). The test compilation leaves the
    /// nullable context off, so a source that needs it must open with <c>#nullable enable</c>.
    /// Throws when the generator produced nothing: a source it rejects has no generated code to
    /// warn about, and an empty result would pass for a clean one.
    /// </remarks>
    public static ImmutableArray<Diagnostic> GetGeneratedCompilationWarnings(
        string source,
        bool includeUnionRuntime = false)
    {
        var parseOptions = new CSharpParseOptions(LanguageVersion.Preview);

        var driver = CreateDriver(parseOptions)
            .RunGeneratorsAndUpdateCompilation(
                BuildCompilation(source, includeUnionRuntime, parseOptions),
                out var updated,
                out _);

        var result = driver.GetRunResult();
        if (result.GeneratedTrees.IsEmpty)
        {
            throw new InvalidOperationException(
                "The generator produced no source:" + Environment.NewLine
                + string.Join(Environment.NewLine, result.Diagnostics));
        }

        return updated.GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Warning)
            .ToImmutableArray();
    }

    /// <summary>
    /// Runs the generator, emits the result, and returns the assembly image.
    /// </summary>
    /// <remarks>
    /// For assertions about what reaches metadata, which the compilation itself cannot answer: source
    /// symbols still report a <c>[Conditional]</c> attribute that emit then drops.
    /// </remarks>
    public static byte[] EmitGenerated(string source, params string[] preprocessorSymbols)
    {
        var parseOptions = new CSharpParseOptions(LanguageVersion.Preview, preprocessorSymbols: preprocessorSymbols);

        CreateDriver(parseOptions)
            .RunGeneratorsAndUpdateCompilation(
                BuildCompilation(source, includeUnionRuntime: false, parseOptions),
                out var updated,
                out _);

        using var image = new MemoryStream();
        var result = updated.Emit(image);
        if (!result.Success)
        {
            throw new InvalidOperationException(
                "Emit failed:" + Environment.NewLine
                + string.Join(Environment.NewLine, result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error)));
        }

        return image.ToArray();
    }

    /// <summary>
    /// Runs the generator over <paramref name="source"/> with <paramref name="library"/> — itself
    /// generated and compiled — as a referenced assembly, and returns the run plus the compiler
    /// errors in the result.
    /// </summary>
    /// <remarks>
    /// The library reaches the generator as metadata, the way a union from another project does:
    /// its fields are visible, their <c>FieldOffset</c>s are not.
    /// </remarks>
    public static (GeneratorDriverRunResult Result, ImmutableArray<Diagnostic> Errors) RunWithLibrary(
        string source,
        string library)
    {
        var parseOptions = new CSharpParseOptions(LanguageVersion.Preview);

        var libraryCompilation = BuildCompilation(library, includeUnionRuntime: false, parseOptions, "TestLibrary");
        CreateDriver(parseOptions).RunGeneratorsAndUpdateCompilation(libraryCompilation, out var generatedLibrary, out _);
        using var image = new MemoryStream();
        var emitted = generatedLibrary.Emit(image);
        if (!emitted.Success)
        {
            throw new InvalidOperationException(
                "Library emit failed:" + Environment.NewLine
                + string.Join(Environment.NewLine, emitted.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error)));
        }

        var compilation = BuildCompilation(source, includeUnionRuntime: false, parseOptions)
            .AddReferences(MetadataReference.CreateFromImage(image.ToArray()));
        var driver = CreateDriver(parseOptions).RunGeneratorsAndUpdateCompilation(compilation, out var updated, out _);

        return (
            driver.GetRunResult(),
            updated.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToImmutableArray());
    }

    static CSharpGeneratorDriver CreateDriver(CSharpParseOptions parseOptions) =>
        CSharpGeneratorDriver.Create(
            [new StructUnionGenerator().AsSourceGenerator()],
            parseOptions: parseOptions);

    static CSharpCompilation BuildCompilation(
        string source,
        bool includeUnionRuntime,
        CSharpParseOptions parseOptions,
        string assemblyName = "TestAssembly")
    {
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

        return CSharpCompilation.Create(
            assemblyName: assemblyName,
            syntaxTrees: syntaxTrees,
            references: references,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }
}
