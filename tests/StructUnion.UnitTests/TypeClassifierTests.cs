using Imposter.Abstractions;
using Microsoft.CodeAnalysis;
using StructUnion.Generator.Parsing;

namespace StructUnion.UnitTests;

public class TypeClassifierTests
{
    // ── Align ──

    [Test]
    [Arguments(0, 1, 0)]
    [Arguments(0, 8, 0)]
    [Arguments(1, 1, 1)]
    [Arguments(1, 4, 4)]
    [Arguments(1, 8, 8)]
    [Arguments(4, 4, 4)]
    [Arguments(5, 4, 8)]
    [Arguments(7, 8, 8)]
    [Arguments(8, 8, 8)]
    [Arguments(9, 8, 16)]
    [Arguments(16, 8, 16)]
    [Arguments(17, 8, 24)]
    [Arguments(100, 16, 112)]
    [Arguments(1000001, 8, 1000008)]
    public async Task Align_RoundsUpToNextMultiple(int offset, int alignment, int expected)
    {
        var result = TypeClassifier.Align(offset, alignment);
        await Assert.That(result).IsEqualTo(expected);
    }

    [Test]
    [Arguments(0, 1)]
    [Arguments(1, 1)]
    [Arguments(2, 2)]
    [Arguments(4, 4)]
    [Arguments(8, 8)]
    [Arguments(16, 16)]
    [Arguments(0, 8)]
    [Arguments(0, 16)]
    public async Task Align_AlreadyAligned_Unchanged(int offset, int alignment)
    {
        var result = TypeClassifier.Align(offset, alignment);
        await Assert.That(result).IsEqualTo(offset);
    }

    // ── GetSize with Imposter-mocked ITypeSymbol ──

    [Test]
    [Arguments(SpecialType.System_Boolean, 1)]
    [Arguments(SpecialType.System_Byte, 1)]
    [Arguments(SpecialType.System_SByte, 1)]
    [Arguments(SpecialType.System_Int16, 2)]
    [Arguments(SpecialType.System_UInt16, 2)]
    [Arguments(SpecialType.System_Char, 2)]
    [Arguments(SpecialType.System_Int32, 4)]
    [Arguments(SpecialType.System_UInt32, 4)]
    [Arguments(SpecialType.System_Single, 4)]
    [Arguments(SpecialType.System_Int64, 8)]
    [Arguments(SpecialType.System_UInt64, 8)]
    [Arguments(SpecialType.System_Double, 8)]
    [Arguments(SpecialType.System_IntPtr, 8)]
    [Arguments(SpecialType.System_UIntPtr, 8)]
    [Arguments(SpecialType.System_Decimal, 16)]
    public async Task GetSize_SpecialTypes(SpecialType specialType, int expectedSize)
    {
        var mock = ITypeSymbol.Imposter();
        mock.SpecialType.Getter().Returns(specialType);
        mock.IsValueType.Getter().Returns(true);
        mock.TypeKind.Getter().Returns(TypeKind.Struct);

        var result = TypeClassifier.GetSize(mock.Instance());
        await Assert.That(result).IsEqualTo(expectedSize);
    }

    [Test]
    public async Task GetSize_TypeParameter_ReturnsNegativeOne()
    {
        var mock = ITypeSymbol.Imposter();
        mock.TypeKind.Getter().Returns(TypeKind.TypeParameter);

        var result = TypeClassifier.GetSize(mock.Instance());
        await Assert.That(result).IsEqualTo(-1);
    }

    [Test]
    public async Task GetSize_ReferenceType_ReturnsEight()
    {
        var mock = ITypeSymbol.Imposter();
        mock.TypeKind.Getter().Returns(TypeKind.Class);
        mock.IsValueType.Getter().Returns(false);
        mock.SpecialType.Getter().Returns(SpecialType.None);

        var result = TypeClassifier.GetSize(mock.Instance());
        await Assert.That(result).IsEqualTo(8);
    }

