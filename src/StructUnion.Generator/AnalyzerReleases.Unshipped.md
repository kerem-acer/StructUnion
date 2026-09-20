; Unshipped analyzer releases
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
SU0014 | StructUnion | Warning | Native union interop requires .NET 11 or a UnionAttribute polyfill
SU0015 | StructUnion | Warning | Native union interop requires C# 15 or later
SU0016 | StructUnion | Error | Native union interop is not supported for unions with common fields
SU0017 | StructUnion | Error | Member name conflicts with a generated nested type
