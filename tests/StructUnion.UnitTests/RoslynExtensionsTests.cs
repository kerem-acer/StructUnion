using Imposter.Abstractions;
using Microsoft.CodeAnalysis;
using StructUnion.Generator.Infrastructure;

namespace StructUnion.UnitTests;

public class RoslynExtensionsTests
{
    // ── ToAccessibilityString ──

    [Test]
    [Arguments(Accessibility.Public, "public")]
    [Arguments(Accessibility.Internal, "internal")]
    [Arguments(Accessibility.Private, "private")]
    [Arguments(Accessibility.Protected, "protected")]
    [Arguments(Accessibility.ProtectedOrInternal, "protected internal")]
    [Arguments(Accessibility.ProtectedAndInternal, "private protected")]
    [Arguments(Accessibility.NotApplicable, "internal")]
    public async Task ToAccessibilityString_AllValues(Accessibility access, string expected)
    {
        await Assert.That(access.ToAccessibilityString()).IsEqualTo(expected);
    }

    // ── GetAccessibilityString ──

    [Test]
    [Arguments(Accessibility.Public, "public")]
    [Arguments(Accessibility.Private, "private")]
    public async Task GetAccessibilityString_FromMockedSymbol(Accessibility access, string expected)
    {
        var mock = ISymbol.Imposter();
        mock.DeclaredAccessibility.Getter().Returns(access);

        await Assert.That(mock.Instance().GetAccessibilityString()).IsEqualTo(expected);
    }

    // ── GetNamespaceString ──

    [Test]
    public async Task GetNamespaceString_GlobalNamespace_ReturnsEmpty()
    {
        var nsMock = INamespaceSymbol.Imposter();
        nsMock.IsGlobalNamespace.Getter().Returns(true);

        var typeMock = INamedTypeSymbol.Imposter();
        typeMock.ContainingNamespace.Getter().Returns(nsMock.Instance());

        await Assert.That(typeMock.Instance().GetNamespaceString()).IsEqualTo("");
    }

    [Test]
    public async Task GetNamespaceString_NestedNamespace_ReturnsDisplayString()
    {
        var nsMock = INamespaceSymbol.Imposter();
        nsMock.IsGlobalNamespace.Getter().Returns(false);
        nsMock.ToDisplayString(Arg<SymbolDisplayFormat?>.Any()).Returns("MyApp.Models");

        var typeMock = INamedTypeSymbol.Imposter();
        typeMock.ContainingNamespace.Getter().Returns(nsMock.Instance());

        await Assert.That(typeMock.Instance().GetNamespaceString()).IsEqualTo("MyApp.Models");
    }

    // ── GetContainingTypeChain ──

    [Test]
    public async Task GetContainingTypeChain_NoParent_ReturnsEmpty()
    {
        var mock = INamedTypeSymbol.Imposter();
        mock.ContainingType.Getter().Returns((INamedTypeSymbol)null!);

        var result = mock.Instance().GetContainingTypeChain();
        await Assert.That(result.Count).IsEqualTo(0);
    }

    [Test]
    public async Task GetContainingTypeChain_SingleClass_ReturnsOne()
    {
        var parent = INamedTypeSymbol.Imposter();
        parent.IsValueType.Getter().Returns(false);
        parent.IsRecord.Getter().Returns(false);
        parent.Name.Getter().Returns("Outer");
        parent.TypeParameters.Getter().Returns([]);
        parent.ContainingType.Getter().Returns((INamedTypeSymbol)null!);

        var child = INamedTypeSymbol.Imposter();
        child.ContainingType.Getter().Returns(parent.Instance());

        var result = child.Instance().GetContainingTypeChain();
        await Assert.That(result.Count).IsEqualTo(1);
        await Assert.That(result[0]).IsEqualTo("partial class Outer");
    }

    [Test]
    public async Task GetContainingTypeChain_Struct_UsesStructKeyword()
    {
        var parent = INamedTypeSymbol.Imposter();
        parent.IsValueType.Getter().Returns(true);
        parent.Name.Getter().Returns("Container");
        parent.TypeParameters.Getter().Returns([]);
        parent.ContainingType.Getter().Returns((INamedTypeSymbol)null!);

        var child = INamedTypeSymbol.Imposter();
        child.ContainingType.Getter().Returns(parent.Instance());

        var result = child.Instance().GetContainingTypeChain();
        await Assert.That(result[0]).IsEqualTo("partial struct Container");
    }

    [Test]
    public async Task GetContainingTypeChain_Record_UsesRecordClassKeyword()
    {
        var parent = INamedTypeSymbol.Imposter();
        parent.IsValueType.Getter().Returns(false);
        parent.IsRecord.Getter().Returns(true);
        parent.Name.Getter().Returns("Base");
        parent.TypeParameters.Getter().Returns([]);
        parent.ContainingType.Getter().Returns((INamedTypeSymbol)null!);

        var child = INamedTypeSymbol.Imposter();
        child.ContainingType.Getter().Returns(parent.Instance());

        var result = child.Instance().GetContainingTypeChain();
        await Assert.That(result[0]).IsEqualTo("partial record class Base");
    }

