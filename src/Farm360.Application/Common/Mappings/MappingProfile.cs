using AutoMapper;
using System.Reflection;

namespace Farm360.Application.Common.Mappings;

/// <summary>
/// AutoMapper profile that automatically discovers and registers all IMapFrom<T> mappings.
/// Constitution §7 (DTO Standards): All queries return DTOs, never domain entities.
/// </summary>
public sealed class MappingProfile : Profile
{
    public MappingProfile()
    {
        ApplyMappingsFromAssembly(Assembly.GetExecutingAssembly());
    }

    private void ApplyMappingsFromAssembly(Assembly assembly)
    {
        var mapFromType = typeof(IMapFrom<>);
        
        var mappingMethodName = nameof(IMapFrom<object>.Mapping);

        bool HasInterface(Type t) => t.IsGenericType && t.GetGenericTypeDefinition() == mapFromType;
        
        var types = assembly.GetExportedTypes().Where(t => t.GetInterfaces().Any(HasInterface)).ToList();
        
        var argumentTypes = new Type[] { typeof(Profile) };

        foreach (var type in types)
        {
            var instance = Activator.CreateInstance(type);
            
            var methodInfo = type.GetMethod(mappingMethodName) 
                             ?? type.GetInterface("IMapFrom`1")?.GetMethod(mappingMethodName);
            
            methodInfo?.Invoke(instance, new object[] { this });
        }
    }
}

/// <summary>
/// Interface for DTOs to implement self-mapping to/from Domain Entities.
/// </summary>
public interface IMapFrom<T>
{
    void Mapping(Profile profile) => profile.CreateMap(typeof(T), GetType());
}
