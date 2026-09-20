; Unshipped analyzer releases
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
SU0018 | StructUnion | Error | Union carrying a ref struct field must be declared ref
SU0019 | StructUnion | Warning | Ref struct field cannot be compared, so equality was not generated
SU0020 | StructUnion | Error | Native union interop is not supported for unions with ref struct fields
