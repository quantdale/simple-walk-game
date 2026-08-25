// Polyfill: netstandard2.1 lacks IsExternalInit, required by C# 9 init-only setters/records.
namespace System.Runtime.CompilerServices
{
    internal static class IsExternalInit
    {
    }
}
