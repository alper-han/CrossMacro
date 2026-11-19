namespace CrossMacro.Infrastructure.Tests.Services.Scripting;

/// <summary>Pins the diagnostic of every validation branch of the screen-reading command grammar.</summary>
public sealed class RunScriptScreenReadingStepParserTests
{
    private const string NamePattern = "Allowed pattern: [A-Za-z_][A-Za-z0-9_]*";

    [Theory]
    [InlineData("pixelcolor 1", "Invalid pixelcolor syntax. Expected: pixelcolor <x> <y> [var] or pixelcolor rel <dx> <dy> [var].")]
    [InlineData("pixelcolor rel 1", "Invalid pixelcolor syntax. Expected: pixelcolor rel <dx> <dy> [var].")]
    [InlineData("pixelcolor a 2", "Invalid pixelcolor coordinate. Expected integer x and y.")]
    [InlineData("pixelcolor rel a 2 v", "Invalid pixelcolor coordinate. Expected integer dx and dy.")]
    [InlineData("pixelcolor 1 2 1v", "Invalid variable name '1v'. " + NamePattern)]
    [InlineData("pixelcolor 1 2 v extra", "Invalid pixelcolor syntax. Expected: pixelcolor <x> <y> [var] or pixelcolor rel <dx> <dy> [var].")]
    [InlineData("waitcolor 1 2", "Invalid waitcolor syntax. Expected: waitcolor <x> <y> <color> [timeout_ms] [result_var].")]
    [InlineData("waitcolor a 2 FF0000", "Invalid waitcolor coordinate. Expected integer x and y.")]
    [InlineData("waitcolor 1 2 GG0000", "Invalid color. Expected 6 hexadecimal RGB characters (RRGGBB) or $variable.")]
    [InlineData("waitcolor 1 2 $1x", "Invalid variable name '$1x'. " + NamePattern)]
    [InlineData("waitcolor 1 2 FF0000 -5", "Invalid waitcolor timeout. Expected integer >= 0.")]
    [InlineData("waitcolor 1 2 FF0000 5 1v", "Invalid variable name '1v'. " + NamePattern)]
    [InlineData("waitcolor 1 2 FF0000 5 v extra", "Invalid waitcolor syntax. Expected: waitcolor <x> <y> <color> [timeout_ms] [result_var].")]
    [InlineData("pixelsearch 0 0 1 1", "Invalid pixelsearch syntax. Expected: pixelsearch <x1> <y1> <x2> <y2> <color> [found_var var_x var_y|var_x var_y] [timeout <milliseconds>=0+] [tolerance <0..255>].")]
    [InlineData("pixelsearch 0 0 a 1 FF0000", "Invalid pixelsearch bounds. Expected integer x1 y1 x2 y2.")]
    [InlineData("pixelsearch -2147483648 0 2147483647 1 FF0000", "Invalid pixelsearch bounds. Endpoint exceeds the supported screen coordinate range.")]
    [InlineData("pixelsearch 0 0 1 1 nothex", "Invalid color. Expected 6 hexadecimal RGB characters (RRGGBB) or $variable.")]
    [InlineData("pixelsearch 0 0 1 1 FF0000 a", "Invalid pixelsearch syntax. Expected either no variables, x_var y_var, or found_var x_var y_var before options.")]
    [InlineData("pixelsearch 0 0 1 1 FF0000 a b c d", "Invalid pixelsearch syntax. Expected either no variables, x_var y_var, or found_var x_var y_var before options.")]
    [InlineData("pixelsearch 0 0 1 1 FF0000 a 1b", "Invalid variable name '1b'. " + NamePattern)]
    [InlineData("pixelsearch 0 0 1 1 FF0000 timeout 5 timeout 6", "Invalid pixelsearch timeout. Expected timeout <milliseconds>=0+.")]
    [InlineData("pixelsearch 0 0 1 1 FF0000 timeout", "Invalid pixelsearch timeout. Expected timeout <milliseconds>=0+.")]
    [InlineData("pixelsearch 0 0 1 1 FF0000 tolerance 256", "Invalid pixelsearch tolerance. Expected tolerance <0..255>.")]
    [InlineData("pixelsearch 0 0 1 1 FF0000 tolerance 1 tolerance 2", "Invalid pixelsearch tolerance. Expected tolerance <0..255>.")]
    [InlineData("pixelsearch 0 0 1 1 FF0000 timeout 5 bogus 1", "Unknown pixelsearch option 'bogus'. Expected timeout <milliseconds>=0+ or tolerance <0..255>.")]
    [InlineData("imagesearch", "Invalid imagesearch syntax. Expected: imagesearch [<x1> <y1> <x2> <y2>|region <left> <top> <width> <height>] <ImageName> [options].")]
    [InlineData("imagesearch $img", "Invalid image name '$img'. " + NamePattern)]
    [InlineData("imagesearch 0 0 a 5 img", "Invalid imagesearch bounds. Expected integer x1 y1 x2 y2.")]
    [InlineData("imagesearch 0 0 0 5 img", "Invalid imagesearch bounds. Expected end-exclusive x2 and y2 to produce a positive region.")]
    [InlineData("imagesearch -2147483648 0 2147483647 5 img", "Invalid imagesearch bounds. Endpoint exceeds the supported screen coordinate range.")]
    [InlineData("imagesearch region 0 0 0 5 img", "Invalid imagesearch region. Expected integer or $variable left/top and positive integer or $variable width/height.")]
    [InlineData("imagesearch region 2147483647 0 1 5 img", "Invalid imagesearch region. Endpoint exceeds the supported screen coordinate range.")]
    [InlineData("imagesearch img a b", "Invalid imagesearch syntax. Expected either no variables or found_var x_var y_var before options.")]
    [InlineData("imagesearch img a b c d", "Invalid imagesearch syntax. Expected either no variables or found_var x_var y_var before options.")]
    [InlineData("imagesearch img f x 1y", "Invalid variable name '1y'. " + NamePattern)]
    [InlineData("imagesearch img similarity 0.5 similarity 0.6", "Duplicate imagesearch similarity option.")]
    [InlineData("imagesearch img similarity", "Invalid imagesearch similarity. Expected similarity <0..1>.")]
    [InlineData("imagesearch img similarity 2", "Invalid imagesearch similarity. Expected number between 0.0 and 1.0.")]
    [InlineData("imagesearch img matchmode best matchmode first", "Duplicate imagesearch matchmode option.")]
    [InlineData("imagesearch img matchmode fastest", "Invalid imagesearch matchmode. Expected matchmode <auto|first|best>.")]
    [InlineData("imagesearch img timeout 5", "Unknown imagesearch option 'timeout'. Expected similarity <0..1> or matchmode <auto|first|best>.")]
    [InlineData("imageclick img button up", "Invalid imageclick button. Expected button <left|right|middle>.")]
    [InlineData("imageclick img button left button right", "Invalid imageclick button. Expected button <left|right|middle>.")]
    [InlineData("imageclick img a b", "Invalid imageclick syntax. Expected either no variables or found_var x_var y_var before options.")]
    [InlineData("imageclick img similarity 0.5 similarity 0.6", "Duplicate image similarity option.")]
    [InlineData("imageclick img similarity", "Invalid image similarity. Expected similarity <0..1>.")]
    [InlineData("imageclick img matchmode best matchmode best", "Duplicate image matchmode option.")]
    [InlineData("imageclick img matchmode x", "Invalid image matchmode. Expected matchmode <auto|first|best>.")]
    [InlineData("imageclick img timeout 1 timeout 2", "Duplicate image timeout option.")]
    [InlineData("imageclick img timeout -1", "Invalid image timeout. Expected timeout <milliseconds>=0+.")]
    [InlineData("imageclick img timeout 5 bogus 1", "Unknown image option 'bogus'. Expected timeout <milliseconds>=0+, similarity <0..1>, or matchmode <auto|first|best>.")]
    [InlineData("waitimage img timeout", "Invalid waitimage timeout. Expected timeout <milliseconds>=0+.")]
    [InlineData("waitimage img timeout 1 timeout 2", "Invalid waitimage timeout. Expected timeout <milliseconds>=0+.")]
    [InlineData("waitimage img timeout -3", "Invalid waitimage timeout. Expected integer >= 0.")]
    [InlineData("waitimage img a b", "Invalid waitimage syntax. Expected either no variables or found_var x_var y_var before options.")]
    [InlineData("waitimage img similarity 5", "Invalid image similarity. Expected similarity <0..1>.")]
    [InlineData("waitimage region $a $b 0 5 img", "Invalid waitimage region. Expected integer or $variable left/top and positive integer or $variable width/height.")]
    public void TryValidateStep_ReportsTheDiagnosticOfTheViolatedRule(string step, string expected)
    {
        _ = RunScriptScreenReadingStepParser.TryValidateStep(step, out var error).Should().BeTrue();
        _ = error.Should().Be(expected);
    }

