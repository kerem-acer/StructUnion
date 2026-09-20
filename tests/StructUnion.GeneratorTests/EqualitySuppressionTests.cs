namespace StructUnion.GeneratorTests;

/// <summary>
/// Equality members the user writes are not generated, per member — and <c>GenerateEquality</c>
/// suppresses the whole set. Applies to every union, not just ref-struct ones.
/// </summary>
/// <remarks>
/// Before this, a hand-written <c>Equals(Self)</c> collided as CS0111, and Roslyn anchored that error
/// to whichever declaration it saw second — normally the generated file. So each of these cases needs
/// the compile check, not just a text assertion: CS0111 is precisely what a regression looks like.
/// </remarks>
public class EqualitySuppressionTests
{
    const string UserEqualsSelf = """
        using StructUnion;

        [StructUnion]
        public readonly partial struct Shape
        {
            public static partial Shape Circle(double radius);
            public static partial Shape Square(double side);

            public bool Equals(Shape other) => _tag == other._tag;
        }
        """;

    const string UserEqualityOperator = """
        using StructUnion;

        [StructUnion]
        public readonly partial struct Shape
        {
            public static partial Shape Circle(double radius);
            public static partial Shape Square(double side);

            public static bool operator ==(Shape left, Shape right) => left._tag == right._tag;
            public static bool operator !=(Shape left, Shape right) => !(left == right);
        }
        """;

    const string UserGetHashCode = """
        using StructUnion;

        [StructUnion]
        public readonly partial struct Shape
        {
            public static partial Shape Circle(double radius);
            public static partial Shape Square(double side);

            public override int GetHashCode() => 42;
        }
        """;

    const string UserEqualsObject = """
        using StructUnion;

        [StructUnion]
        public readonly partial struct Shape
        {
            public static partial Shape Circle(double radius);
            public static partial Shape Square(double side);

            public override bool Equals(object? obj) => false;
        }
        """;

    const string EqualityDisabled = """
        using StructUnion;

        [StructUnion(GenerateEquality = false)]
        public readonly partial struct Shape
        {
            public static partial Shape Circle(double radius);
            public static partial Shape Square(double side);
        }
        """;

    // -- No collisions --

    [Test]
    public void UserEqualsSelf_Compiles() => GeneratorTestHelper.AssertGeneratedCompiles(UserEqualsSelf);

    [Test]
    public void UserEqualityOperator_Compiles() => GeneratorTestHelper.AssertGeneratedCompiles(UserEqualityOperator);

    [Test]
    public void UserGetHashCode_Compiles() => GeneratorTestHelper.AssertGeneratedCompiles(UserGetHashCode);

    [Test]
    public void UserEqualsObject_Compiles() => GeneratorTestHelper.AssertGeneratedCompiles(UserEqualsObject);

    [Test]
    public void EqualityDisabled_Compiles() => GeneratorTestHelper.AssertGeneratedCompiles(EqualityDisabled);

    // -- Exactly the written member is suppressed --

    [Test]
    public async Task UserEqualsSelf_KeepsGeneratedDelegators()
    {
        var generated = Generate(UserEqualsSelf);

        await Assert.That(generated).DoesNotContain("public bool Equals(Shape other)");

        // Equals(object), == and != all delegate to Equals(T), so they now call the user's.
        await Assert.That(generated).Contains("public override bool Equals(object? obj) => obj is Shape other && Equals(other);");
        await Assert.That(generated).Contains("operator ==(Shape left, Shape right) => left.Equals(right)");
        await Assert.That(generated).Contains("public override int GetHashCode()");

        // The user's method satisfies the interface, so it still belongs in the base list.
        await Assert.That(generated).Contains("global::System.IEquatable<Shape>");
    }

