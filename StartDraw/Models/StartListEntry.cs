using System;

namespace StartDraw {
  public class StartListEntry : Entry {
    public int? CompetitionRank { get; set; }
    public int? Heat { get; set; }
    public int? Time { get; set; }
    public Guid Guid { get; set; }
  }
}
