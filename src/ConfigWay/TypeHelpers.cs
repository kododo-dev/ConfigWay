using System.Reflection;

namespace Kododo.ConfigWay;

internal static class TypeHelpers
{
    internal static bool IsLeaf(Type t) =>
        t == typeof(string) || t == typeof(bool) || IsNumeric(t) || t.IsEnum;

    internal static bool IsNumeric(Type t) =>
        t == typeof(int)     || t == typeof(long)    || t == typeof(short)   ||
        t == typeof(float)   || t == typeof(double)  || t == typeof(decimal) ||
        t == typeof(byte)    || t == typeof(uint)    || t == typeof(ulong);

    internal static bool IsArrayOrCollection(Type t)
    {
        if (t.IsArray) return true;
        if (!t.IsGenericType) return false;
        var gtd = t.GetGenericTypeDefinition();
        return gtd == typeof(List<>)                ||
               gtd == typeof(IList<>)               ||
               gtd == typeof(IEnumerable<>)         ||
               gtd == typeof(IReadOnlyList<>)       ||
               gtd == typeof(ICollection<>)         ||
               gtd == typeof(IReadOnlyCollection<>);
    }

    internal static bool TryGetCollectionElementType(Type t, out Type elementType)
    {
        if (t.IsArray)
        {
            elementType = t.GetElementType()!;
            return true;
        }

        if (t.IsGenericType)
        {
            var gtd = t.GetGenericTypeDefinition();
            if (gtd == typeof(List<>)                ||
                gtd == typeof(IList<>)               ||
                gtd == typeof(IEnumerable<>)         ||
                gtd == typeof(IReadOnlyList<>)       ||
                gtd == typeof(ICollection<>)         ||
                gtd == typeof(IReadOnlyCollection<>))
            {
                elementType = t.GetGenericArguments()[0];
                return true;
            }
        }

        elementType = null!;
        return false;
    }

    internal static IEnumerable<PropertyInfo> GetWritableProperties(Type type) =>
        type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && p.CanWrite);
}
