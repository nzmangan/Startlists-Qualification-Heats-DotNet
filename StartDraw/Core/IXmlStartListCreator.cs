using System.Collections.Generic;
using IOF.XML.V3;

namespace StartDraw {
  public interface IXmlStartListCreator {
    StartList Create(string eventName, List<StartListEntry> runners);
  }
}