using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.JSInterop;
using PolyType;
using PolyType.Abstractions;

namespace DotNetLab;

internal sealed class LocalStorageService(
    ILogger<LocalStorageService> logger,
    IJSRuntime jsRuntime)
{
    private static readonly JsonSerializerOptions jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DictionaryKeyPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly Lazy<Task<IJSObjectReference>> moduleTask = new(() => jsRuntime.InvokeAsync<IJSObjectReference>(
        "import", "../_content/DotNetLab.App/js/LocalStorageService.js").AsTask());

    public async ValueTask<T?> GetItemAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        ValidateKey(key);
        var json = await jsRuntime.InvokeAsync<string?>("localStorage.getItem", cancellationToken, key);
        return string.IsNullOrWhiteSpace(json)
            ? default
            : JsonSerializer.Deserialize<T>(json, jsonOptions);
    }

    public async ValueTask SetItemAsync<T>(string key, T value, CancellationToken cancellationToken = default)
    {
        ValidateKey(key);
        var json = JsonSerializer.Serialize(value, jsonOptions);
        await jsRuntime.InvokeVoidAsync("localStorage.setItem", cancellationToken, key, json);
    }

    private static void ValidateKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentNullException(nameof(key));
        }
    }

    public async Task TryLoadPropertiesAsync<T>(T target) where T : class, IShapeable<T>
    {
        var objectShape = (IObjectTypeShape<T>)T.GetTypeShape();
        var properties = objectShape.Properties;
        var module = await moduleTask.Value;
        var result = await module.InvokeAsync<IReadOnlyDictionary<string, string?>>("loadItems", properties.Select(GetSerializationKey));
        var visitor = new PropertyDeserializer(logger, result);
        var mutator = visitor.VisitObject(objectShape);
        mutator(ref target);
    }

    public static void SerializeProperty<T>(T target, string propertyName, SerializedPropertyList items) where T : IShapeable<T>
    {
        var serializer = PropertySerializer.GetPropertySerializer<T>();
        var serializeProperty = serializer(propertyName);
        serializeProperty(target, items);
    }

    public async Task SavePropertiesAsync(SerializedPropertyList items)
    {
        var module = await moduleTask.Value;
        await module.InvokeVoidAsync("saveItems", items.Drain());
    }

    private static string GetSerializationKey(IPropertyShape propertyShape)
    {
        return propertyShape.AttributeProvider.GetCustomAttribute<DisplayNameAttribute>() is { } attribute
            ? attribute.DisplayName
            : propertyShape.Name;
    }

    private sealed class PropertyDeserializer(
        ILogger<LocalStorageService> logger,
        IReadOnlyDictionary<string, string?> items)
        : TypeShapeVisitor
    {
        public override Mutator<T> VisitObject<T>(IObjectTypeShape<T> objectShape, object? state = null)
        {
            var propertyMutators = objectShape.Properties
                .Where(static prop => prop.HasSetter)
                .Select(prop => (Mutator<T>)prop.Accept(this)!)
                .ToArray();

            return (ref value) => { foreach (var mutator in propertyMutators) mutator(ref value); };
        }

        public override Mutator<TDeclaringType> VisitProperty<TDeclaringType, TPropertyType>(IPropertyShape<TDeclaringType, TPropertyType> propertyShape, object? state = null)
        {
            var json = items[GetSerializationKey(propertyShape)];

            if (string.IsNullOrWhiteSpace(json))
            {
                return static (ref _) => { };
            }

            TPropertyType? value;
            try
            {
                value = JsonSerializer.Deserialize<TPropertyType>(json, jsonOptions);  
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to deserialize property {PropertyName} from JSON {Json}", propertyShape.Name, json);
                return static (ref _) => { };
            }

            if (value is null && propertyShape.IsSetterNonNullable)
            {
                logger.LogWarning("Ignoring null value for non-nullable property {PropertyName} during deserialization", propertyShape.Name);
                return static (ref _) => { };
            }

            var setter = propertyShape.GetSetter();
            return (ref obj) => setter(ref obj, value!);
        }
    }

    private sealed class PropertySerializer : TypeShapeVisitor
    {
        private static readonly PropertySerializer visitor = new();
        private static readonly ConcurrentDictionary<Type, Func<string, Delegate>> cache = new();

        public static Func<string, Action<T, SerializedPropertyList>> GetPropertySerializer<T>() where T : IShapeable<T>
        {
            return (Func<string, Action<T, SerializedPropertyList>>)cache.GetOrAdd(typeof(T), static _ =>
            {
                var typeShape = T.GetTypeShape();
                return visitor.VisitObject((IObjectTypeShape<T>)typeShape);
            });
        }

        private PropertySerializer() { }

        public override Func<string, Action<T, SerializedPropertyList>> VisitObject<T>(IObjectTypeShape<T> objectShape, object? state = null)
        {
            var lookup = objectShape.Properties
                .Where(static prop => prop.HasGetter)
                .ToFrozenDictionary(static prop => prop.Name, prop => (Action<T, SerializedPropertyList>)prop.Accept(this)!);
            return (name) => lookup[name];
        }

        public override Action<TDeclaringType, SerializedPropertyList> VisitProperty<TDeclaringType, TPropertyType>(IPropertyShape<TDeclaringType, TPropertyType> propertyShape, object? state = null)
        {
            var getter = propertyShape.GetGetter();
            return (instance, items) =>
            {
                var value = getter(ref instance);
                var json = JsonSerializer.Serialize(value, jsonOptions);
                items[GetSerializationKey(propertyShape)] = json;
            };
        }
    }
}

internal delegate void Mutator<T>(ref T obj);

/// <summary>
/// Note that keys in this dictionary might not match property names,
/// see <see cref="LocalStorageService.GetSerializationKey"/>.
/// </summary>
internal sealed class SerializedPropertyList
{
    private readonly Lock gate = new();
    private readonly Dictionary<string, string?> items = new();

    public string? this[string key]
    {
        get
        {
            lock (gate)
            {
                return items.TryGetValue(key, out var value)
                    ? value
                    : null;
            }
        }
        set
        {
            lock (gate)
            {
                items[key] = value;
            }
        }
    }

    public bool IsEmpty
    {
        get
        {
            lock (gate)
            {
                return items.Count == 0;
            }
        }
    }

    public Dictionary<string, string?> Drain()
    {
        lock (gate)
        {
            var drained = new Dictionary<string, string?>(items);
            items.Clear();
            return drained;
        }
    }
}