    // ── GetAlignment ──

    [Test]
    [Arguments(SpecialType.System_Boolean, 1)]
    [Arguments(SpecialType.System_Byte, 1)]
    [Arguments(SpecialType.System_SByte, 1)]
    [Arguments(SpecialType.System_Int16, 2)]
    [Arguments(SpecialType.System_UInt16, 2)]
    [Arguments(SpecialType.System_Char, 2)]
    [Arguments(SpecialType.System_Int32, 4)]
    [Arguments(SpecialType.System_UInt32, 4)]
    [Arguments(SpecialType.System_Single, 4)]
    [Arguments(SpecialType.System_Int64, 8)]
    [Arguments(SpecialType.System_UInt64, 8)]
    [Arguments(SpecialType.System_Double, 8)]
    [Arguments(SpecialType.System_IntPtr, 8)]
    [Arguments(SpecialType.System_UIntPtr, 8)]
    [Arguments(SpecialType.System_Decimal, 8)]
    public async Task GetAlignment_SpecialTypes(SpecialType specialType, int expectedAlignment)
    {
        var mock = ITypeSymbol.Imposter();
        mock.SpecialType.Getter().Returns(specialType);
        mock.IsValueType.Getter().Returns(true);
        mock.TypeKind.Getter().Returns(TypeKind.Struct);

        var result = TypeClassifier.GetAlignment(mock.Instance());
        await Assert.That(result).IsEqualTo(expectedAlignment);
    }

    [Test]
    public async Task GetAlignment_ReferenceType_ReturnsEight()
    {
        var mock = ITypeSymbol.Imposter();
        mock.TypeKind.Getter().Returns(TypeKind.Class);
        mock.IsValueType.Getter().Returns(false);
        mock.SpecialType.Getter().Returns(SpecialType.None);

        var result = TypeClassifier.GetAlignment(mock.Instance());
        await Assert.That(result).IsEqualTo(8);
    }

    [Test]
    public async Task GetAlignment_TypeParameter_ReturnsNegativeOne()
    {
        var mock = ITypeSymbol.Imposter();
        mock.TypeKind.Getter().Returns(TypeKind.TypeParameter);

        var result = TypeClassifier.GetAlignment(mock.Instance());
        await Assert.That(result).IsEqualTo(-1);
    }

    // ── WellKnownTypes via INamedTypeSymbol ──

    [Test]
    [Arguments("global::System.Guid", 16, 4)]
    [Arguments("global::System.DateTime", 8, 8)]
    [Arguments("global::System.DateTimeOffset", 16, 8)]
    [Arguments("global::System.TimeSpan", 8, 8)]
    [Arguments("global::System.DateOnly", 4, 4)]
    [Arguments("global::System.TimeOnly", 8, 8)]
    [Arguments("global::System.Half", 2, 2)]
    [Arguments("global::System.Int128", 16, 8)]
    [Arguments("global::System.UInt128", 16, 8)]
    public async Task GetSizeAndAlignment_WellKnownTypes(string fqn, int expectedSize, int expectedAlignment)
    {
        var mock = INamedTypeSymbol.Imposter();
        mock.TypeKind.Getter().Returns(TypeKind.Struct);
        mock.IsValueType.Getter().Returns(true);
        mock.SpecialType.Getter().Returns(SpecialType.None);
        mock.ToDisplayString(Arg<SymbolDisplayFormat?>.Any()).Returns(fqn);
        mock.GetMembers().Returns([]);

        var instance = mock.Instance();
        await Assert.That(TypeClassifier.GetSize(instance)).IsEqualTo(expectedSize);
        await Assert.That(TypeClassifier.GetAlignment(instance)).IsEqualTo(expectedAlignment);
    }

    // ── TryComputeStructSize / TryComputeStructAlignment ──

