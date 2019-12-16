using System;
using System.IO;
using System.Linq;

namespace StartDraw {
  class Program {
    static void Main() {
      var serializer = new Serializer();
      var settings = serializer.Deserialize<Settings>(File.ReadAllText("settings.json"));
      var logger = new ConsoleLogger(settings.LogLevel);
      new MainProgram(settings, logger).Run();
    }
  }

  class MainProgram {
    private readonly Settings _Settings;
    private readonly ILogger _Logger;

    public MainProgram(Settings settings, ILogger logger) {
      _Settings = settings;
      _Logger = logger;
    }

    public void Run() {
      IImporter importer = null;

      if (_Settings.ImporterType == "json") {
        importer = new JsonImporter(_Settings.SourceFile);
      }

      if (importer == null) {
        throw new Exception("No valid importer specified!");
      }

      IExporter exporter = null;

      if (_Settings.ExporterType == "xml") {
        exporter = new XmlExporter(_Settings.DestinationFile);
      }

      if (exporter == null) {
        throw new Exception("No valid exporter specified!");
      }

      var runners = importer.Import().OrderByDescending(p => p.Rank).Select(p => new StartListEntry {
        CompetitionRank = null,
        Federation = p.Federation,
        FirstName = p.FirstName,
        Grade = p.Grade,
        Group = p.Group,
        Heat = null,
        Id = p.Id,
        LastName = p.LastName,
        Rank = p.Rank,
        Time = null,
        Guid = Guid.NewGuid()
      }).ToList();

      var nations = runners.Select(p => p.Federation).GroupBy(p => p).Select(p => new Nation { Name = p.Key, Runners = p.Count() }).OrderBy(p => p.Name).ToList();

      int heats = _Settings.Heats;

      for (int i = 0; i < runners.Count(); i++) {
        runners[i].CompetitionRank = i + 1;
      }

      var solver = new StartListSolver(_Logger);

      var result = solver.Solve(heats, runners, nations);

      result = result.OrderBy(p => p.Heat).ThenBy(r => r.Time).ToList();

      var rv = new ResultValidator();

      if (!rv.Valid(runners, result)) {
        throw new Exception("Validation failed.");
      }

      if (_Settings.ExporterType == "xml") {
        var xml = new XmlStartListCreator().Create("Test", result);
        exporter = new XmlExporter(_Settings.DestinationFile);
        exporter.Export(xml);
      }

      foreach (var item in result) {
        _Logger.Debug($"{item.Heat + 1};{item.Time};{item.FirstName} {item.LastName};{item.Federation};{item.CompetitionRank};{item.Id};{item.Group}");
      }

      _Logger.Info("Done!");
      Console.ReadLine();
    }
  }
}
