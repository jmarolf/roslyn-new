using System.CommandLine;
using System.CommandLine.Parsing;

var parser = Builder.BuildParser();
await parser.InvokeAsync(args);
