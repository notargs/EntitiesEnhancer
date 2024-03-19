using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;

namespace EntitiesEnhancerSourceGenerator
{
    internal record SyntaxParameter(
        INamedTypeSymbol TypeSymbol,
        INamedTypeSymbol QueryTargetTypeSymbol,
        INamedTypeSymbol? WithAllTypeSymbol,
        INamedTypeSymbol? WithAnyTypeSymbol,
        INamedTypeSymbol? WithNoneTypeSymbol
    );

    internal static class SyntaxParameterExtensions
    {
        public static bool IsStructComponentData(this ITypeSymbol typeSymbol) =>
            typeSymbol.AllInterfaces.Any(x =>
                x.Name == "IComponentData" && x.ContainingNamespace.ToString() == "Unity.Entities") &&
            typeSymbol.TypeKind == TypeKind.Struct;

        // Tupleならば展開して返す、そうでなければ長さ1で返す
        public static ImmutableArray<INamedTypeSymbol> ExpandTypeOrTuple(this INamedTypeSymbol typeSymbol) =>
            typeSymbol.IsTupleType
                ? typeSymbol.TupleElements.Select(x => (INamedTypeSymbol)x.Type).ToImmutableArray()
                : ImmutableArray.Create(typeSymbol);

        private static string WrapRefRwIfStructComponentData(this INamedTypeSymbol typeSymbol) =>
            IsStructComponentData(typeSymbol)
                ? $"RefRW<{typeSymbol}>"
                : typeSymbol.ToString();

        public static string GetEnumeratorElementTypeName(this SyntaxParameter syntaxParameter)
        {
            var isTupleType = syntaxParameter.QueryTargetTypeSymbol.IsTupleType;
            if (isTupleType)
            {
                return
                    $"({
                        string.Join(",",
                            syntaxParameter.QueryTargetTypeSymbol.ExpandTypeOrTuple()
                                .Select(WrapRefRwIfStructComponentData)
                        )
                    })";
            }

            return WrapRefRwIfStructComponentData(syntaxParameter.QueryTargetTypeSymbol);
        }

        public static string GetQueryActionTypeName(this SyntaxParameter syntaxParameter) =>
            $"Action<{
                string.Join(",",
                    syntaxParameter.QueryTargetTypeSymbol.ExpandTypeOrTuple()
                        .Select(WrapRefRwIfStructComponentData)
                )}>";


        public static string GetQueryWithEntityActionTypeName(this SyntaxParameter syntaxParameter) =>
            $"Action<{
                string.Join(",",
                    new[] { "Entity" }
                        .Concat(syntaxParameter.QueryTargetTypeSymbol.ExpandTypeOrTuple()
                            .Select(WrapRefRwIfStructComponentData))
                )}>";

        public static string GetSingletonResultTypeName(this SyntaxParameter syntaxParameter)
        {
            if (syntaxParameter.QueryTargetTypeSymbol.IsTupleType)
            {
                return $"({
                    string.Join(",",
                        syntaxParameter.QueryTargetTypeSymbol.ExpandTypeOrTuple()
                            .Select(WrapRefRwIfStructComponentData)
                    )})";
            }

            return WrapRefRwIfStructComponentData(syntaxParameter.QueryTargetTypeSymbol);
        }
        
        public static string GetSingletonWithEntityResultTypeName(this SyntaxParameter syntaxParameter)
        {
            return $"(Entity, {
                string.Join(",",
                    syntaxParameter.QueryTargetTypeSymbol.ExpandTypeOrTuple()
                        .Select(WrapRefRwIfStructComponentData)
                )})";
        }


        public static string GetWithAllText(this SyntaxParameter syntaxParameter) =>
            $".WithAll<{
                string.Join(", ",
                    syntaxParameter.QueryTargetTypeSymbol.ExpandTypeOrTuple()
                        .Concat(syntaxParameter.WithAllTypeSymbol?.ExpandTypeOrTuple() ?? Enumerable.Empty<INamedTypeSymbol>())
                )
            }>()";

        public static string GetWithAnyText(this SyntaxParameter syntaxParameter) =>
            $".WithAny<{
                string.Join(", ",
                    syntaxParameter.WithAnyTypeSymbol?.ExpandTypeOrTuple() ?? Enumerable.Empty<INamedTypeSymbol>()
                )
            }>()";

        public static string GetWithNoneText(this SyntaxParameter syntaxParameter) =>
            $".WithNone<{
                string.Join(", ",
                    syntaxParameter.WithNoneTypeSymbol?.ExpandTypeOrTuple() ?? Enumerable.Empty<INamedTypeSymbol>()
                )
            }>()";

        public static bool MatchNameAndNamespace(this ITypeSymbol typeSymbol, string name, string @namespace) =>
            typeSymbol.ContainingNamespace.ToString() == @namespace && typeSymbol.Name == name;

        public static string ToGetComponentString(this ITypeSymbol typeSymbol) =>
            typeSymbol.IsStructComponentData()
                ? $"GetComponentDataRW<{typeSymbol}>(entity)"
                : $"GetComponentObject<{typeSymbol}>(entity)";

        public static string ToBuildString(this SyntaxParameter syntaxParameter)
        {
            var stringBuilder = new StringBuilder();

            stringBuilder.Append("new EntityQueryBuilder(Allocator.Temp)");
            stringBuilder.Append($"{syntaxParameter.GetWithAllText()}");
            if (syntaxParameter.WithAnyTypeSymbol != null)
                stringBuilder.Append($"{syntaxParameter.GetWithAnyText()}");
            if (syntaxParameter.WithNoneTypeSymbol != null)
                stringBuilder.Append($"{syntaxParameter.GetWithNoneText()}");
            stringBuilder.Append(".Build(entityManager)");

            return stringBuilder.ToString();
        }
    }
}