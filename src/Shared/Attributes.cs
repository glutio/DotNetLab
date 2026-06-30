namespace DotNetLab;

[AttributeUsage(AttributeTargets.All, Inherited = false, AllowMultiple = true)]
public sealed class GenericArgumentsAttribute(params Type[] genericArguments) : Attribute
{
    public Type[] GenericArguments { get; } = genericArguments;
}

[AttributeUsage(AttributeTargets.All, Inherited = false, AllowMultiple = true)]
public sealed class MethodInstantiationAttribute : Attribute
{
    public MethodInstantiationAttribute(string typeName, string methodName, Type[]? genericTypeArguments, Type[]? genericMethodArguments)
    {
        TypeName = typeName;
        MethodName = methodName;
        GenericTypeArguments = genericTypeArguments ?? Type.EmptyTypes;
        GenericMethodArguments = genericMethodArguments ?? Type.EmptyTypes;
    }

    public MethodInstantiationAttribute(string typeName, string methodName, string[]? genericTypeArguments, string[]? genericMethodArguments)
    {
        TypeName = typeName;
        MethodName = methodName;
        GenericTypeArguments = (genericTypeArguments ?? [])
            .Select(Type.GetType).Where(t => t != null).ToArray()!;
        GenericMethodArguments = (genericMethodArguments ?? [])
            .Select(Type.GetType).Where(t => t != null).ToArray()!;
    }

    public MethodInstantiationAttribute(string methodName, Type[]? genericMethodArguments)
    {
        MethodName = methodName;
        GenericMethodArguments = genericMethodArguments ?? Type.EmptyTypes;
    }

    public MethodInstantiationAttribute(string methodName, string[]? genericMethodArguments)
    {
        MethodName = methodName;
        GenericMethodArguments = (genericMethodArguments ?? [])
            .Select(Type.GetType).Where(t => t != null).ToArray()!;
    }

    public string? TypeName { get; }
    public string MethodName { get; }
    public Type[] GenericTypeArguments { get; } = Type.EmptyTypes;
    public Type[] GenericMethodArguments { get; } = Type.EmptyTypes;
}
