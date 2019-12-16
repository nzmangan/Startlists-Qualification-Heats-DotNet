using System.Collections.Generic;

namespace StartDraw {
  public interface IImporter {
    List<Entry> Import();
  }
}
