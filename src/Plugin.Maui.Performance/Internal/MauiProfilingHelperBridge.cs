using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace Plugin.Maui.Performance;

/// <summary>
/// Optional late-bound call into <c>Microsoft.Maui.ProfilingHelper</c> when the
/// official package is referenced or injected by <c>maui profile</c>.
/// </summary>
static class MauiProfilingHelperBridge
{
    const string MarkerTypeName = "Microsoft.Maui.ProfilingHelper.MauiProfilingMarker";
    const string AssemblyName = "Microsoft.Maui.ProfilingHelper";

    static readonly Lazy<Bridge> s_bridge = new(Resolve);

    internal static bool IsOfficialSession => s_bridge.Value.IsSession?.Invoke() == true;

    internal static void Complete() => s_bridge.Value.Complete?.Invoke();

    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Optional official helper looked up by well-known type name.")]
    [UnconditionalSuppressMessage("Trimming", "IL2075", Justification = "Optional official helper looked up by well-known type name.")]
    static Bridge Resolve()
    {
        var type = Type.GetType($"{MarkerTypeName}, {AssemblyName}", throwOnError: false)
            ?? Type.GetType(MarkerTypeName, throwOnError: false);
        if (type is null)
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                type = assembly.GetType(MarkerTypeName, throwOnError: false);
                if (type is not null)
                {
                    break;
                }
            }
        }

        if (type is null)
        {
            return default;
        }

        var complete = type.GetMethod("Complete", BindingFlags.Public | BindingFlags.Static, binder: null, Type.EmptyTypes, modifiers: null);
        var session = type.GetProperty("IsProfilingSession", BindingFlags.Public | BindingFlags.Static);

        return new Bridge(
            complete is null ? null : () => complete.Invoke(null, null),
            session is null ? null : () => session.GetValue(null) is true);
    }

    readonly record struct Bridge(Action? Complete, Func<bool>? IsSession);
}
