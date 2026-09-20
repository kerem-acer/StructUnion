# StructUnion

A C# source generator that creates zero-allocation discriminated unions (tagged unions) as structs with explicit memory layout.

## Features

- **Zero-allocation unions** — generates structs with `[StructLayout(LayoutKind.Explicit)]` and `[FieldOffset]` for compact, union-like memory
- **Two definition styles** — partial struct API for simple cases, record template API for variants with common fields
- **Rich generated API** — factory methods, `Is*` checks, property accessors, `TryGet*`, `Match`, equality operators, and `ToString`
- **Implicit conversions** — single-parameter variants with unique types get implicit conversion operators
- **Tag enum** — generates a nested `Tags` enum for use with `switch` expressions
- **Native C# 15 unions (net11.0)** — opt in with `[StructUnion(NativeUnion = true)]` to get compiler-checked `switch` exhaustiveness with **no boxing**, unlike the language's own `union` declaration
- **Compile-time diagnostics** — 17 analyzer rules (SU0001–SU0017) catch mistakes at build time
- **Wide compatibility** — targets netstandard2.0, netstandard2.1, net6.0, net8.0, net10.0, and net11.0

## Quick Start

Add the NuGet package:

```xml
<PackageReference Include="StructUnion" Version="*" />
```

The source generator is bundled inside the package and activates automatically.

Define a union:

```csharp
using StructUnion;

[StructUnion]
public readonly partial struct Shape
{
    public static partial Shape Circle(double radius);
    public static partial Shape Rectangle(double length, double width);
    public static partial Shape Triangle(double @base, double height);
}
```

Use it:

```csharp
var shape = Shape.Circle(5.0);

// Check variant
if (shape.IsCircle)
    Console.WriteLine(shape.CircleRadius); // 5

// Pattern match
var area = shape.Match(
    r => Math.PI * r * r,
    (l, w) => l * w,
    (b, h) => 0.5 * b * h);

// Try-get
if (shape.TryGetCircle(out var radius))
    Console.WriteLine(radius);

// Switch on tag enum
var name = shape.Tag switch
{
    Shape.Tags.Circle => "circle",
    Shape.Tags.Rectangle => "rectangle",
    Shape.Tags.Triangle => "triangle",
    _ => "unknown"
};

// Equality
var same = Shape.Circle(5.0) == Shape.Circle(5.0); // true

// ToString
Console.WriteLine(shape); // "Circle(5)"
```

## Usage

### Partial Struct API

Define variants as `static partial` methods on a `readonly partial struct`:

```csharp
[StructUnion]
public readonly partial struct OptionInt
{
    public static partial OptionInt Some(int value);
    public static partial OptionInt None();
}

// Implicit conversion (single-parameter variant with unique type)
OptionInt opt = 42;
opt.Match(v => Console.WriteLine(v), () => Console.WriteLine("none"));
```

Variants can have zero or more parameters, and support both value types and reference types:

```csharp
[StructUnion]
public readonly partial struct Payload
{
    public static partial Payload Text(string value);
    public static partial Payload Number(int value);
    public static partial Payload Both(string name, int age);
    public static partial Payload Empty();
}
```

### Generic Unions

Generic type parameters are fully supported:

```csharp
[StructUnion]
public readonly partial struct Option<T>
{
    public static partial Option<T> Some(T value);
    public static partial Option<T> None();
}

[StructUnion]
public readonly partial struct Result<TOk, TError>
{
    public static partial Result<TOk, TError> Ok(TOk value);
    public static partial Result<TOk, TError> Error(TError error);
}

Option<int> opt = Option<int>.Some(42);
Result<string, Exception> result = Result<string, Exception>.Ok("hello");
```

Generic unions use sequential (`Auto`) layout since type sizes are unknown at generation time. Constraints (`where T : struct`, `where T : class`, etc.) are preserved on the generated struct.

### Record Template API

Define variants as nested types inside a `partial record` or `partial class`. This style supports **common fields** shared across all variants:

```csharp
[StructUnion]
public partial record ShapeRecord(int Common)
{
    public record Circle(double Radius);
    public record Rectangle(double Length, double Width);
    public record Triangle(double Base, double Height);
}
```

The generator strips the `Record` suffix to produce a struct named `Shape`:

```csharp
var shape = Shape.Circle(42, 5.0); // common field + variant fields
Console.WriteLine(shape.Common);       // 42
Console.WriteLine(shape.CircleRadius); // 5
```

For more complex types with multiple reference-type variants:

