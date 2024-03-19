using System.ComponentModel.DataAnnotations;
using EntitiesEnhancer;
using Unity.Entities;

namespace EntitiesEnhancerSourceGenerator.Sample;

public class Examples
{
    public record struct TestComponent : IComponentData;

    public void ExampleMain()
    {
        var entityManager = new EntityManager();
        var query = new FlatQuery<int, int>();
        var test = new FlatQuery<int, string>();
        var test2 = new FlatQuery<UrlAttribute, string>();
        new FlatQuery<(TestComponent, int), (FlatWithAll<float>, FlatWithAny<(string, long)>, FlatWithNone<double>)>()
            .Query(entityManager, (component, i) =>
            {
                
            });
        new FlatQuery<(TestComponent, int), (FlatWithAll<float>, FlatWithAny<(string, long)>, FlatWithNone<double>)>()
            .Query(entityManager, (entity1, testComponent, arg3) => 
            {
            });
        var (a, b) = new FlatQuery<(TestComponent, int), (FlatWithAll<float>, FlatWithAny<(string, long)>, FlatWithNone<double>)>().GetSingleton(entityManager);
    }
}