using System.Collections;
using System.Globalization;
using System.Text.Json;

namespace Sentier.Execution;

/// <summary>
/// Variables d'une instance, persistées en JSON (colonne VARIABLES). Après relecture, les valeurs sont des
/// <see cref="JsonElement"/> : utiliser <see cref="Get{T}"/> ou <see cref="GetString"/> pour les lire typées.
/// </summary>
public sealed class ProcessVariables : IEnumerable<KeyValuePair<string, object?>>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly Dictionary<string, object?> _values = new(StringComparer.Ordinal);

    public ProcessVariables() { }

    public ProcessVariables(IEnumerable<KeyValuePair<string, object?>>? values) => Merge(values);

    public int Count => _values.Count;

    public IEnumerable<string> Names => _values.Keys;

    public object? this[string name]
    {
        get => _values.TryGetValue(name, out var value) ? value : null;
        set => _values[name] = value;
    }

    public bool Contains(string name) => _values.ContainsKey(name);

    public ProcessVariables Set(string name, object? value)
    {
        _values[name] = value;
        return this;
    }

    public bool Remove(string name) => _values.Remove(name);

    public T? Get<T>(string name)
    {
        if (!_values.TryGetValue(name, out var value) || value is null)
        {
            return default;
        }

        if (value is T typed)
        {
            return typed;
        }

        if (value is JsonElement element)
        {
            return element.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined
                ? default
                : element.Deserialize<T>(JsonOptions);
        }

        var target = Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T);
        return (T)Convert.ChangeType(value, target, CultureInfo.InvariantCulture);
    }

    /// <summary>Représentation texte d'une variable (clés de corrélation, décisions par variable).</summary>
    public string? GetString(string name) => this[name] switch
    {
        null => null,
        string s => s,
        JsonElement { ValueKind: JsonValueKind.String } e => e.GetString(),
        JsonElement { ValueKind: JsonValueKind.Null or JsonValueKind.Undefined } => null,
        JsonElement e => e.GetRawText(),
        bool b => b ? "true" : "false",
        IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
        var other => other.ToString(),
    };

    public void Merge(IEnumerable<KeyValuePair<string, object?>>? values)
    {
        if (values is null)
        {
            return;
        }

        foreach (var (key, value) in values)
        {
            _values[key] = value;
        }
    }

    public IReadOnlyDictionary<string, object?> ToReadOnly() => new Dictionary<string, object?>(_values, StringComparer.Ordinal);

    public string ToJson() => JsonSerializer.Serialize(_values, JsonOptions);

    public static ProcessVariables FromJson(string? json)
    {
        var variables = new ProcessVariables();
        if (string.IsNullOrWhiteSpace(json))
        {
            return variables;
        }

        var values = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json, JsonOptions);
        if (values is not null)
        {
            foreach (var (key, value) in values)
            {
                variables._values[key] = value;
            }
        }

        return variables;
    }

    public IEnumerator<KeyValuePair<string, object?>> GetEnumerator() => _values.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
