using System.Text.Json;

namespace StartDraw {
  public class Serializer {
    public string Serialize<T>(T value) {
      var options = new JsonSerializerOptions {
        WriteIndented = true
      };

      return JsonSerializer.Serialize(value, options);
    }

    public T Deserialize<T>(string json) {
      var options = new JsonSerializerOptions {
        AllowTrailingCommas = true,
        PropertyNameCaseInsensitive = true
      };

      return JsonSerializer.Deserialize<T>(json, options);
    }
  }
}
