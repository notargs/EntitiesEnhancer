using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace EntitiesEnhancerSourceGenerator.Tests;

public class FlatQuerySourceGeneratorTest
{
    private const string VectorClassText = 
        """
        using EntityEnhancer;
        using Unity.Entities;
        
        namespace TestNamespace;
        
        public class Examples
        {
            private FlatQuery<double, string> _test = new();
        
            public void ExampleMain()
            {
                var query = new FlatQuery<int, int>(new EntityManager());
                var test = new FlatQuery<int, string>(new EntityManager());
                foreach(var n in new FlatQuery<string, string>(new EntityManager()))
                {
                }
            }
        }
        """;
    [Fact]
    public void GenerateReportMethod()
    {
        // Create an instance of the source generator.
        var generator = new FlatQuerySourceGenerator();

        // Source generators should be tested using 'GeneratorDriver'.
        var driver = CSharpGeneratorDriver.Create(generator);

        // We need to create a compilation with the required source code.
        var compilation = CSharpCompilation.Create(nameof(FlatQuerySourceGeneratorTest),
            new[] { CSharpSyntaxTree.ParseText(VectorClassText) },
            new[]
            {
                // To support 'System.Attribute' inheritance, add reference to 'System.Private.CoreLib'.
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location)
            });

        // Run generators and retrieve all results.
        var runResult = driver.RunGenerators(compilation).GetRunResult();
    }
}