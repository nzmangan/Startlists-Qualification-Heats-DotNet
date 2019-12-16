using System;
using System.Collections.Generic;
using System.Linq;
using Google.OrTools.LinearSolver;

namespace StartDraw {
  public class StartListSolver {
    private readonly ILogger _Logger;

    public StartListSolver(ILogger logger) {
      _Logger = logger;
    }

    public List<StartListEntry> Solve(int heats, List<StartListEntry> runners, List<Nation> nations) {
      var solverContext = CreateContext(runners, nations, heats);

      var solveResponse = new SolveResponse { Status = "New" };

      int z = 0;
      while (solveResponse.Status != "OPTIMAL") {
        solveResponse = SolveInternal(solverContext, z);
        z++;
      }

      if (z == 0) {
        _Logger.Info("Starting times: Optimal solution found");
      } else {
        _Logger.Info($"Starting times: Solution found with correction factor = {z}");
      }

      return solveResponse.Runners;
    }

    private SolveResponse SolveInternal(SolverContext solverContext, int z) {
      using var s = Solver.CreateSolver("StartListGenerator", "CBC_MIXED_INTEGER_PROGRAMMING");
      var variablesLookup = CreateVaribles(s, solverContext);

      EachRunnerAssignedTo1HeatTimeCombination(s, solverContext, variablesLookup);
      EachHeatTimeToOneRunner(s, solverContext, variablesLookup);
      BalanceRunnerFromSameFedarationAcrossHeats(s, solverContext, variablesLookup);
      SpreadingSimilarRankedRunnersAcrossHeats(s, solverContext, variablesLookup);
      AvoidConsecutiveTimesFromSameNation(s, solverContext, variablesLookup);
      StartGroupRequests(s, solverContext, variablesLookup, z);
      FixRandomRunnersToHeat(s, solverContext, variablesLookup);

      var sol = s.Solve();

      var time = TimeSpan.FromMilliseconds(s.WallTime());

      _Logger.Debug($"Elapsed time: {time}");

      if (sol != Solver.ResultStatus.OPTIMAL) {
        return new SolveResponse { Status = "FAILED", Runners = null };
      }

      var response = CreateList(solverContext, variablesLookup);

      return new SolveResponse { Status = "OPTIMAL", Runners = response };
    }

    private List<StartListEntry> CreateList(SolverContext solverContext, Dictionary<string, Variable> variables) {
      var response = new List<StartListEntry>();

      var lookup = solverContext.Runners.ToDictionary(k => k.Guid);

      foreach (var h in solverContext.HeatRange) {
        foreach (var r in solverContext.Runners.Select(p => p.Guid)) {
          foreach (var t in solverContext.TimeslotsForHeat[h]) {
            if (variables[$"{GetKey(r, h, t)}"].SolutionValue() == 1) {
              if (lookup.ContainsKey(r)) {
                var current = lookup[r];
                response.Add(new StartListEntry {
                  CompetitionRank = current.CompetitionRank,
                  Federation = current.Federation,
                  FirstName = current.FirstName,
                  Grade = current.Grade,
                  Group = current.Group,
                  Guid = current.Guid,
                  LastName = current.LastName,
                  Rank = current.Rank,
                  Id = current.Id,
                  Heat = h,
                  Time = t
                });
              }
            }
          }
        }
      }

      return response;
    }

    private SolverContext CreateContext(List<StartListEntry> runners, List<Nation> nations, int heats) {
      var heatRange = Enumerable.Range(0, heats).ToList();
      var numberOfRunner = (decimal)runners.Count();

      var runnersPerHeat = Enumerable.Range(1, heats).Select(p => Convert.ToInt32(Math.Floor(numberOfRunner / heats))).ToList();

      for (var i = 0; i < numberOfRunner % heats; i++) {
        runnersPerHeat[i]++;
      }

      _Logger.Debug($"Runners per heat: {String.Join(",", runnersPerHeat)}");

      var timeslotsForHeat = runnersPerHeat.Select(p => Enumerable.Range(0, p)).ToList();

      // startingblocks given by teammanagers ( 0 = no preference, 1 = early, 2 mid section, 3 =late)
      // count runners per starting block
      var startingBlocks = runners.Where(p => p.Group.HasValue && p.Group >= 1).Select(p => p.Group).OrderBy(p => p).GroupBy(p => p).Select(p => p.Count()).ToList();
      int minimumValueIndex = startingBlocks.IndexOf(startingBlocks.Min());
      startingBlocks[minimumValueIndex] += runners.Count(p => !p.Group.HasValue || p.Group < 1);

      _Logger.Debug($"Runners per starting block: {String.Join(",", startingBlocks)}");

      // define random runners to be fixed to heats
      var randomRunners = runners.OrderBy(arg => Guid.NewGuid()).Take(heats).ToList();
      _Logger.Debug("Following runners are fixed to a heat to ensure random startlists.");

      foreach (var heat in heatRange) {
        _Logger.Info($"{randomRunners[heat].FirstName} {randomRunners[heat].LastName} to heat {heat + 1}.");
      }

      /*
      var r1 = runners.FirstOrDefault(p => p.FirstName == "Jesse" && p.LastName == "Laukkarinen");
      var r2 = runners.FirstOrDefault(p => p.FirstName == "Davis" && p.LastName == "Dislers");
      var r3 = runners.FirstOrDefault(p => p.FirstName == "Joni" && p.LastName == "Hirvikallio");

      randomRunners = new List<StartListEntry> { r1, r2, r3 };
      */

      return new SolverContext {
        HeatRange = heatRange,
        Runners = runners,
        TimeslotsForHeat = timeslotsForHeat,
        Heats = heats,
        Nations = nations,
        RunnersPerHeat = runnersPerHeat,
        StartingBlocks = startingBlocks,
        RandomRunners = randomRunners
      };
    }