    [Test]
    public async Task GetSize_EnumWithIntUnderlying_ReturnsFour()
    {
        var underlyingType = INamedTypeSymbol.Imposter();
        underlyingType.TypeKind.Getter().Returns(TypeKind.Struct);
        underlyingType.IsValueType.Getter().Returns(true);
        underlyingType.SpecialType.Getter().Returns(SpecialType.System_Int32);
        underlyingType.ToDisplayString(Arg<SymbolDisplayFormat?>.Any()).Returns("int");

        var enumType = INamedTypeSymbol.Imposter();
        enumType.TypeKind.Getter().Returns(TypeKind.Enum);
        enumType.IsValueType.Getter().Returns(true);
        enumType.SpecialType.Getter().Returns(SpecialType.None);
        enumType.ToDisplayString(Arg<SymbolDisplayFormat?>.Any()).Returns("global::MyEnum");
        enumType.EnumUnderlyingType.Getter().Returns(underlyingType.Instance());

        await Assert.That(TypeClassifier.GetSize(enumType.Instance())).IsEqualTo(4);
    }

    [Test]
    public async Task GetAlignment_EnumWithByteUnderlying_ReturnsOne()
    {
        var underlyingType = INamedTypeSymbol.Imposter();
        underlyingType.TypeKind.Getter().Returns(TypeKind.Struct);
        underlyingType.IsValueType.Getter().Returns(true);
        underlyingType.SpecialType.Getter().Returns(SpecialType.System_Byte);
        underlyingType.ToDisplayString(Arg<SymbolDisplayFormat?>.Any()).Returns("byte");

        var enumType = INamedTypeSymbol.Imposter();
        enumType.TypeKind.Getter().Returns(TypeKind.Enum);
        enumType.IsValueType.Getter().Returns(true);
        enumType.SpecialType.Getter().Returns(SpecialType.None);
        enumType.ToDisplayString(Arg<SymbolDisplayFormat?>.Any()).Returns("global::MyEnum");
        enumType.EnumUnderlyingType.Getter().Returns(underlyingType.Instance());

        await Assert.That(TypeClassifier.GetAlignment(enumType.Instance())).IsEqualTo(1);
    }

    [Test]
    public async Task GetSize_StructWithOneIntField_ReturnsFour()
    {
        var intType = ITypeSymbol.Imposter();
        intType.TypeKind.Getter().Returns(TypeKind.Struct);
        intType.IsValueType.Getter().Returns(true);
        intType.SpecialType.Getter().Returns(SpecialType.System_Int32);

        var field = IFieldSymbol.Imposter();
        field.IsStatic.Getter().Returns(false);
        field.IsConst.Getter().Returns(false);
        field.Type.Getter().Returns(intType.Instance());

        var structType = INamedTypeSymbol.Imposter();
        structType.TypeKind.Getter().Returns(TypeKind.Struct);
        structType.IsValueType.Getter().Returns(true);
        structType.SpecialType.Getter().Returns(SpecialType.None);
        structType.ToDisplayString(Arg<SymbolDisplayFormat?>.Any()).Returns("global::MyStruct");
        structType.GetMembers().Returns([field.Instance()]);

        await Assert.That(TypeClassifier.GetSize(structType.Instance())).IsEqualTo(4);
    }

