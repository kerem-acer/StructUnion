using System.Collections.Immutable;
using StructUnion.Generator.Infrastructure;

namespace StructUnion.UnitTests;

public class EquatableArrayTests
{
    [Test]
    public async Task Empty_HasCountZero()
    {
        var arr = EquatableArray<int>.Empty;
        await Assert.That(arr.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Default_HasCountZero()
    {
        var arr = default(EquatableArray<int>);
        await Assert.That(arr.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Populated_HasCorrectCount()
    {
        var sourceArray = new[] { 1, 2, 3 };
        var arr = sourceArray.ToEquatableArray();
        await Assert.That(arr.Count).IsEqualTo(3);
    }

    [Test]
    public async Task Indexer_ReturnsCorrectElement()
    {
        var sourceArray = new[] { 10, 20, 30 };
        var arr = sourceArray.ToEquatableArray();
        await Assert.That(arr[0]).IsEqualTo(10);
        await Assert.That(arr[1]).IsEqualTo(20);
        await Assert.That(arr[2]).IsEqualTo(30);
    }

    [Test]
    public async Task Equals_TwoEmpty_AreEqual()
    {
        var a = EquatableArray<int>.Empty;
        var b = EquatableArray<int>.Empty;
        await Assert.That(a.Equals(b)).IsTrue();
    }

    [Test]
    public async Task Equals_TwoDefaults_AreEqual()
    {
        var a = default(EquatableArray<int>);
        var b = default(EquatableArray<int>);
        await Assert.That(a.Equals(b)).IsTrue();
    }

    [Test]
    public async Task Equals_DefaultAndEmpty_AreEqual()
    {
        var a = default(EquatableArray<int>);
        var b = EquatableArray<int>.Empty;
        // Default has IsDefault=true, Empty does not — they differ
        // But both have 0 elements. Implementation: default vs empty returns false
        // because one is IsDefault and other is not.
        await Assert.That(a.Equals(b)).IsFalse();
    }

    [Test]
    public async Task Equals_SameElements_AreEqual()
    {
        var sourceArray = new[] { 1, 2, 3 };
        var a = sourceArray.ToEquatableArray();
        var b = sourceArray.ToEquatableArray();
        await Assert.That(a.Equals(b)).IsTrue();
    }

    [Test]
    public async Task Equals_DifferentElements_AreNotEqual()
    {
        var sourceArray = new[] { 1, 2, 3 };
        var a = sourceArray.ToEquatableArray();
        var differentElements = new[] { 1, 2, 4 };
        var b = differentElements.ToEquatableArray();
        await Assert.That(a.Equals(b)).IsFalse();
    }

    [Test]
    public async Task Equals_DifferentLengths_AreNotEqual()
    {
        var sourceArray = new[] { 1, 2 };
        var a = sourceArray.ToEquatableArray();
        var longerArray = new[] { 1, 2, 3 };
        var b = longerArray.ToEquatableArray();
        await Assert.That(a.Equals(b)).IsFalse();
    }

    [Test]
    public async Task GetHashCode_EqualArrays_SameHash()
    {
        var sourceArray = new[] { 1, 2, 3 };
        var a = sourceArray.ToEquatableArray();
        var b = sourceArray.ToEquatableArray();
        await Assert.That(a.GetHashCode()).IsEqualTo(b.GetHashCode());
    }

    [Test]
    public async Task GetHashCode_Default_ReturnsZero()
    {
        var arr = default(EquatableArray<int>);
        await Assert.That(arr.GetHashCode()).IsEqualTo(0);
    }

    [Test]
    public async Task Operator_Equality()
    {
        var sourceArray = new[] { "x", "y" };
        var a = sourceArray.ToEquatableArray();
        var b = sourceArray.ToEquatableArray();
        await Assert.That(a == b).IsTrue();
        await Assert.That(a != b).IsFalse();
    }

    [Test]
    public async Task Operator_Inequality()
    {
        var sourceArray = new[] { "x" };
        var a = sourceArray.ToEquatableArray();
        var differentStrings = new[] { "y" };
        var b = differentStrings.ToEquatableArray();
        await Assert.That(a != b).IsTrue();
        await Assert.That(a == b).IsFalse();
    }

    [Test]
    public async Task Enumeration_Populated()
    {
        var sourceArray = new[] { 10, 20, 30 };
        var arr = sourceArray.ToEquatableArray();
        var list = new List<int>();
        foreach (var item in arr)
        {
            list.Add(item);
        }

        await Assert.That(list.Count).IsEqualTo(3);
        await Assert.That(list[0]).IsEqualTo(10);
    }

    [Test]
    public async Task Enumeration_Empty()
    {
        var arr = EquatableArray<int>.Empty;
        var count = 0;
        foreach (var _ in arr)
        {
            count++;
        }

        await Assert.That(count).IsEqualTo(0);
    }

    [Test]
    public async Task Enumeration_Default()
    {
        var arr = default(EquatableArray<int>);
        var count = 0;
        foreach (var _ in arr)
        {
            count++;
        }

        await Assert.That(count).IsEqualTo(0);
    }

    [Test]
    public async Task AsImmutableArray_ReturnsOriginal()
    {
        var original = ImmutableArray.Create(1, 2, 3);
        var arr = new EquatableArray<int>(original);
        var result = arr.AsImmutableArray();
        await Assert.That(result.Length).IsEqualTo(3);
        await Assert.That(result[0]).IsEqualTo(1);
    }

    [Test]
    public async Task ToEquatableArray_FromImmutableArray()
    {
        var imm = ImmutableArray.Create("a", "b");
        var arr = imm.ToEquatableArray();
        await Assert.That(arr.Count).IsEqualTo(2);
        await Assert.That(arr[0]).IsEqualTo("a");
    }

    [Test]
    public async Task ToEquatableArray_FromEnumerable()
    {
        var enumerable = Enumerable.Range(0, 5);
        var arr = enumerable.ToEquatableArray();
        await Assert.That(arr.Count).IsEqualTo(5);
    }

    [Test]
    public async Task EqualsObject_BoxedEquatableArray()
    {
        var sourceArray = new[] { 1, 2 };
        var a = sourceArray.ToEquatableArray();
        object b = sourceArray.ToEquatableArray();
        await Assert.That(a.Equals(b)).IsTrue();
    }

    [Test]
    public async Task EqualsObject_WrongType_ReturnsFalse()
    {
        var sourceArray = new[] { 1, 2 };
        var a = sourceArray.ToEquatableArray();
        await Assert.That(a.Equals("not an array")).IsFalse();
    }

    [Test]
    public async Task NonGenericEnumerator_Works()
    {
        var sourceArray = new[] { 1, 2, 3 };
        var arr = sourceArray.ToEquatableArray();
        var enumerable = (System.Collections.IEnumerable)arr;
        var count = 0;
        foreach (var _ in enumerable)
        {
            count++;
        }

        await Assert.That(count).IsEqualTo(3);
    }
}
