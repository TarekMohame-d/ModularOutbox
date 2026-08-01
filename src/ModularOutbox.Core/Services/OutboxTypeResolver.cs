using System.Collections.Concurrent;
using System.Reflection;
using ModularOutbox.Abstractions.Attributes;

namespace ModularOutbox.Core.Services;

internal interface IOutboxTypeResolver
{
    string GetMessageName(Type type);
    Type ResolveType(string messageName);
}

internal class OutboxTypeResolver : IOutboxTypeResolver
{
    private readonly ConcurrentDictionary<Type, string> _typeToNameCache = new();
    private readonly ConcurrentDictionary<string, Type> _nameToTypeCache = new();

    public string GetMessageName(Type type)
    {
        return _typeToNameCache.GetOrAdd(
            type,
            t =>
            {
                var attr = t.GetCustomAttribute<OutboxMessageNameAttribute>();
                if (attr != null)
                    return attr.Name;

                // Default: Fully qualified name format ("Namespace.ClassName, AssemblyName")
                return $"{t.FullName}, {t.Assembly.GetName().Name}";
            }
        );
    }

    public Type ResolveType(string messageName)
    {
        return _nameToTypeCache.GetOrAdd(
            messageName,
            name =>
            {
                var directType = Type.GetType(name);
                if (directType != null)
                    return directType;

                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    if (assembly.IsDynamic)
                        continue;
                    try
                    {
                        foreach (var type in assembly.GetExportedTypes())
                        {
                            var attr = type.GetCustomAttribute<OutboxMessageNameAttribute>();
                            if (attr != null && attr.Name == name)
                                return type;
                        }
                    }
                    catch
                    { /* Skip unreadable assembly */
                    }
                }

                var cleanTypeName = name.Split(',')[0].Trim();

                // Search by Full Name (Namespace + Class) across loaded assemblies
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    if (assembly.IsDynamic)
                        continue;
                    try
                    {
                        var match = assembly
                            .GetExportedTypes()
                            .FirstOrDefault(t => t.FullName == cleanTypeName);
                        if (match != null)
                            return match;
                    }
                    catch
                    { /* Skip unreadable assembly */
                    }
                }

                var shortClassName = cleanTypeName.Split('.').Last();
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    if (assembly.IsDynamic)
                        continue;
                    try
                    {
                        var match = assembly.GetExportedTypes().FirstOrDefault(t => t.Name == shortClassName);
                        if (match != null)
                            return match;
                    }
                    catch
                    { /* Skip unreadable assembly */
                    }
                }

                throw new InvalidOperationException(
                    $"[MyOutboxPackage] Unable to resolve C# type for stored outbox message '{messageName}'. "
                        + $"If the class was renamed or moved, decorate it with [OutboxMessageName(\"{messageName}\")]."
                );
            }
        );
    }
}