```csharp
[StructUnion]
public partial record JsonValueRecord
{
    public record Str(string Value);
    public record Num(double Value);
    public record Bool(bool Value);
    public record Arr(object[] Items);
    public record Obj(Dictionary<string, object> Data);
    public record Null();
}

var v = JsonValue.Num(3.14);
var result = v.Match(
    s => "string",
    n => $"number: {n}",
    b => "bool",
    a => "array",
    d => "object",
    () => "null");
```

## Generated API

For each union, the generator produces:

| Member | Example | Description |
|--------|---------|-------------|
| `Tags` enum | `Shape.Tags.Circle` | Nested enum (`: byte`) with a member per variant and `Default = 0` |
| `Tag` property | `shape.Tag` | Returns the `Tags` enum value for the active variant |
| Factory method | `Shape.Circle(5.0)` | Creates an instance of the variant |
| `Is*` property | `shape.IsCircle` | Returns `true` if the instance is that variant |
| Property accessor | `shape.CircleRadius` | Gets the variant's field value (throws if wrong variant) |
| `TryGet*` method | `shape.TryGetCircle(out var r)` | Returns `true` and extracts fields via `out` parameters |
| `Match<T>` | `shape.Match(...)` | Exhaustive pattern match returning `T` |
| `Match<TState, T>` | `shape.Match(state, ...)` | Stateful match (avoids closure allocations) |
| `Match` (void) | `shape.Match(...)` | Exhaustive pattern match with `Action` delegates |
| `==` / `!=` | `a == b` | Structural equality by variant tag and field values |
| `Equals` / `GetHashCode` | `a.Equals(b)` | Implements `IEquatable<T>` |
| `ToString` | `shape.ToString()` | Formats as `"Variant(field1, field2)"` |
| Implicit conversion | `OptionInt opt = 42;` | For single-parameter variants with unique types |
| `IsDefault` | `shape.IsDefault` | Returns `true` for `default(Shape)` (tag 0, no variant) |
| `Dispose` / `DisposeAsync` | `using var r = ...;` | Opt-in via `GenerateDispose = true`; disposes the active variant's resources |
| `Take*` / `TryTake*` | `Resource.TakeFile(ref r)` | Opt-in ownership-transfer helpers; extract a disposable and zero the union |

## Configuration

### Custom Generated Name

Override the generated struct name for record templates:

```csharp
[StructUnion("Shape")]
public partial record ShapeData
{
    public record Circle(double Radius);
    public record Rectangle(double Length, double Width);
}
// Generates a struct named "Shape" instead of deriving from "ShapeData"
```

### Disable Implicit Conversions

```csharp
[StructUnion(EnableImplicitConversions = false)]
public readonly partial struct OptionInt { ... }
```

### Custom Tag Property Name

By default, the tag property is named `Tag`. If this conflicts with a variant or common field name, use a custom name:

```csharp
[StructUnion(TagPropertyName = "Kind")]
public readonly partial struct Event
{
    public static partial Event Tag(string value); // "Tag" variant is now fine
    public static partial Event Click(int x, int y);
}

// Usage: event.Kind == Event.Tags.Click
```

### Nested Accessors

By default, variant fields are exposed as flat properties like `shape.CircleRadius`. Enable nested accessors to generate a `Cases` class with a readonly struct per variant, accessed via `As{Variant}` properties:

```csharp
[StructUnion(NestedAccessors = true)]
public readonly partial struct DrawCmd
{
    public static partial DrawCmd MoveTo(double x, double y);
    public static partial DrawCmd LineTo(double x, double y);
    public static partial DrawCmd Close();
}

var cmd = DrawCmd.MoveTo(10, 20);
cmd.AsMoveTo.X  // 10
cmd.AsMoveTo.Y  // 20

// TryGet returns the variant struct
if (cmd.TryGetMoveTo(out var move))
    Console.WriteLine($"{move.X}, {move.Y}");

// Variants can have duplicate field names (both have X, Y)
cmd.AsLineTo.X
```

The generated `Cases` class contains a readonly struct per variant:

```csharp
// Generated:
public static class Cases
{
    public readonly struct MoveTo { public double X { get; } public double Y { get; } ... }
    public readonly struct LineTo { public double X { get; } public double Y { get; } ... }
}
```

### Disposable Support

Set `GenerateDispose = true` to make the union a first-class owner of disposable resources. The generator then:

