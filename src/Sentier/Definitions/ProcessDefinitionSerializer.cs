using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sentier.Definitions;

/// <summary>Sérialisation JSON des définitions (colonne CONTENT) et empreinte servant à détecter les changements.</summary>
public static class ProcessDefinitionSerializer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() },
    };

    public static string Serialize(ProcessDefinition definition) => JsonSerializer.Serialize(definition, Options);

    public static ProcessDefinition Deserialize(string json)
        => JsonSerializer.Deserialize<ProcessDefinition>(json, Options)
           ?? throw new SentierException("Contenu de définition de processus vide.");

    public static string ComputeHash(string json) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json)));
}
