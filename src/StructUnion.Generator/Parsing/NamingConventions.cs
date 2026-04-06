namespace StructUnion.Generator.Parsing;

/// <summary>
/// Handles name derivation for generated struct types from template declarations.
/// </summary>
static class NamingConventions
{
    /// <summary>
    /// Derives the generated struct name from the template type name.
    /// Priority: generatedName from attribute > trim suffix > use as-is.
    /// </summary>
    public static string DeriveStructName(string templateName, string? generatedName, string suffix)
    {
        if (generatedName is not null)
        {
            return generatedName;
        }

        if (suffix.Length > 0 && templateName.EndsWith(suffix) && templateName.Length > suffix.Length)
        {
            return templateName.Substring(0, templateName.Length - suffix.Length);
        }

        return templateName;
    }
}