- Implements `IDisposable` (and `IAsyncDisposable` if any variant field implements it).
- Emits `Dispose()` / `DisposeAsync()` that switch on the active tag and dispose only that variant's fields. Safe on `default(T)` and on variants without disposables.
- Emits `Take{Variant}` and `TryTake{Variant}` static helpers (`ref T self`) that extract the variant's payload and reset `self` to `default`, so a subsequent `Dispose()` is a no-op. Use these to transfer ownership out of a union without double-disposing.

```csharp
[StructUnion(GenerateDispose = true)]
public readonly partial struct Resource
{
    public static partial Resource File(System.IO.FileStream stream);   // owns the stream
    public static partial Resource Buffer(System.Buffers.IMemoryOwner<byte> owner);
    public static partial Resource Inline(int value);                   // not disposable
}

// Disposes the FileStream automatically.
using var r = Resource.File(File.OpenRead(path));

// No-op for variants without disposables.
using var inline = Resource.Inline(42);

// Default instances are safe.
default(Resource).Dispose();

// Ownership transfer: caller now owns the stream; r is left as default.
var stream = Resource.TakeFile(ref r);
```

Detection is **statically known only** — concrete types and `where T : IDisposable` constraints are picked up; unconstrained generic parameters are not. If a variant carries a disposable field but `GenerateDispose` is off, the generator reports `SU0013` so the choice is explicit.

For async-only resources, prefer `await using`:

```csharp
await using var r = Resource.File(asyncDisposable);
```

Can be set assembly-wide via `[assembly: StructUnionOptions(GenerateDispose = true)]`.

### Ref Struct Fields

Variants can carry `Span<T>`, `ReadOnlySpan<T>`, or any `ref struct`. Declare the union `ref` and the
rest follows:

```csharp
[StructUnion]
public readonly ref partial struct Payload
{
    public static partial Payload Bytes(Span<byte> data);
    public static partial Payload Text(ReadOnlySpan<char> text);
    public static partial Payload Number(int value);
}

Span<byte> buffer = stackalloc byte[3];
var p = Payload.Bytes(buffer);
int length = p.Match(
    bytes: b => b.Length,
    text: t => t.Length,
    number: n => n);
```

The generator will **not** add `ref` for you — it reports `SU0018` instead. Adding it silently would
make your type non-boxable, unusable as a field of a class and unusable in a `List<T>`, so the change
belongs in your declaration where it is visible.

Ref safety is preserved end to end. A stack buffer still cannot escape through a generated factory:

```csharp
static Payload Leak()
{
    Span<byte> local = stackalloc byte[4];
    return Payload.Bytes(local); // CS8347: may expose variables outside their declaration scope
}
```

**Equality is pointer identity, not element equality.** `Span<T>.operator ==` compares reference and
length, so two spans over distinct buffers with identical contents are not equal:

```csharp
byte[] a = [1, 2, 3];
Payload.Bytes(a) == Payload.Bytes(a);             // true
Payload.Bytes(a) == Payload.Bytes([1, 2, 3]);     // false — different buffer
```

Three further consequences of the CLR's rules, all handled automatically:

- **Layout is always `Auto`.** A byref has no representation at a fixed `FieldOffset`.
- **`GetHashCode` skips ref-like fields**, hashing the tag and the remaining fields. Equal values still
  hash equally — the hash just distinguishes fewer values.
- **`Equals(object)` throws** and is marked `[Obsolete(error: true)]`, following the `Span<T>`
  precedent, because a ref struct can never be boxed to reach it.

A `ref struct` of your own must declare `operator ==` or implement `IEquatable<itself>` to be
comparable — a ref struct cannot be a generic type argument to `EqualityComparer<T>`, and cannot be
boxed to reach `object.Equals`, so there is no fallback. Without one, equality is skipped for the whole
union and the generator reports `SU0019`.

Generic unions work too, with `allows ref struct`. Since `T` has no statically known comparison, these
always take the `SU0019` path:

```csharp
[StructUnion]
public readonly ref partial struct Box<T> where T : allows ref struct
{
    public static partial Box<T> Some(T value);
    public static partial Box<T> None();
}
```

Not supported together with `NativeUnion` (`SU0020`): the union contract exposes cases through a boxing
`object? Value`.

### Controlling Equality

By default the generated struct implements `IEquatable<T>` and gets `Equals`, `GetHashCode`, `==` and
`!=`. Writing any of those yourself suppresses **just that member** — the rest still generate and
delegate to yours:

```csharp
[StructUnion]
public readonly partial struct Shape
{
    public static partial Shape Circle(double radius);
    public static partial Shape Square(double side);

    // Generated Equals(object), == and != now call this one.
    public bool Equals(Shape other) => Tag == other.Tag;
}
```

