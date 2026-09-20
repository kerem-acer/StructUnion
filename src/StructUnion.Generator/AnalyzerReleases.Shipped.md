; Shipped analyzer releases
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

## Release 0.1.0

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
SU0001 | StructUnion | Error | Struct must be partial
SU0002 | StructUnion | Error | Struct must be readonly
SU0003 | StructUnion | Error | No union variants found
SU0004 | StructUnion | Error | Method must return containing struct type
SU0005 | StructUnion | Error | Ref/in/out parameters not supported
SU0006 | StructUnion | Error | Too many variants
SU0007 | StructUnion | Warning | Large struct union
SU0008 | StructUnion | Error | Duplicate variant name (case-insensitive)
SU0009 | StructUnion | Error | Tag property name conflicts with generated member
SU0010 | StructUnion | Error | GeneratedName and TemplateSuffix cannot both be set
SU0011 | StructUnion | Error | Variant name is reserved
SU0012 | StructUnion | Error | Invalid C# identifier
SU0013 | StructUnion | Warning | Variant field is disposable but GenerateDispose is not enabled

## Release 0.2.0

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
SU0014 | StructUnion | Warning | Native union interop requires .NET 11 or a UnionAttribute polyfill
SU0015 | StructUnion | Warning | Native union interop requires C# 15 or later
SU0016 | StructUnion | Error | Native union interop is not supported for unions with common fields
SU0017 | StructUnion | Error | Member name conflicts with a generated nested type

## Release 0.3.0

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
SU0018 | StructUnion | Error | Union carrying a ref struct field must be declared ref
SU0019 | StructUnion | Warning | Ref struct field cannot be compared, so equality was not generated
SU0020 | StructUnion | Error | Native union interop is not supported for unions with ref struct fields
