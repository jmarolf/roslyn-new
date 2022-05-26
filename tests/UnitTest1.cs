using System.CommandLine.Parsing;

namespace Roslyn.New.UnitTests;

public class ParserUnitTests
{
    [Fact]
    public void TestSuccess()
    {
        var parser = Builder.BuildParser();
        var result = parser.Parse("quotes read");
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void TestFileDoesNotExist()
    {
        var parser = Builder.BuildParser();
        var result = parser.Parse("quotes read --file war-and-peace.txt");
        var error = Assert.Single(result.Errors);
        Assert.Equal("File does not exist", error.Message);
    }
}