    /// <summary>
    /// consecutive times not from same nation
    /// </summary>
    /// <param name="solverContext"></param>
    private void AvoidConsecutiveTimesFromSameNation(Solver solver, SolverContext solverContext, Dictionary<string, Variable> variables) {
      foreach (var r1 in solverContext.Runners) {
        foreach (var r2 in solverContext.Runners) {
          if (r1.Federation == r2.Federation && r1.Guid != r2.Guid) {
            foreach (var heat in solverContext.HeatRange) {
              for (var t = 0; t < solverContext.RunnersPerHeat[heat] - 1; t++) {
                var v1 = variables[GetKey(r1.Guid, heat, t)];
                var v2 = variables[GetKey(r2.Guid, heat, t + 1)];
                solver.Add(v1 + v2 <= 1);
              }
            }
          }
        }
      }

      _Logger.Verbose($"{solver.NumConstraints()}");
    }

    /// <summary>
    /// fix random runners to specific heats and time
    /// </summary>
    /// <param name="solverContext"></param>
    private void FixRandomRunnersToHeat(Solver solver, SolverContext solverContext, Dictionary<string, Variable> variablesLookup) {
      foreach (var heat in solverContext.HeatRange) {
        var variables = new List<Variable>();
        foreach (var t in solverContext.TimeslotsForHeat[heat]) {
          variables.Add(variablesLookup[GetKey(solverContext.RandomRunners[heat].Guid, heat, t)]);
        }
        solver.Add(Sum(variables) == 1);
      }

      _Logger.Verbose($"{solver.NumConstraints()}");
    }

    /// <summary>
    /// comply with startgroup requests
    /// </summary>
    /// <param name="solverContext"></param>
    /// <param name="z"></param>
    private void StartGroupRequests(Solver solver, SolverContext solverContext, Dictionary<string, Variable> variablesLookup, int z) {
      foreach (var r in solverContext.Runners) {
        var sb1 = solverContext.StartingBlocks.Take(r.Group.Value - 1);
        var sb2 = solverContext.StartingBlocks.Take(r.Group.Value);
        var ssb1 = sb1.Sum();
        var ssb2 = sb2.Sum();
        var ssb1h = FloorDivision((ssb1 - 1), solverContext.Heats) - z;
        var ssb2h = FloorDivision((ssb2 - 1), solverContext.Heats) + z;

        _Logger.Verbose($"{r.FirstName} {r.LastName} [{String.Join(", ", sb1)}] [{String.Join(", ", sb2)}] {ssb1} {ssb2} {ssb1h} {ssb2h}");

        var variables = new List<LinearExpr>();

        foreach (var heat in solverContext.HeatRange) {
          foreach (var t in solverContext.TimeslotsForHeat[heat]) {
            variables.Add(variablesLookup[GetKey(r.Guid, heat, t)] * t);
          }
        }

        var c1 = LinearExprArrayHelper.Sum(variables.ToArray());

        if (r.Group > 1) {
          solver.Add(c1 >= ssb1h);
        }

        if (r.Group < solverContext.Heats && r.Group != 0) {
          solver.Add(c1 <= ssb2h);
        }
      }
    }

    private int FloorDivision(int a, int b) {
      return (a / b - Convert.ToInt32(((a < 0) ^ (b < 0)) && (a % b != 0)));
    }

