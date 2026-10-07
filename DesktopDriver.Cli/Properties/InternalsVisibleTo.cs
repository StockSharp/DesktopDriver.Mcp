using System.Runtime.CompilerServices;

// The tests reach the command planner directly. Going through the process instead would mean a running
// application for every case about the command line, and none of those cases is about an application.
[assembly: InternalsVisibleTo("StockSharp.DesktopDriver.Cli.Tests")]
