using Microsoft.Extensions.DependencyInjection;
using SekaiMasterDb;
using System;
using System.Reflection;

namespace SelfHostSekai.Extensions;

public static class MasterDbServiceCollectionExtensions
{
    public static IServiceCollection AddSekaiMasterDb(this IServiceCollection services)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "MasterDb");
        var masterDb = new MasterDb(path);
        services.AddSingleton(masterDb);

        var propertyInfos = typeof(MasterDb).GetProperties(BindingFlags.Public | BindingFlags.Instance);
        foreach (var prop in propertyInfos)
        {
            if (!prop.PropertyType.IsGenericType || prop.PropertyType.GetGenericTypeDefinition() != typeof(Lazy<>))
                continue;
            
            var lazyType = prop.PropertyType;
            var lazyValueType = lazyType.GenericTypeArguments[0]; // MasterData<T>
                
            if (lazyValueType.IsGenericType && lazyValueType.GetGenericTypeDefinition() == typeof(MasterData<>))
            {
                services.AddSingleton(lazyValueType, sp => 
                {
                    var db = sp.GetRequiredService<MasterDb>();
                    var lazyInstance = prop.GetValue(db);
                    var valueProp = lazyType.GetProperty("Value");
                    return valueProp!.GetValue(lazyInstance)!;
                });
            }
        }

        return services;
    }
}
