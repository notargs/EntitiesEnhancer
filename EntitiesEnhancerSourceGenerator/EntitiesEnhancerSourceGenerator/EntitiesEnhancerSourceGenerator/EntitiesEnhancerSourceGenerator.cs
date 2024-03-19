using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace EntitiesEnhancerSourceGenerator;

[Generator]
public class FlatQuerySourceGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        context.RegisterPostInitializationOutput(ctx =>
        {
            ctx.AddSource("FlatQuery.g.cs",
                """
                using Unity.Entities;

                namespace EntitiesEnhancer;

                internal record struct FlatQuery<TQueryTarget>();

                internal record struct FlatQuery<TQueryTarget, TQueryOption>();

                internal record struct FlatWithAll<TDesc>;
                internal record struct FlatWithAny<TDesc>;
                internal record struct FlatWithNone<TDesc>;

                internal static class EntityManagerExtensions
                {
                    public static FlatQuery<TQueryTarget> Explicit<TQueryTarget>(this FlatQuery<TQueryTarget> query)
                    {
                        return query;
                    }
                    
                    public static FlatQuery<TQueryTarget, TQueryOption> Explicit<TQueryTarget, TQueryOption>(this FlatQuery<TQueryTarget, TQueryOption> query)
                    {
                        return query;
                    }
                }
                """
            );
        });

        var objectCreationSyntaxProvider = context.SyntaxProvider.CreateSyntaxProvider(
                (s, _) => s is BaseObjectCreationExpressionSyntax,
                (generatorSyntaxContext, _) =>
                {
                    var model = generatorSyntaxContext.SemanticModel;
                    TypeInfo typeInfo;
                    switch (generatorSyntaxContext.Node)
                    {
                        case BaseObjectCreationExpressionSyntax objectCreationExpressionSyntax:
                            typeInfo = model.GetTypeInfo(objectCreationExpressionSyntax);
                            break;
                        default:
                            return null;
                    }

                    // EntitiesEnhancer.FlatQuery<>でなければスキップ
                    if (typeInfo.Type is not INamedTypeSymbol typeSymbol) return null;
                    if (!typeSymbol.MatchNameAndNamespace("FlatQuery", "EntitiesEnhancer")) return null;

                    var hasQueryOption = typeSymbol.TypeArguments.Length > 1;

                    var queryOptionTypeDictionary = new Dictionary<string, INamedTypeSymbol>();
                    if (hasQueryOption)
                    {
                        foreach (var namedTypeSymbol in ((INamedTypeSymbol)typeSymbol.TypeArguments[1])
                                 .ExpandTypeOrTuple())
                        {
                            // "namespace.className"をキーにして登録
                            var key = $"{namedTypeSymbol.ContainingNamespace}.{namedTypeSymbol.Name}";
                            queryOptionTypeDictionary[key] = namedTypeSymbol;
                        }
                    }

                    return new SyntaxParameter(
                        typeSymbol,
                        (INamedTypeSymbol)typeSymbol.TypeArguments[0],
                        queryOptionTypeDictionary.TryGetValue("EntitiesEnhancer.FlatWithAll", out var withAllTypeSymbol)
                            ? (INamedTypeSymbol)withAllTypeSymbol.TypeArguments[0]
                            : null,
                        queryOptionTypeDictionary.TryGetValue("EntitiesEnhancer.FlatWithAny", out var withAnyTypeSymbol)
                            ? (INamedTypeSymbol)withAnyTypeSymbol.TypeArguments[0]
                            : null,
                        queryOptionTypeDictionary.TryGetValue("EntitiesEnhancer.FlatWithNone",
                            out var withNoneTypeSymbol)
                            ? (INamedTypeSymbol)withNoneTypeSymbol.TypeArguments[0]
                            : null
                    );
                }).Where(x => x != null)
            .Select((x, _) => x!);

        context.RegisterSourceOutput(
            context.CompilationProvider.Combine(objectCreationSyntaxProvider.Collect()),
            (ctx, tuple) =>
            {
                var stringBuilder = new StringBuilder();
                stringBuilder.AppendLine(
                    """
                    using System;
                    using System.Collections.Generic;
                    using Unity.Collections;
                    using Unity.Entities;

                    namespace EntitiesEnhancer;

                    internal static class FlatQueryExtension
                    {
                    """);

                var registeredTypes = new HashSet<string>();
                foreach (var syntaxParameter in tuple.Right.Where(syntaxParameter =>
                             registeredTypes.Add(syntaxParameter.TypeSymbol.ToString())))
                {
                    //-----------------------------------------
                    // Query without Entity

                    stringBuilder.AppendLine(
                        $$"""
                                public static void Query(this {{syntaxParameter.TypeSymbol.ToDisplayString()}} query, EntityManager entityManager, {{syntaxParameter.GetQueryActionTypeName()}} action)
                                {
                          """);

                    stringBuilder.AppendLine($"          var entityQuery = {syntaxParameter.ToBuildString()};");
                    stringBuilder.AppendLine("          var entities = entityQuery.ToEntityArray(Allocator.Temp);");
                    foreach (var namedTypeSymbol in syntaxParameter.QueryTargetTypeSymbol.ExpandTypeOrTuple().Where(namedTypeSymbol => namedTypeSymbol.IsStructComponentData()))
                    {
                        stringBuilder.AppendLine(
                            $"          var {namedTypeSymbol.Name.ToLower()} = entityQuery.ToComponentDataArray<{namedTypeSymbol}>(Allocator.Temp);"
                        );
                    }

                    // for
                    stringBuilder.AppendLine(
                        """
                                  for (int i = 0; i < entities.Length; i++)
                                  {
                        """
                    );

                    // actionを実行
                    stringBuilder.AppendLine(
                        $"              action.Invoke({string.Join(", ",
                            syntaxParameter.QueryTargetTypeSymbol.ExpandTypeOrTuple()
                                .Select(x => x.IsStructComponentData() ?
                                    $"new RefRW<{x.ToDisplayString()}>({x.Name.ToLower()}, i)"
                                    : $"entityManager.GetComponentObject<{x.ToDisplayString()}>(entities[i])"
                                )
                        )});"
                    );

                    stringBuilder.AppendLine("          }");

                    // NativeArrayに情報を戻す
                    foreach (var namedTypeSymbol in syntaxParameter.QueryTargetTypeSymbol.ExpandTypeOrTuple()
                                 .Where(namedTypeSymbol => namedTypeSymbol.IsStructComponentData()))
                    {
                        // CopyFromComponentDataArray
                        stringBuilder.AppendLine(
                            $"          entityQuery.CopyFromComponentDataArray({namedTypeSymbol.Name.ToLower()});"
                        );
                    }

                    stringBuilder.AppendLine( "      }"  );

                    //-----------------------------------------
                    // Query with Entity

                    stringBuilder.AppendLine(
                        $$"""
                                public static void Query(this {{syntaxParameter.TypeSymbol.ToDisplayString()}} query, EntityManager entityManager, {{syntaxParameter.GetQueryWithEntityActionTypeName()}} action)
                                {
                          """);

                    stringBuilder.AppendLine($"          var entityQuery = {syntaxParameter.ToBuildString()};");
                    stringBuilder.AppendLine("          var entities = entityQuery.ToEntityArray(Allocator.Temp);");
                    foreach (var namedTypeSymbol in syntaxParameter.QueryTargetTypeSymbol.ExpandTypeOrTuple()
                                 .Where(namedTypeSymbol => namedTypeSymbol.IsStructComponentData()))
                    {
                        stringBuilder.AppendLine(
                            $"          var {namedTypeSymbol.Name.ToLower()} = entityQuery.ToComponentDataArray<{namedTypeSymbol}>(Allocator.Temp);"
                        );
                    }

                    // for
                    stringBuilder.AppendLine(
                        """
                                  for (int i = 0; i < entities.Length; i++)
                                  {
                        """
                    );

                    // actionを実行
                    stringBuilder.AppendLine(
                        $"              action.Invoke({string.Join(", ",
                            new[] { "entities[i]" }
                                .Concat(syntaxParameter.QueryTargetTypeSymbol.ExpandTypeOrTuple()
                                    .Select(x => x.IsStructComponentData() ?
                                        $"new RefRW<{x.ToDisplayString()}>({x.Name.ToLower()}, i)"
                                        : $"entityManager.GetComponentObject<{x.ToDisplayString()}>(entities[i])"
                                    )
                                )
                        )});"
                    );

                    stringBuilder.AppendLine("          }");

                    // NativeArrayに情報を戻す
                    foreach (var namedTypeSymbol in syntaxParameter.QueryTargetTypeSymbol.ExpandTypeOrTuple()
                                 .Where(namedTypeSymbol => namedTypeSymbol.IsStructComponentData()))
                    {
                        // CopyFromComponentDataArray
                        stringBuilder.AppendLine(
                            $"          entityQuery.CopyFromComponentDataArray({namedTypeSymbol.Name.ToLower()});"
                        );
                    }

                    stringBuilder.AppendLine("      }");

                    //-----------------------------------------
                    // GetSingleton

                    stringBuilder.AppendLine(
                        $$"""
                                public static {{syntaxParameter.GetSingletonResultTypeName()}} GetSingleton(this {{syntaxParameter.TypeSymbol.ToDisplayString()}} query, EntityManager entityManager)
                                {
                          """);

                    stringBuilder.AppendLine($"          var entityQuery = {syntaxParameter.ToBuildString()};");
                    stringBuilder.AppendLine("          var entities = entityQuery.ToEntityArray(Allocator.Temp);");

                    stringBuilder.AppendLine(
                        $"          return {(syntaxParameter.QueryTargetTypeSymbol.IsTupleType ? $"({
                            string.Join(", ",
                                syntaxParameter.QueryTargetTypeSymbol.ExpandTypeOrTuple()
                                    .Select(x => x.IsStructComponentData() ?
                                        $"entityQuery.GetSingletonRW<{x.ToDisplayString()}>()"
                                        : $"entityQuery.GetSingleton<{x.ToDisplayString()}>()"
                                    )
                            )
                        })" : syntaxParameter.QueryTargetTypeSymbol.IsStructComponentData() ?
                                $"entityQuery.GetSingletonRW<{syntaxParameter.QueryTargetTypeSymbol.ToDisplayString()}>()"
                                : $"entityQuery.GetSingleton<{syntaxParameter.QueryTargetTypeSymbol.ToDisplayString()}>()")};"
                    );


                    stringBuilder.AppendLine(
                        "      }"
                    );
                    //-----------------------------------------
                    // GetSingletonWithEntity

                    stringBuilder.AppendLine(
                        $$"""
                                public static {{syntaxParameter.GetSingletonWithEntityResultTypeName()}} GetSingletonWithEntity(this {{syntaxParameter.TypeSymbol.ToDisplayString()}} query, EntityManager entityManager)
                                {
                          """);

                    stringBuilder.AppendLine($"          var entityQuery = {syntaxParameter.ToBuildString()};");
                    stringBuilder.AppendLine("          var entities = entityQuery.ToEntityArray(Allocator.Temp);");

                    stringBuilder.AppendLine(
                        $"          return ({string.Join(", ",
                            new[] { "entityQuery.GetSingletonEntity()" }.Concat(syntaxParameter.QueryTargetTypeSymbol.ExpandTypeOrTuple()
                                .Select(x => x.IsStructComponentData() ?
                                    $"entityQuery.GetSingletonRW<{x.ToDisplayString()}>()"
                                    : $"entityQuery.GetSingleton<{x.ToDisplayString()}>()"
                                ))
                        )});"
                    );


                    stringBuilder.AppendLine(
                        "      }"
                    );
                }

                stringBuilder.AppendLine("}");
                ctx.AddSource("FlatQueryExtension.g.cs", stringBuilder.ToString());
            }
        );
    }
}