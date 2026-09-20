namespace StructUnion.NativeUnionTests;

[StructUnion(NativeUnion = true)]
public readonly partial struct Shape
{
    public static partial Shape Circle(double radius);
    public static partial Shape Rectangle(double length, double width);
    public static partial Shape Empty();
}

[StructUnion(NativeUnion = true)]
public readonly partial struct Option<T>
{
    public static partial Option<T> Some(T value);
    public static partial Option<T> None();
}

/// <summary>
/// A variant named <c>Value</c> would collide with the union's own <c>Value</c> member if the
/// union members lived on the struct. They live on the provider interface instead, so this
/// compiles — that is the whole reason for the provider.
/// </summary>
[StructUnion(NativeUnion = true)]
public readonly partial struct Awkward
{
    public static partial Awkward Value(int x);
    public static partial Awkward HasValue(bool flag);
}