`==` and `!=` are treated as a pair, since C# requires them together — declaring one suppresses both.

To suppress the whole set, set `GenerateEquality = false`:

```csharp
[StructUnion(GenerateEquality = false)]
public readonly partial struct Shape { /* ... */ }
```

That drops `IEquatable<T>` from the base list along with every equality member. Note that a
hand-written `Equals` combined with a *generated* `GetHashCode` can break the equal-implies-same-hash
contract if your equality is looser than field equality — override `GetHashCode` too in that case. Can
be set assembly-wide via `[assembly: StructUnionOptions(GenerateEquality = false)]`.

### Native C# 15 Unions

C# 15 (.NET 11) lets any type opt into being a *union type*, gaining implicit conversions from
each case type and **compiler-checked `switch` exhaustiveness**. The language's own `union`
declaration pays for that with allocation — it stores a single `object?`, so every value-type case
is boxed on entry. StructUnion gives you the same language behaviour on top of its packed,
explicit-layout storage, with no boxing at all.

Set `NativeUnion = true`:

```csharp
[StructUnion(NativeUnion = true)]
public readonly partial struct Shape
{
    public static partial Shape Circle(double radius);
    public static partial Shape Rectangle(double length, double width);
    public static partial Shape Empty();
}
```

Each variant gets a case type under `Cases`. Import them with `using static` so switch arms read
as bare names:

```csharp
using static MyApp.Shape.Cases;

var area = shape switch      // exhaustive — no `_` arm, and no allocation
{
    Circle c                         => Math.PI * c.Radius * c.Radius,
    Rectangle(var length, var width) => length * width,
    Empty                            => 0,
    null                             => 0,
};

Shape s = new Rectangle(2, 3);       // implicit union conversion
if (shape is Circle circle) { ... }
```

Add a variant later and every incomplete `switch` in your codebase becomes a compiler warning.
Everything StructUnion already generates — `Match`, `TryGet*`, `Tag`, equality, `ToString`,
`Dispose` — keeps working alongside it.

| | `union` keyword | `[StructUnion(NativeUnion = true)]` |
|---|---|---|
| Storage | one `object?` | explicit layout, overlapping fields |
| Value-type cases | boxed on entry | stored inline, never boxed |
| Pattern matching | reads `Value` (boxes) | calls `TryGetValue` (no box, no defensive copy) |
| Exhaustiveness checking | yes | yes |
| Case types | existing types you name | generated `Cases.{Variant}`, one per variant |

**Handle `default`.** Every case type is a non-nullable struct, so the compiler considers a switch
over all of them exhaustive and will *not* ask you for a `null` arm. But `default(Shape)` has no
active variant, and matches `null` — so without that arm it throws `SwitchExpressionException` at
runtime. Add `null => ...` (as above) or guard with `IsDefault` wherever a default instance can
reach the switch. This mirrors the existing `Match()` behaviour, which throws on `default` too.

**Opting in changes how patterns bind.** A union type's patterns unwrap to its contents, so
`shape is Shape` becomes a compile error and `shape is { Tag: Shape.Tags.Circle }` starts matching
the contents rather than the union. That is why the flag is off by default; unions that have not
opted in are unaffected.

**Requirements.** The consuming project must target `net11.0` (or provide a `UnionAttribute`
polyfill) and set `<LangVersion>preview</LangVersion>`. Otherwise the generator reports `SU0014` or
`SU0015` — warnings, not errors, so a multi-targeting library can set the flag once:

```xml
<NoWarn Condition="'$(TargetFramework)' != 'net11.0'">$(NoWarn);SU0014</NoWarn>
```

Not supported together with common fields — a case type cannot carry them, so `Create` and `Value`
would silently drop them (`SU0016`). Can be set assembly-wide via
`[assembly: StructUnionOptions(NativeUnion = true)]`.

### Assembly-Level Defaults

Set project-wide defaults with `[StructUnionOptions]`. Per-type attributes override these when set:

```csharp
[assembly: StructUnionOptions(
    TemplateSuffix = "Template",          // strip "Template" instead of "Record" from template names
    TagPropertyName = "Kind",             // default tag property name for all unions
    EnableImplicitConversions = false,    // disable implicit conversions project-wide
    NestedAccessors = true,               // enable nested accessors project-wide
    GenerateDispose = true,               // generate Dispose/DisposeAsync where applicable
    NativeUnion = true,                   // emit C# 15 union members (net11.0)
    GenerateEquality = false)]            // suppress Equals/GetHashCode/==/!= project-wide
```

## Diagnostics

