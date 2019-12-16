using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace StartDraw {
  public class JsonImporter : IImporter {
    private readonly string _SourceFile;

    public JsonImporter(string sourceFile) {
      _SourceFile = sourceFile;
    }

    public List<Entry> Import() {
      return new Serializer().Deserialize<List<Entry>>(File.ReadAllText(_SourceFile)).ToList();
    }
  }
}