    /// <summary>
    /// spreading runners 1,2,3 over different heats
    /// </summary>
    /// <param name="solverContext"></param>
    private void SpreadingSimilarRankedRunnersAcrossHeats(Solver solver, SolverContext solverContext, Dictionary<string, Variable> variablesLookup) {
      foreach (var r1 in solverContext.Runners) {
        if (r1.CompetitionRank % solverContext.Heats == 1) {
          foreach (var r2 in solverContext.Runners) {
            if (r2.CompetitionRank == r1.CompetitionRank + 1) {
              foreach (var r3 in solverContext.Runners) {
                if (r3.CompetitionRank == r1.CompetitionRank + 2) {
                  foreach (var heat in solverContext.HeatRange) {
                    var variables = new List<Variable>();

                    foreach (var t in solverContext.TimeslotsForHeat[heat]) {
                      variables.Add(variablesLookup[GetKey(r1.Guid, heat, t)]);
                      variables.Add(variablesLookup[GetKey(r2.Guid, heat, t)]);
                      variables.Add(variablesLookup[GetKey(r3.Guid, heat, t)]);
                    }

                    solver.Add(Sum(variables) <= 1);
                  }
                }
              }
            }
          }
        }
      }

      _Logger.Verbose($"{solver.NumConstraints()}");
    }

    /// <summary>
    /// balance number of runners from 1 country to a heat
    /// </summary>
    /// <param name="solverContext"></param>
    private void BalanceRunnerFromSameFedarationAcrossHeats(Solver solver, SolverContext solverContext, Dictionary<string, Variable> variablesLookup) {
      foreach (var n in solverContext.Nations) {
        var lower = 1 + (n.Runners - 1) / solverContext.Heats;
        var upper = n.Runners / solverContext.Heats;
        _Logger.Verbose($"{n.Name} {lower} {upper}");

        foreach (var heat in solverContext.HeatRange) {
          var variables1 = new List<Variable>();
          var variables2 = new List<Variable>();

          foreach (var t in solverContext.TimeslotsForHeat[heat]) {
            foreach (var runner in solverContext.Runners.Where(r => r.Federation == n.Name)) {
              variables1.Add(variablesLookup[GetKey(runner.Guid, heat, t)]);
              variables2.Add(variablesLookup[GetKey(runner.Guid, heat, t)]);
            }
          }

          solver.Add(Sum(variables1) <= lower);
          solver.Add(Sum(variables2) >= upper);
        }
      }

      _Logger.Verbose($"{solver.NumConstraints()}");
    }

    private LinearExpr Sum(List<Variable> variables) {
      return LinearExprArrayHelper.Sum(variables.ToArray());
    }

    /// <summary>
    /// each heat / time combination is assigned to exactly 1 runner
    /// </summary>
    /// <param name="solverContext"></param>
    private void EachHeatTimeToOneRunner(Solver solver, SolverContext solverContext, Dictionary<string, Variable> variablesLookup) {

      foreach (var heat in solverContext.HeatRange) {
        foreach (var t in solverContext.TimeslotsForHeat[heat]) {
          var variables = new List<Variable>();
          foreach (var runner in solverContext.Runners) {
            variables.Add(variablesLookup[GetKey(runner.Guid, heat, t)]);
          }
          solver.Add(Sum(variables) == 1);
        }
      }

      _Logger.Verbose($"{solver.NumConstraints()}");
    }

    /// <summary>
    /// Each runner is assigned to exactly 1 heat / time combination
    /// </summary>
    /// <param name="solverContext"></param>
    private void EachRunnerAssignedTo1HeatTimeCombination(Solver solver, SolverContext solverContext, Dictionary<string, Variable> variablesLookup) {
      foreach (var runner in solverContext.Runners) {
        var variables = new List<Variable>();
        foreach (var heat in solverContext.HeatRange) {
          foreach (var t in solverContext.TimeslotsForHeat[heat]) {
            variables.Add(variablesLookup[GetKey(runner.Guid, heat, t)]);
          }
        }
        solver.Add(Sum(variables) == 1);
      }

      _Logger.Verbose($"{solver.NumConstraints()}");
    }

    private Dictionary<string, Variable> CreateVaribles(Solver solver, SolverContext solverContext) {
      var variables = new Dictionary<string, Variable>();

      foreach (var runner in solverContext.Runners) {
        foreach (var heat in solverContext.HeatRange) {
          foreach (var t in solverContext.TimeslotsForHeat[heat]) {
            variables[GetKey(runner.Guid, heat, t)] = solver.MakeBoolVar(GetKey(runner.Guid, heat, t));
          }
        }
      }

      return variables;
    }

    private string GetKey(Guid runnerGuid, int heat, int timeSlot) {
      return $"{runnerGuid}::{heat}::{timeSlot}";
    }
  }
}