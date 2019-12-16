using System.Collections.Generic;

namespace StartDraw {
  public class SolverContext {
    public List<StartListEntry> Runners { get; set; }
    public List<int> HeatRange { get; set; }
    public List<IEnumerable<int>> TimeslotsForHeat { get; set; }
    public int Heats { get; set; }
    public List<Nation> Nations { get; set; }
    public List<int> RunnersPerHeat { get; set; }
    public List<int> StartingBlocks { get; set; }
    public List<StartListEntry> RandomRunners { get; set; }
  }
}