| ID | Severity | Description |
|----|----------|-------------|
| SU0001 | Error | Struct must be declared as `partial` |
| SU0002 | Error | Struct must be declared as `readonly` |
| SU0003 | Error | No variant methods found (at least one required) |
| SU0004 | Error | Variant method must return the containing struct type |
| SU0005 | Error | `ref`/`in`/`out` parameters are not supported on variants |
| SU0006 | Error | Too many variants (maximum 255) |
| SU0007 | Warning | Large struct payload (consider a class-based union above 64 bytes) |
| SU0008 | Error | Duplicate variant names (case-insensitive) |
| SU0009 | Error | Tag property name conflicts with a variant or common field name |
| SU0010 | Error | `GeneratedName` and `TemplateSuffix` cannot both be set |
| SU0011 | Error | Variant name is reserved (conflicts with generated `Tags` enum) |
| SU0012 | Error | Invalid C# identifier for `GeneratedName` or `TagPropertyName` |
| SU0013 | Warning | Variant field is disposable but `GenerateDispose` is not enabled |
| SU0014 | Warning | `NativeUnion` requires a target framework providing `UnionAttribute` (net11.0+) |
| SU0015 | Warning | `NativeUnion` requires C# 15 (`<LangVersion>preview</LangVersion>`) |
| SU0016 | Error | `NativeUnion` is not supported for unions with common fields |
| SU0017 | Error | Member name conflicts with a generated nested type (`Cases`, `IUnionMembers`) |
| SU0018 | Error | Union carries a `ref struct` field but is not declared `ref` |
| SU0019 | Warning | A `ref struct` field declares neither `operator ==` nor `IEquatable<itself>`, so equality was not generated |
| SU0020 | Error | `NativeUnion` is not supported for unions with `ref struct` fields |

## How It Works

The generator produces structs with `[StructLayout(LayoutKind.Explicit)]` where variant fields overlap at calculated offsets, similar to C unions:

- A **tag field** (`_tag`) of the generated `Tags` enum (`: byte`) at offset 0 identifies the active variant (`Default = 0`, then `1+` per variant)
- **Value-type fields** are packed with proper alignment after the tag
- **Reference-type fields** occupy a separate zone (required by the CLR for GC correctness)
- The struct size equals: tag + padding + max(variant payload sizes)

For example, `Shape` with three double-based variants occupies just 24 bytes: 1 byte tag + 7 bytes padding + 16 bytes payload (2 doubles).

When a field cannot sit at an explicit offset, the generator falls back to sequential (`Auto`) layout. That happens for unknowable sizes (generic type parameters), managed value types (e.g. `ValueTuple<string, int>`, whose GC references the CLR cannot track through overlapping fields), and `ref struct` fields (a byref has no representation at a fixed offset). The generated API is identical; only the internal memory strategy differs.

## Requirements

- **Building this repo:** .NET SDK 11.0.100-rc.1 or later, pinned in `global.json`. SDKs install
  side by side, so existing .NET 10 installs are unaffected, and `rollForward: latestFeature`
  picks up the 11.0 GA SDK automatically when it ships. Running the test suite also needs the
  .NET 10 runtime, because three of the four test projects target `net10.0`.
- **Consumers:** target netstandard2.0+, netstandard2.1+, net6.0+, net8.0+, net10.0+, or net11.0+.
- **`NativeUnion` mode:** requires the consuming project to target `net11.0` and set
  `<LangVersion>preview</LangVersion>`. On any earlier target the generator reports `SU0014` and
  generates the standard API without the union members.
- **Ref struct fields:** a `Span<T>` or `ref struct` variant needs no particular target — any framework
  where the type is available will do. The *generic* form, `where T : allows ref struct`, is C# 13 and
  so needs `net9.0` or later.

## Building

```bash
dotnet build
```

## Testing

```bash
# Run all tests
dotnet test

# Run specific test projects
dotnet test tests/StructUnion.UnitTests
dotnet test tests/StructUnion.GeneratorTests
dotnet test tests/StructUnion.IntegrationTests
dotnet test tests/StructUnion.NativeUnionTests
```

The test suite includes:

- **Unit tests** — parsing, layout calculation, type classification, naming conventions
- **Generator tests** — snapshot-based verification of generated code using [Verify](https://github.com/VerifyTests/Verify)
- **Integration tests** — end-to-end functional tests exercising the generated API
- **Native union tests** — a `net11.0` project that compiles real C# 15 `switch` expressions over
  generated unions, so a missing arm fails the build, and asserts that matching allocates 0 bytes

## License

[MIT](LICENSE)