    [Test]
    public async Task GetContainingTypeChain_GenericClass_IncludesTypeParameters()
    {
        var tp = ITypeParameterSymbol.Imposter();
        tp.Name.Getter().Returns("T");

        var parent = INamedTypeSymbol.Imposter();
        parent.IsValueType.Getter().Returns(false);
        parent.IsRecord.Getter().Returns(false);
        parent.Name.Getter().Returns("Outer");
        parent.TypeParameters.Getter().Returns([tp.Instance()]);
        parent.ContainingType.Getter().Returns((INamedTypeSymbol)null!);

        var child = INamedTypeSymbol.Imposter();
        child.ContainingType.Getter().Returns(parent.Instance());

        var result = child.Instance().GetContainingTypeChain();
        await Assert.That(result[0]).IsEqualTo("partial class Outer<T>");
    }

    // ── GetTypeParameterModels ──

    [Test]
    public async Task GetTypeParameterModels_NoParams_ReturnsEmpty()
    {
        var mock = INamedTypeSymbol.Imposter();
        mock.TypeParameters.Getter().Returns([]);

        var result = mock.Instance().GetTypeParameterModels();
        await Assert.That(result.Count).IsEqualTo(0);
    }

    [Test]
    public async Task GetTypeParameterModels_SingleUnconstrained()
    {
        var tp = ITypeParameterSymbol.Imposter();
        tp.Name.Getter().Returns("T");
        tp.HasReferenceTypeConstraint.Getter().Returns(false);
        tp.HasValueTypeConstraint.Getter().Returns(false);
        tp.HasUnmanagedTypeConstraint.Getter().Returns(false);
        tp.HasNotNullConstraint.Getter().Returns(false);
        tp.HasConstructorConstraint.Getter().Returns(false);
        tp.ConstraintTypes.Getter().Returns([]);

        var mock = INamedTypeSymbol.Imposter();
        mock.TypeParameters.Getter().Returns([tp.Instance()]);

        var result = mock.Instance().GetTypeParameterModels();
        await Assert.That(result.Count).IsEqualTo(1);
        await Assert.That(result[0].Name).IsEqualTo("T");
        await Assert.That(result[0].Constraints.Count).IsEqualTo(0);
    }

    [Test]
    public async Task GetTypeParameterModels_WithStructConstraint()
    {
        var tp = ITypeParameterSymbol.Imposter();
        tp.Name.Getter().Returns("T");
        tp.HasReferenceTypeConstraint.Getter().Returns(false);
        tp.HasValueTypeConstraint.Getter().Returns(true);
        tp.HasUnmanagedTypeConstraint.Getter().Returns(false);
        tp.HasNotNullConstraint.Getter().Returns(false);
        tp.HasConstructorConstraint.Getter().Returns(false);
        tp.ConstraintTypes.Getter().Returns([]);

        var mock = INamedTypeSymbol.Imposter();
        mock.TypeParameters.Getter().Returns([tp.Instance()]);

        var result = mock.Instance().GetTypeParameterModels();
        await Assert.That(result[0].Constraints.Count).IsEqualTo(1);
        await Assert.That(result[0].Constraints[0]).IsEqualTo("struct");
    }

    [Test]
    public async Task GetTypeParameterModels_MultipleConstraints()
    {
        var tp = ITypeParameterSymbol.Imposter();
        tp.Name.Getter().Returns("T");
        tp.HasReferenceTypeConstraint.Getter().Returns(false);
        tp.HasValueTypeConstraint.Getter().Returns(false);
        tp.HasUnmanagedTypeConstraint.Getter().Returns(false);
        tp.HasNotNullConstraint.Getter().Returns(true);
        tp.HasConstructorConstraint.Getter().Returns(true);
        tp.ConstraintTypes.Getter().Returns([]);

        var mock = INamedTypeSymbol.Imposter();
        mock.TypeParameters.Getter().Returns([tp.Instance()]);

        var result = mock.Instance().GetTypeParameterModels();
        await Assert.That(result[0].Constraints.Count).IsEqualTo(2);
        await Assert.That(result[0].Constraints[0]).IsEqualTo("notnull");
        await Assert.That(result[0].Constraints[1]).IsEqualTo("new()");
    }

    [Test]
    public async Task GetTypeParameterModels_WithTypeConstraint()
    {
        var constraintType = ITypeSymbol.Imposter();
        constraintType.ToDisplayString(Arg<SymbolDisplayFormat?>.Any()).Returns("global::System.IDisposable");

        var tp = ITypeParameterSymbol.Imposter();
        tp.Name.Getter().Returns("T");
        tp.HasReferenceTypeConstraint.Getter().Returns(false);
        tp.HasValueTypeConstraint.Getter().Returns(false);
        tp.HasUnmanagedTypeConstraint.Getter().Returns(false);
        tp.HasNotNullConstraint.Getter().Returns(false);
        tp.HasConstructorConstraint.Getter().Returns(false);
        tp.ConstraintTypes.Getter().Returns([constraintType.Instance()]);

        var mock = INamedTypeSymbol.Imposter();
        mock.TypeParameters.Getter().Returns([tp.Instance()]);

        var result = mock.Instance().GetTypeParameterModels();
        await Assert.That(result[0].Constraints.Count).IsEqualTo(1);
        await Assert.That(result[0].Constraints[0]).IsEqualTo("global::System.IDisposable");
    }
}