    [Test]
    public async Task UserEqualityOperator_SuppressesBothOperators()
    {
        var generated = Generate(UserEqualityOperator);

        // C# requires == and != in pairs, so generating the other half would silently pair two
        // operators with different semantics.
        await Assert.That(generated).DoesNotContain("operator ==");
        await Assert.That(generated).DoesNotContain("operator !=");

        // Everything else is untouched.
        await Assert.That(generated).Contains("public bool Equals(Shape other)");
        await Assert.That(generated).Contains("public override int GetHashCode()");
    }

    [Test]
    public async Task UserGetHashCode_SuppressesOnlyGetHashCode()
    {
        var generated = Generate(UserGetHashCode);

        await Assert.That(generated).DoesNotContain("public override int GetHashCode()");
        await Assert.That(generated).Contains("public bool Equals(Shape other)");
        await Assert.That(generated).Contains("operator ==");
    }

    [Test]
    public async Task UserEqualsObject_SuppressesOnlyEqualsObject()
    {
        var generated = Generate(UserEqualsObject);

        await Assert.That(generated).DoesNotContain("public override bool Equals(object? obj)");
        await Assert.That(generated).Contains("public bool Equals(Shape other)");
        await Assert.That(generated).Contains("operator ==");
    }

    [Test]
    public async Task EqualityDisabled_EmitsNothingAndDropsInterface()
    {
        var generated = Generate(EqualityDisabled);

        await Assert.That(generated).DoesNotContain("Equals");
        await Assert.That(generated).DoesNotContain("GetHashCode");
        await Assert.That(generated).DoesNotContain("operator ==");
        await Assert.That(generated).DoesNotContain("IEquatable");

        // With no interfaces left, the declaration must not trail a dangling colon.
        await Assert.That(generated).Contains("public readonly partial struct Shape\n");
    }

    [Test]
    public async Task EqualityDisabled_KeepsEverythingElse()
    {
        var generated = Generate(EqualityDisabled);

        await Assert.That(generated).Contains("public static partial Shape Circle(double radius)");
        await Assert.That(generated).Contains("public bool IsCircle");
        await Assert.That(generated).Contains("public override string ToString()");
    }

    [Test]
    public async Task GenerateEqualityFalse_StillDropsInterfaceAlongsideDispose()
    {
        // Dispose contributes its own base types, so the list is non-empty even without IEquatable —
        // this is the case that would hide a broken separator.
        var source = """
            using System.IO;
            using StructUnion;

            [StructUnion(GenerateEquality = false, GenerateDispose = true)]
            public readonly partial struct Resource
            {
                public static partial Resource File(FileStream stream);
                public static partial Resource Number(int value);
            }
            """;

        GeneratorTestHelper.AssertGeneratedCompiles(source);

        var generated = Generate(source);
        await Assert.That(generated).Contains("public readonly partial struct Resource : global::System.IDisposable");
        await Assert.That(generated).DoesNotContain("IEquatable");
    }

    [Test]
    public async Task AssemblyLevelGenerateEqualityFalse_Applies()
    {
        var source = """
            using StructUnion;

            [assembly: StructUnionOptions(GenerateEquality = false)]

            [StructUnion]
            public readonly partial struct Shape
            {
                public static partial Shape Circle(double radius);
                public static partial Shape Square(double side);
            }
            """;

        var generated = Generate(source);

        await Assert.That(generated).DoesNotContain("IEquatable");
    }

    [Test]
    public async Task PerTypeGenerateEqualityBeatsAssemblyDefault()
    {
        var source = """
            using StructUnion;

            [assembly: StructUnionOptions(GenerateEquality = false)]

            [StructUnion(GenerateEquality = true)]
            public readonly partial struct Shape
            {
                public static partial Shape Circle(double radius);
                public static partial Shape Square(double side);
            }
            """;

        var generated = Generate(source);

        await Assert.That(generated).Contains("global::System.IEquatable<Shape>");
    }

    static string Generate(string source) =>
        string.Concat(GeneratorTestHelper.CreateDriver(source).GetRunResult()
            .GeneratedTrees.Select(t => t.ToString()));
}
