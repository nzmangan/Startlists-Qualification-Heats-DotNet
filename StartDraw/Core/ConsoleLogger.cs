using System;

namespace StartDraw {
  public class ConsoleLogger : ILogger {
    private readonly string _LogLevel;

    public ConsoleLogger(string logLevel) {
      _LogLevel = logLevel;
    }

    public void Verbose(string message) {
      if (_LogLevel == "verbose") {
        Print(message);
      }
    }
    public void Debug(string message) {
      if (_LogLevel == "verbose" || _LogLevel == "debug") {
        Print(message);
      }
    }

    public void Info(string message) {
      if (_LogLevel == "verbose" || _LogLevel == "debug" || _LogLevel == "info" || String.IsNullOrWhiteSpace(_LogLevel)) {
        Print(message);
      }
    }

    private void Print(string message) {
      Console.WriteLine(message);
    }
  }
}
