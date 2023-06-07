namespace DataTables.GeneratorCore;

public static class JsonUtility
{
    public static string Serialize(object? obj)
    {
#if NET7_0_OR_GREATER
        return System.Text.Json.JsonSerializer.Serialize(obj);
#else
        return Newtonsoft.Json.JsonConvert.SerializeObject(obj);
#endif
    }

    public static T? Deserialize<T>(string plain)
    {
#if NET7_0_OR_GREATER
        return System.Text.Json.JsonSerializer.Deserialize<T>(plain);
#else
        return Newtonsoft.Json.JsonConvert.DeserializeObject<T>(plain);
#endif
    }
}
