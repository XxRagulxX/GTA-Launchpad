#if NET472
// C# 9 record types use init-only setters which require this type.
// It ships with .NET 5+ but must be declared manually for .NET Framework.
namespace System.Runtime.CompilerServices
{
    internal static class IsExternalInit { }
}
#endif
