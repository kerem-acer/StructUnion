using System.Collections;
using System.Collections.Immutable;

namespace StructUnion.Generator.Infrastructure;

/// <summary>
/// Value-equatable wrapper around <see cref="ImmutableArray{T}"/> for incremental generator caching.
/// </summary>
readonly struct EquatableArray<T>(ImmutableArray<T> array)
    : IEquatable<EquatableArray<T>>, IReadOnlyList<T>
    where T : IEquatable<T>
{
    public static readonly EquatableArray<T> Empty = new([]);

    readonly ImmutableArray<T> _array = array;

    public ImmutableArray<T> AsImmutableArray() => _array;

    public int Count => _array.IsDefault ? 0 : _array.Length;

    public T this[int index] => _array[index];

    public bool Equals(EquatableArray<T> other)
    {
        if (_array.IsDefault && other._array.IsDefault)
        {
            return true;
        }

        if (_array.IsDefault || other._array.IsDefault)
        {
            return false;
        }

        if (_array.Length != other._array.Length)
        {
            return false;
        }

        for (var i = 0; i < _array.Length; i++)
        {
            if (!_array[i].Equals(other[i]))
            {
                return false;
            }
        }

        return true;
    }

    public override bool Equals(object? obj) => obj is EquatableArray<T> other && Equals(other);

    public override int GetHashCode()
    {
        if (_array.IsDefault)
        {
            return 0;
        }

        unchecked
        {
            var hash = (int)2166136261;
            foreach (var item in _array)
            {
                hash = (hash ^ item.GetHashCode()) * 16777619;
            }
            return hash;
        }
    }

    /// <summary>
    /// Returns a struct enumerator that avoids boxing.
    /// C# foreach resolves this via duck typing over the interface version.
    /// </summary>
    public ImmutableArray<T>.Enumerator GetEnumerator() =>
        _array.IsDefault ? ImmutableArray<T>.Empty.GetEnumerator() : _array.GetEnumerator();

    IEnumerator<T> IEnumerable<T>.GetEnumerator()
    {
        if (_array.IsDefault)
        {
            return ((IEnumerable<T>)[]).GetEnumerator();
        }

        return ((IEnumerable<T>)_array).GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable<T>)this).GetEnumerator();

    public static bool operator ==(EquatableArray<T> left, EquatableArray<T> right) => left.Equals(right);
    public static bool operator !=(EquatableArray<T> left, EquatableArray<T> right) => !left.Equals(right);
}

static class EquatableArrayExtensions
{
    public static EquatableArray<T> ToEquatableArray<T>(this ImmutableArray<T> array)
        where T : IEquatable<T> => new(array);

    public static EquatableArray<T> ToEquatableArray<T>(this IEnumerable<T> source)
        where T : IEquatable<T> => new(source.ToImmutableArray());
}