    [Test]
    public async Task GetSize_StructWithTwoFieldsAndPadding_ReturnsAlignedSize()
    {
        // byte (1) + padding (3) + int (4) = 8, aligned to 4 = 8
        var byteType = ITypeSymbol.Imposter();
        byteType.TypeKind.Getter().Returns(TypeKind.Struct);
        byteType.IsValueType.Getter().Returns(true);
        byteType.SpecialType.Getter().Returns(SpecialType.System_Byte);

        var intType = ITypeSymbol.Imposter();
        intType.TypeKind.Getter().Returns(TypeKind.Struct);
        intType.IsValueType.Getter().Returns(true);
        intType.SpecialType.Getter().Returns(SpecialType.System_Int32);

        var field1 = IFieldSymbol.Imposter();
        field1.IsStatic.Getter().Returns(false);
        field1.IsConst.Getter().Returns(false);
        field1.Type.Getter().Returns(byteType.Instance());

        var field2 = IFieldSymbol.Imposter();
        field2.IsStatic.Getter().Returns(false);
        field2.IsConst.Getter().Returns(false);
        field2.Type.Getter().Returns(intType.Instance());

        var structType = INamedTypeSymbol.Imposter();
        structType.TypeKind.Getter().Returns(TypeKind.Struct);
        structType.IsValueType.Getter().Returns(true);
        structType.SpecialType.Getter().Returns(SpecialType.None);
        structType.ToDisplayString(Arg<SymbolDisplayFormat?>.Any()).Returns("global::MyStruct");
        structType.GetMembers().Returns([field1.Instance(), field2.Instance()]);

        await Assert.That(TypeClassifier.GetSize(structType.Instance())).IsEqualTo(8);
    }

    [Test]
    public async Task GetAlignment_StructWithMultipleFields_ReturnsMaxFieldAlignment()
    {
        var byteType = ITypeSymbol.Imposter();
        byteType.TypeKind.Getter().Returns(TypeKind.Struct);
        byteType.IsValueType.Getter().Returns(true);
        byteType.SpecialType.Getter().Returns(SpecialType.System_Byte);

        var doubleType = ITypeSymbol.Imposter();
        doubleType.TypeKind.Getter().Returns(TypeKind.Struct);
        doubleType.IsValueType.Getter().Returns(true);
        doubleType.SpecialType.Getter().Returns(SpecialType.System_Double);

        var field1 = IFieldSymbol.Imposter();
        field1.IsStatic.Getter().Returns(false);
        field1.IsConst.Getter().Returns(false);
        field1.Type.Getter().Returns(byteType.Instance());

        var field2 = IFieldSymbol.Imposter();
        field2.IsStatic.Getter().Returns(false);
        field2.IsConst.Getter().Returns(false);
        field2.Type.Getter().Returns(doubleType.Instance());

        var structType = INamedTypeSymbol.Imposter();
        structType.TypeKind.Getter().Returns(TypeKind.Struct);
        structType.IsValueType.Getter().Returns(true);
        structType.SpecialType.Getter().Returns(SpecialType.None);
        structType.ToDisplayString(Arg<SymbolDisplayFormat?>.Any()).Returns("global::MyStruct");
        structType.GetMembers().Returns([field1.Instance(), field2.Instance()]);

        await Assert.That(TypeClassifier.GetAlignment(structType.Instance())).IsEqualTo(8);
    }

    [Test]
    public async Task GetSize_StructWithUnknownFieldSize_ReturnsNegativeOne()
    {
        var unknownType = ITypeSymbol.Imposter();
        unknownType.TypeKind.Getter().Returns(TypeKind.TypeParameter);

        var field = IFieldSymbol.Imposter();
        field.IsStatic.Getter().Returns(false);
        field.IsConst.Getter().Returns(false);
        field.Type.Getter().Returns(unknownType.Instance());

        var structType = INamedTypeSymbol.Imposter();
        structType.TypeKind.Getter().Returns(TypeKind.Struct);
        structType.IsValueType.Getter().Returns(true);
        structType.SpecialType.Getter().Returns(SpecialType.None);
        structType.ToDisplayString(Arg<SymbolDisplayFormat?>.Any()).Returns("global::MyStruct");
        structType.GetMembers().Returns([field.Instance()]);

        await Assert.That(TypeClassifier.GetSize(structType.Instance())).IsEqualTo(-1);
    }

