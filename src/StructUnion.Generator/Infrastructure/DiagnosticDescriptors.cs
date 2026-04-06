using Microsoft.CodeAnalysis;

namespace StructUnion.Generator.Infrastructure;

static class DiagnosticDescriptors
{
    const string Category = "StructUnion";

    public static readonly DiagnosticDescriptor StructMustBePartial = new(
        id: "SU0001",
        title: "Struct must be partial",
        messageFormat: "The struct '{0}' must be declared as partial to use [StructUnion]",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor StructMustBeReadonly = new(
        id: "SU0002",
        title: "Struct must be readonly",
        messageFormat: "The struct '{0}' must be declared as readonly to use [StructUnion]",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor NoVariantsFound = new(
        id: "SU0003",
        title: "No union variants found",
        messageFormat: "The struct '{0}' has no static partial methods returning the struct type; at least one variant is required",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor MethodMustReturnContainingType = new(
        id: "SU0004",
        title: "Method must return containing struct type",
        messageFormat: "The method '{0}' must return '{1}' to be a union variant",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor RefParametersNotSupported = new(
        id: "SU0005",
        title: "Ref/in/out parameters not supported",
        messageFormat: "The variant method '{0}' has a ref/in/out parameter '{1}'; this is not supported",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor TooManyVariants = new(
        id: "SU0006",
        title: "Too many variants",
        messageFormat: "The struct '{0}' has {1} variants; maximum supported is 255 (tag 0 is reserved for default)",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor LargeStructWarning = new(
        id: "SU0007",
        title: "Large struct union",
        messageFormat: "The struct '{0}' has an estimated payload of {1} bytes; consider a class-based union for types exceeding 64 bytes",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor DuplicateVariantNameCaseInsensitive = new(
        id: "SU0008",
        title: "Duplicate variant name (case-insensitive)",
        messageFormat: "The variant '{0}' conflicts with '{1}' when compared case-insensitively; this would produce duplicate field names",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static DiagnosticDescriptor GetById(string id) => id switch
    {
        "SU0001" => StructMustBePartial,
        "SU0002" => StructMustBeReadonly,
        "SU0003" => NoVariantsFound,
        "SU0004" => MethodMustReturnContainingType,
        "SU0005" => RefParametersNotSupported,
        "SU0006" => TooManyVariants,
        "SU0007" => LargeStructWarning,
        "SU0008" => DuplicateVariantNameCaseInsensitive,
        _ => throw new ArgumentException($"Unknown diagnostic id: {id}", nameof(id))
    };
}
