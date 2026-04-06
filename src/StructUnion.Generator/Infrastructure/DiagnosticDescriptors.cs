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

    public static readonly DiagnosticDescriptor TagPropertyNameConflict = new(
        id: "SU0009",
        title: "Tag property name conflicts with generated member",
        messageFormat: "The tag property name '{0}' conflicts with a generated member on '{1}'. Rename the conflicting member or set TagPropertyName to a different name.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor GeneratedNameAndSuffixConflict = new(
        id: "SU0010",
        title: "GeneratedName and TemplateSuffix cannot both be set",
        messageFormat: "The type '{0}' has both GeneratedName and TemplateSuffix set on [StructUnion]; use one or the other",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor ReservedVariantName = new(
        id: "SU0011",
        title: "Variant name is reserved",
        messageFormat: "The variant name '{0}' on '{1}' is reserved and would conflict with generated members; rename the variant",
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
        "SU0009" => TagPropertyNameConflict,
        "SU0010" => GeneratedNameAndSuffixConflict,
        "SU0011" => ReservedVariantName,
        _ => throw new ArgumentException($"Unknown diagnostic id: {id}", nameof(id))
    };
}
