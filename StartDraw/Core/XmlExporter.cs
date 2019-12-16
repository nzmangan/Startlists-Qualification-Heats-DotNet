using System.IO;
using System.Xml.Serialization;

namespace StartDraw {
  public class XmlExporter : IExporter {
    private string _DestinationFile;

    public XmlExporter(string destinationFile) {
      _DestinationFile = destinationFile;
    }

    public void Export<T>(T instance) {
      using (var writer = new StreamWriter(_DestinationFile)) {
        var serializer = new XmlSerializer(typeof(T));
        serializer.Serialize(writer, instance);
        writer.Flush();
      }
    }
  }
}
