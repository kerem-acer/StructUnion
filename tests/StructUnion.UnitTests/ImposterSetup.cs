using Imposter.Abstractions;
using Microsoft.CodeAnalysis;

[assembly: GenerateImposter(typeof(ITypeSymbol))]
[assembly: GenerateImposter(typeof(INamedTypeSymbol))]
[assembly: GenerateImposter(typeof(ISymbol))]
[assembly: GenerateImposter(typeof(INamespaceSymbol))]
[assembly: GenerateImposter(typeof(ITypeParameterSymbol))]
[assembly: GenerateImposter(typeof(IFieldSymbol))]
