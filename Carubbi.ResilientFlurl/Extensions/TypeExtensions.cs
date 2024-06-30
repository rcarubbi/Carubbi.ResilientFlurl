namespace Carubbi.ResilientFlurl.Extensions
{
    internal static class TypeExtensions
    {
        internal static string GetGenericTypeString(this Type type)
        {
            if (type.IsGenericType)
            {
                var typeName = type.Name;
                var genericArguments = type.GetGenericArguments();
                var genericArgumentsString = string.Join(",", genericArguments.Select(GetGenericTypeString));

                typeName = typeName.Substring(0, typeName.IndexOf('`'));
                return $"{typeName}<{genericArgumentsString}>";
            }
            else
            {
                return type.Name;
            }
        }
    }
}
