using System.IO;
using System.Text.Json;

namespace StartDraw {
  public class JsonExporter : IExporter {
    private readonly string _DestinationFile;

    public JsonExporter(string destinationFile) {
      _DestinationFile = destinationFile;
    }

    public void Export<T>(T instance) {
      File.WriteAllText(_DestinationFile, JsonSerializer.Serialize(instance, new JsonSerializerOptions {
        WriteIndented = true
      }));
    }
  }
}