    [Test]
    public async Task GetAlignment_StructWithUnknownFieldAlignment_ReturnsNegativeOne()
    {
        var unknownType = ITypeSymbol.Imposter();
        unknownType.TypeKind.Getter().Returns(TypeKind.TypeParameter);

        var field = IFieldSymbol.Imposter();
        field.IsStatic.Getter().Returns(false);
        field.IsConst.Getter().Returns(false);
        field.Type.Getter().Returns(unknownType.Instance());

        var structType = INamedTypeSymbol.Imposter();
        structType.TypeKind.Getter().Returns(TypeKind.Struct);
        structType.IsValueType.Getter().Returns(true);
        structType.SpecialType.Getter().Returns(SpecialType.None);
        structType.ToDisplayString(Arg<SymbolDisplayFormat?>.Any()).Returns("global::MyStruct");
        structType.GetMembers().Returns([field.Instance()]);

        await Assert.That(TypeClassifier.GetAlignment(structType.Instance())).IsEqualTo(-1);
    }

    [Test]
    public async Task GetSize_EmptyStruct_ReturnsZero()
    {
        var structType = INamedTypeSymbol.Imposter();
        structType.TypeKind.Getter().Returns(TypeKind.Struct);
        structType.IsValueType.Getter().Returns(true);
        structType.SpecialType.Getter().Returns(SpecialType.None);
        structType.ToDisplayString(Arg<SymbolDisplayFormat?>.Any()).Returns("global::MyEmptyStruct");
        structType.GetMembers().Returns([]);

        await Assert.That(TypeClassifier.GetSize(structType.Instance())).IsEqualTo(0);
    }

    [Test]
    public async Task GetSize_StructWithOnlyStaticFields_ReturnsZero()
    {
        var intType = ITypeSymbol.Imposter();
        intType.TypeKind.Getter().Returns(TypeKind.Struct);
        intType.IsValueType.Getter().Returns(true);
        intType.SpecialType.Getter().Returns(SpecialType.System_Int32);

        var staticField = IFieldSymbol.Imposter();
        staticField.IsStatic.Getter().Returns(true);
        staticField.IsConst.Getter().Returns(false);
        staticField.Type.Getter().Returns(intType.Instance());

        var constField = IFieldSymbol.Imposter();
        constField.IsStatic.Getter().Returns(false);
        constField.IsConst.Getter().Returns(true);
        constField.Type.Getter().Returns(intType.Instance());

        var structType = INamedTypeSymbol.Imposter();
        structType.TypeKind.Getter().Returns(TypeKind.Struct);
        structType.IsValueType.Getter().Returns(true);
        structType.SpecialType.Getter().Returns(SpecialType.None);
        structType.ToDisplayString(Arg<SymbolDisplayFormat?>.Any()).Returns("global::MyStruct");
        structType.GetMembers().Returns([staticField.Instance(), constField.Instance()]);

        await Assert.That(TypeClassifier.GetSize(structType.Instance())).IsEqualTo(0);
    }

    [Test]
    public async Task GetSize_NonNamedValueType_ReturnsNegativeOne()
    {
        // ITypeSymbol (not INamedTypeSymbol) that is a value type with no special type
        var mock = ITypeSymbol.Imposter();
        mock.TypeKind.Getter().Returns(TypeKind.Struct);
        mock.IsValueType.Getter().Returns(true);
        mock.SpecialType.Getter().Returns(SpecialType.None);
        mock.ToDisplayString(Arg<SymbolDisplayFormat?>.Any()).Returns("global::UnknownStruct");

        await Assert.That(TypeClassifier.GetSize(mock.Instance())).IsEqualTo(-1);
    }

    [Test]
    public async Task GetAlignment_NonNamedValueType_ReturnsNegativeOne()
    {
        var mock = ITypeSymbol.Imposter();
        mock.TypeKind.Getter().Returns(TypeKind.Struct);
        mock.IsValueType.Getter().Returns(true);
        mock.SpecialType.Getter().Returns(SpecialType.None);
        mock.ToDisplayString(Arg<SymbolDisplayFormat?>.Any()).Returns("global::UnknownStruct");

        await Assert.That(TypeClassifier.GetAlignment(mock.Instance())).IsEqualTo(-1);
    }
}
