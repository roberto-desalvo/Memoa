using System.Collections.Concurrent;
using System.Reflection;
using Microsoft.Azure.Functions.Worker;

namespace Memoa.Functions.Authentication;

/// <summary>Attributi sul metodo o sulla classe di una funzione, letti una volta per funzione e attributo.</summary>
internal static class FunctionAttributes
{
    private static readonly ConcurrentDictionary<(string FunctionId, Type Attribute), bool> Cache = new();

    public static bool Has<TAttribute>(FunctionDefinition definition)
        where TAttribute : Attribute =>
        Cache.GetOrAdd((definition.Id, typeof(TAttribute)), _ =>
        {
            // EntryPoint ha la forma "Namespace.Classe.Metodo"; le funzioni stanno in questo assembly.
            var separator = definition.EntryPoint.LastIndexOf('.');
            var type = typeof(FunctionAttributes).Assembly
                .GetType(definition.EntryPoint[..separator]);
            var method = type?.GetMethod(definition.EntryPoint[(separator + 1)..]);

            return method?.GetCustomAttribute<TAttribute>() is not null
                || type?.GetCustomAttribute<TAttribute>() is not null;
        });
}
