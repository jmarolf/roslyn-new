using System;
using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Help;
using System.CommandLine.Parsing;
using System.Diagnostics;

using Microsoft.Extensions.Logging;

using Roslyn.New;

using Spectre.Console;

using Logger = OpenTelemetry.Trace.Tracer;

public static class Builder
{
    public static Option<FileInfo?> FileOption { get; } = new(
        name: "--file",
        description: "An option whose argument is parsed as a FileInfo",
        isDefault: true,
        parseArgument: result =>
        {
            if (result.Tokens.Count == 0)
            {
                return new FileInfo("sampleQuotes.txt");

            }
            var filePath = result.Tokens.Single().Value;
            if (!File.Exists(filePath))
            {
                result.ErrorMessage = "File does not exist";
                return null;
            }
            else
            {
                return new FileInfo(filePath);
            }
        });
    public static Option<int> DelayOption { get; } = new(
        name: "--delay",
        description: "Delay between lines, specified as milliseconds per character in a line.",
        getDefaultValue: () => 0);
    public static Option<ConsoleColor> ForegroundColorOption { get; } = new(
        name: "--fgcolor",
        description: "Foreground color of text displayed on the console.",
        getDefaultValue: () => ConsoleColor.White);

    public static Option<LogLevel> VerbosityOption { get; } = new(
        name: "--verbosity",
        description: "Specifies the amount of information to display in the console.",
        getDefaultValue: () => LogLevel.Error);

    public static Option<bool> LightModeOption { get; } = new(
        name: "--light-mode",
        description: "Background color of text displayed on the console: default is black, light mode is white.");
    public static Option<string[]> SearchTermsOption { get; } = new(
        name: "--search-terms",
        description: "Strings to search for when deleting entries.")
    {
        IsRequired = true,
        AllowMultipleArgumentsPerToken = true
    };
    public static Argument<string> QuoteArgument { get; } = new(
            name: "quote",
            description: "Text of quote.");
    public static Argument<string> BylineArgument { get; } = new(
            name: "byline",
            description: "Byline of quote.");

    public static Parser BuildParser()
    {
        var rootCommand = new RootCommand("Sample app for System.CommandLine");
        rootCommand.AddGlobalOption(FileOption);
        rootCommand.AddGlobalOption(VerbosityOption);

        var quotesCommand = new Command("quotes", "Work with a file that contains quotes.");
        rootCommand.AddCommand(quotesCommand);

        var readCommand = new Command("read", "Read and display the file.")
        {
            DelayOption,
            ForegroundColorOption,
            LightModeOption
        };

        quotesCommand.AddCommand(readCommand);

        var deleteCommand = new Command("delete", "Delete lines from the file.");
        deleteCommand.AddOption(SearchTermsOption);
        quotesCommand.AddCommand(deleteCommand);

        var addCommand = new Command("add", "Add an entry to the file.");
        addCommand.AddArgument(QuoteArgument);
        addCommand.AddArgument(BylineArgument);
        addCommand.AddAlias("insert");
        quotesCommand.AddCommand(addCommand);

        var loggerProvider = new LoggerProvider();

        readCommand.SetHandler(async
            (FileInfo file, int delay, ConsoleColor fgcolor, bool lightMode, Logger logger) => await ReadFileAsync(file, delay, fgcolor, lightMode, logger),
            FileOption, DelayOption, ForegroundColorOption, LightModeOption, loggerProvider);

        deleteCommand.SetHandler(
            (FileInfo file, string[] searchTerms, Logger logger) => DeleteFromFile(file, searchTerms, logger),
            FileOption, SearchTermsOption, loggerProvider);

        addCommand.SetHandler(
            (FileInfo file, string quote, string byline, Logger logger) => AddToFile(file, quote, byline, logger),
            FileOption, QuoteArgument, BylineArgument, loggerProvider);

        return new CommandLineBuilder(rootCommand)
                .UseDefaults()
                .UseHelp(ctx =>
                {
                    ctx.HelpBuilder.CustomizeSymbol(ForegroundColorOption,
                        firstColumnText: "--color <Black, White, Red, or Yellow>",
                        secondColumnText: "Specifies the foreground color. " +
                            "Choose a color that provides enough contrast " +
                            "with the background color. " +
                            "For example, a yellow foreground can't be read " +
                            "against a light mode background.");
                    ctx.HelpBuilder.CustomizeLayout(
                        _ =>
                            HelpBuilder.Default
                                .GetLayout()
                                .Skip(1) // Skip the default command description section.
                                .Prepend(
                                    _ => AnsiConsole.Write(
                                        new FigletText(rootCommand.Description!))
                        ));
                })
                .Build();
    }

    static async Task ReadFileAsync(FileInfo file, int delay, ConsoleColor fgColor, bool lightMode, Logger logger)
    {
        using var span = logger.StartActiveSpan("Read File Command");
        span.SetAttribute(nameof(file), file.FullName);
        span.SetAttribute(nameof(delay), delay);
        span.SetAttribute(nameof(fgColor), Enum.GetName(fgColor));
        span.SetAttribute(nameof(lightMode), lightMode);

        try
        {
            Console.BackgroundColor = lightMode ? ConsoleColor.White : ConsoleColor.Black;
            Console.ForegroundColor = fgColor;
            List<string> lines = File.ReadLines(file.FullName).ToList();
            foreach (string line in lines)
            {
                AnsiConsole.WriteLine(line);
                await Task.Delay(delay * line.Length);
            };
        }
        catch (Exception ex)
        {
            span.SetStatus(OpenTelemetry.Trace.Status.Error);
            span.RecordException(ex);

            AnsiConsole.WriteLine("Error Reading File");
            AnsiConsole.WriteException(ex);
        }
    }

    static void DeleteFromFile(FileInfo file, string[] searchTerms, Logger logger)
    {
        using var span = logger.StartActiveSpan("Delete File Command");
        span.SetAttribute(nameof(file), file.FullName);
        span.SetAttribute(nameof(searchTerms), string.Join(",", searchTerms));

        try
        {
            File.WriteAllLines(file.FullName, File.ReadLines(file.FullName)
                .Where(line => searchTerms.All(s => !line.Contains(s))).ToList());
        }
        catch (Exception ex)
        {
            span.SetStatus(OpenTelemetry.Trace.Status.Error);
            span.RecordException(ex);

            AnsiConsole.WriteLine("Error Deleting File");
            AnsiConsole.WriteException(ex);
        }

    }

    static void AddToFile(FileInfo file, string quote, string byline, Logger logger)
    {
        using var span = logger.StartActiveSpan("Append to File Command");
        span.SetAttribute(nameof(file), file.FullName);
        span.SetAttribute(nameof(quote), quote);
        span.SetAttribute(nameof(byline), byline);

        try
        {
            using var writer = file.AppendText();
            writer.WriteLine($"{Environment.NewLine}{Environment.NewLine}{quote}");
            writer.WriteLine($"{Environment.NewLine}-{byline}");
            writer.Flush();
        }
        catch (Exception ex)
        {
            span.SetStatus(OpenTelemetry.Trace.Status.Error);
            span.RecordException(ex);

            AnsiConsole.WriteLine("Error Appendding File");
            AnsiConsole.WriteException(ex);
        }

    }
}
