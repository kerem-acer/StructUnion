# StructUnion

A C# source generator that creates zero-allocation discriminated unions (tagged unions) as structs with explicit memory layout.

## Features

- **Zero-allocation unions** — generates structs with `[StructLayout(LayoutKind.Explicit)]` and `[FieldOffset]` for compact, union-like memory
- **Two definition styles** — partial struct API for simple cases, record template API for variants with common fields
- **Rich generated API** — factory methods, `Is*` checks, property accessors, `TryGet*`, `Match`, equality operators, and `ToString`
- **Implicit conversions** — single-parameter variants with unique types get implicit conversion operators
- **Tag enum** — generates a nested `Tags` enum for use with `switch` expressions
- **Compile-time diagnostics** — 12 analyzer rules (SU0001–SU0012) catch mistakes at build time
- **Wide compatibility** — targets netstandard2.0, netstandard2.1, net6.0, net8.0, and net10.0

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

### Assembly-Level Defaults

Set project-wide defaults with `[StructUnionOptions]`. Per-type attributes override these when set:

```csharp
[assembly: StructUnionOptions(
    TemplateSuffix = "Template",          // strip "Template" instead of "Record" from template names
    TagPropertyName = "Kind",             // default tag property name for all unions
    EnableImplicitConversions = false,    // disable implicit conversions project-wide
    NestedAccessors = true)]              // enable nested accessors project-wide
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

## How It Works

The generator produces structs with `[StructLayout(LayoutKind.Explicit)]` where variant fields overlap at calculated offsets, similar to C unions:

- A **tag field** (`_tag`) of the generated `Tags` enum (`: byte`) at offset 0 identifies the active variant (`Default = 0`, then `1+` per variant)
- **Value-type fields** are packed with proper alignment after the tag
- **Reference-type fields** occupy a separate zone (required by the CLR for GC correctness)
- The struct size equals: tag + padding + max(variant payload sizes)

For example, `Shape` with three double-based variants occupies just 24 bytes: 1 byte tag + 7 bytes padding + 16 bytes payload (2 doubles).

When the generator cannot determine field sizes at compile time — generic type parameters or managed value types (e.g., `ValueTuple<string, int>`) — it falls back to sequential (`Auto`) layout. The generated API is identical; only the internal memory strategy differs.

## Requirements

- .NET SDK 10.0 or later (for building and testing)
- Consumers of the NuGet package can target netstandard2.0+, net6.0+, net8.0+, or net10.0+

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
```

The test suite includes:

- **Unit tests** — parsing, layout calculation, type classification, naming conventions
- **Generator tests** — snapshot-based verification of generated code using [Verify](https://github.com/VerifyTests/Verify)
- **Integration tests** — end-to-end functional tests exercising the generated API

## License

[MIT](LICENSE)