    [Theory]
    [InlineData("pixelcolor 1 2")]
    [InlineData("pixelcolor rel -1 2 v")]
    [InlineData("waitcolor 1 2 $c 100 ok")]
    [InlineData("pixelsearch 0 0 1 1 ff00ff x y timeout 0 tolerance 255")]
    [InlineData("pixelsearch 0 0 1 1 $c found x y tolerance 5 timeout 9")]
    [InlineData("imagesearch img")]
    [InlineData("imagesearch 0 0 10 10 img found x y similarity 1 matchmode BEST")]
    [InlineData("imagesearch region $l $t 5 $h img")]
    [InlineData("imageclick region 0 0 5 5 img found x y button middle timeout 0 similarity 0")]
    [InlineData("waitimage img found x y timeout 100 matchmode first")]
    public void TryValidateStep_AcceptsWellFormedCommands(string step)
    {
        var recognized = RunScriptScreenReadingStepParser.TryParseStep(step, out var parsed, out var error);

        _ = recognized.Should().BeTrue();
        _ = error.Should().BeNull();
        _ = parsed.Should().NotBeNull();
    }

    [Theory]
    [InlineData("click left")]
    [InlineData("pixelcolorful 1 2 v")]
    [InlineData("")]
    public void TryValidateStep_IgnoresOtherCommands(string step)
    {
        _ = RunScriptScreenReadingStepParser.TryValidateStep(step, out var error).Should().BeFalse();
        _ = error.Should().BeNull();
    }
}
