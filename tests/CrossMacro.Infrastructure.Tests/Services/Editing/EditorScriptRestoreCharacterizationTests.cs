using System.Globalization;

namespace CrossMacro.Infrastructure.Tests.Services.Editing;

/// <summary>
/// Pins how script steps restore into editor actions (type sequence and diagnostics) and what those
/// actions compile back to, across every step family the editor script reader understands.
/// </summary>
public sealed class EditorScriptRestoreCharacterizationTests
{
    public static TheoryData<string, string, string, string> Cases { get; } = new()
    {
        { "move abs 100 200", "MouseMove", "", "events: MouseMove/100/200/None/0" },
        { "move rel-logical 3 -2", "MouseMove", "", "events: MouseMove/3/-2/None/0" },
        { "move rel-raw 3 4", "MouseMove", "", "events: MouseMove/3/4/None/0" },
        { "move abs 10 20\nclick left\ndown right\nup right", "MouseMove MouseClick MouseDown MouseUp", "", "events: MouseMove/10/20/None/0;Click/10/20/Left/0;ButtonPress/10/20/Right/0;ButtonRelease/10/20/Right/0" },
        { "move rel-raw 3 4\nclick left", "MouseMove MouseClick", "", "events: MouseMove/3/4/None/0;Click/0/0/Left/0" },
        { "move rel 5 5\nclick left", "MouseMove MouseClick", "", "events: MouseMove/5/5/None/0;Click/0/0/Left/0" },
        { "move abs 10 20\nmove rel 1 1\nclick left", "MouseMove MouseMove MouseClick", "", "events: MouseMove/10/20/None/0;MouseMove/1/1/None/0;Click/0/0/Left/0" },
        { "down current left\nup left", "MouseDown MouseUp", "", "events: ButtonPress/0/0/Left/0;ButtonRelease/0/0/Left/0" },
        { "click current back", "MouseClick", "", "events: Click/0/0/Side1/0" },
        { "click invalid", "RawScriptStep", "click invalid:Unsupported step restored as raw script text.", "throws: Step 1: Unknown mouse button 'invalid'." },
        { "set x 1\nset y 2\nset n 3\nset c FF0000\nset d 5\nmove abs $x $y\nclick left", "SetVariable SetVariable SetVariable SetVariable SetVariable MouseMove MouseClick", "", "script: set x 1\nset y 2\nset n 3\nset c FF0000\nset d 5\nmove abs $x $y\nclick left" },
        { "tap 30", "KeyPress", "", "events: KeyPress/0/0/None/0;KeyRelease/0/0/None/10000" },
        { "tap 30+31", "RawScriptStep", "", "throws: Step 1: tap expects either exactly one non-modifier key (example: ctrl+c) or a single modifier key." },
        { "tap enter", "RawScriptStep", "tap enter:Unsupported step restored as raw script text.", "script: tap enter" },
        { "key down 30\nkey up 30", "KeyDown KeyUp", "", "events: KeyPress/0/0/None/0;KeyRelease/0/0/None/0" },
        { "key sideways 30", "RawScriptStep", "key sideways 30:Unsupported step restored as raw script text.", "throws: Step 1: Invalid key action. Expected: key down <key> | key up <key>." },
        { "delay 100\nclick left", "Delay MouseClick", "", "events: Click/0/0/Left/100000" },
        { "delay 125us\nclick left", "Delay MouseClick", "", "events: Click/0/0/Left/125" },
        { "delay 2.375ms\nclick left", "Delay MouseClick", "", "events: Click/0/0/Left/2375" },
        { "delay random 1..3\nclick left", "Delay MouseClick", "", "events: Click/0/0/Left/0" },
        { "delay random 5 1", "RawScriptStep", "delay random 5 1:Unsupported step restored as raw script text.", "throws: Step 1: Invalid random delay bounds. Expected 0 <= min <= max." },
        { "set x 1\nset y 2\nset n 3\nset c FF0000\nset d 5\ndelay $d", "SetVariable SetVariable SetVariable SetVariable SetVariable RawScriptStep", "delay $d:Unsupported step restored as raw script text.", "script: set x 1\nset y 2\nset n 3\nset c FF0000\nset d 5\ndelay $d" },
        { "scroll up 3\nscroll left 2\nscroll down 1\nscroll right 4", "ScrollVertical ScrollHorizontal ScrollVertical ScrollHorizontal", "", "events: Click/0/0/ScrollUp/0;Click/0/0/ScrollUp/0;Click/0/0/ScrollUp/0;Click/0/0/ScrollLeft/0;Click/0/0/ScrollLeft/0;Click/0/0/ScrollDown/0;Click/0/0/ScrollRight/0;Click/0/0/ScrollRight/0;Click/0/0/ScrollRight/0;Click/0/0/ScrollRight/0" },
        { "scroll right 0", "RawScriptStep", "scroll right 0:Unsupported step restored as raw script text.", "throws: Step 1: Invalid scroll count. Expected integer > 0." },
        { "type ab", "TextInput", "", "events: KeyPress/0/0/None/0;KeyRelease/0/0/None/0;KeyPress/0/0/None/10000;KeyRelease/0/0/None/0" },
        { "mouse position mx my", "MousePosition", "", "script: mouse position mx my" },
        { "mouse position a a", "RawScriptStep", "mouse position a a:Unsupported step restored as raw script text.", "throws: Step 1: X and Y destination variables must be different." },
        { "set x 5", "SetVariable", "", "script: set x 5" },
        { "set x = 5", "SetVariable", "", "script: set x 5" },
        { "set name = hello world", "SetVariable", "", "script: set name hello world" },
        { "set flag true", "SetVariable", "", "script: set flag true" },
        { "set v $other", "SetVariable", "", "throws: Step 1: Unknown variable '$other'." },
        { "set literal $$foo", "SetVariable", "", "script: set literal $$foo" },
        { "set 1bad 5", "SetVariable", "", "throws: Step 1: Invalid variable name '1bad'. Allowed pattern: [A-Za-z_][A-Za-z0-9_]*" },
        { "set x", "SetVariable", "", "throws: Step 1: Invalid set syntax. Expected: set <name> <value> or set <name>=<value>." },
        { "set x 1\nset y 2\nset n 3\nset c FF0000\nset d 5\ninc x\ninc x 5\ninc x $y\ndec x 2\nmul x 3\ndiv x 4", "SetVariable SetVariable SetVariable SetVariable SetVariable IncrementVariable IncrementVariable IncrementVariable DecrementVariable MultiplyVariable DivideVariable", "", "script: set x 1\nset y 2\nset n 3\nset c FF0000\nset d 5\ninc x 1\ninc x 5\ninc x $y\ndec x 2\nmul x 3\ndiv x 4" },
        { "set x 1\nset y 2\nset n 3\nset c FF0000\nset d 5\ndiv x abc", "SetVariable SetVariable SetVariable SetVariable SetVariable DivideVariable", "", "throws: Step 6: Invalid mul/div amount 'abc'. Expected integer." },
        { "div 1x 2", "DivideVariable", "", "throws: Step 1: Invalid variable name '1x'. Allowed pattern: [A-Za-z_][A-Za-z0-9_]*" },
        { "inc", "RawScriptStep", "inc:Unsupported step restored as raw script text.", "throws: Step 1: unsupported step syntax 'inc'." },
        { "inc x $y + 1", "IncrementVariable", "", "throws: Step 1: Invalid inc syntax. Expected: inc <name> [amount]." },
        { "repeat 3 {\nclick left\n}", "RepeatBlockStart MouseClick BlockEnd", "", "script: repeat 3 {\nclick current left\n}" },
        { "set x 1\nset y 2\nset n 3\nset c FF0000\nset d 5\nrepeat $n {\nclick left\n}", "SetVariable SetVariable SetVariable SetVariable SetVariable RepeatBlockStart MouseClick BlockEnd", "", "script: set x 1\nset y 2\nset n 3\nset c FF0000\nset d 5\nrepeat $n {\nclick current left\n}" },
        { "set x 1\nset y 2\nset n 3\nset c FF0000\nset d 5\nrepeat $n + 1 {\nclick left\n}", "SetVariable SetVariable SetVariable SetVariable SetVariable RepeatBlockStart MouseClick BlockEnd", "", "script: set x 1\nset y 2\nset n 3\nset c FF0000\nset d 5\nrepeat $n + 1 {\nclick current left\n}" },
        { "repeat 2 + 3 {\nclick left\n}", "RepeatBlockStart MouseClick BlockEnd", "", "script: repeat 2 + 3 {\nclick current left\n}" },
        { "repeat (bad) {\nclick left\n}", "RepeatBlockStart MouseClick BlockEnd", "", "throws: Step 1: Invalid repeat count '(bad)'. Expected integer." },
        { "repeat {", "RawScriptStep", "repeat {:Unsupported step restored as raw script text.", "throws: Step 1: unsupported block syntax. Expected one of: repeat <count> {, if <left> <op> <right> {, while <left> <op> <right> {, for <var> from <start> to <end> [step <n>] {" },
        { "repeat 3 {", "RepeatBlockStart", "", "throws: Missing closing brace '}' for block." },
        { "set x 1\nset y 2\nset n 3\nset c FF0000\nset d 5\nif $x == 1 {\nbreak\n}\nelse {\ncontinue\n}", "SetVariable SetVariable SetVariable SetVariable SetVariable IfBlockStart Break BlockEnd ElseBlockStart Continue BlockEnd", "", "throws: Step 7: 'break' can only be used inside repeat/while/for blocks." },
        { "set x 1\nset y 2\nset n 3\nset c FF0000\nset d 5\nrepeat 2 {\nif $x == 1 {\nbreak\n}\nelse {\ncontinue\n}\n}", "SetVariable SetVariable SetVariable SetVariable SetVariable RepeatBlockStart IfBlockStart Break BlockEnd ElseBlockStart Continue BlockEnd BlockEnd", "", "script: set x 1\nset y 2\nset n 3\nset c FF0000\nset d 5\nrepeat 2 {\nif $x == 1 {\nbreak\n}\nelse {\ncontinue\n}\n}" },
        { "set x 1\nset y 2\nset n 3\nset c FF0000\nset d 5\nif $x > 5 {\nclick left\n}", "SetVariable SetVariable SetVariable SetVariable SetVariable IfBlockStart MouseClick BlockEnd", "", "script: set x 1\nset y 2\nset n 3\nset c FF0000\nset d 5\nif $x > 5 {\nclick current left\n}" },
        { "set x 1\nset y 2\nset n 3\nset c FF0000\nset d 5\nif $x + 1 > 5 {\nclick left\n}", "SetVariable SetVariable SetVariable SetVariable SetVariable IfBlockStart MouseClick BlockEnd", "", "script: set x 1\nset y 2\nset n 3\nset c FF0000\nset d 5\nif $x + 1 > 5 {\nclick current left\n}" },
        { "set x 1\nset y 2\nset n 3\nset c FF0000\nset d 5\nif $c == FF0000 {\nclick left\n}", "SetVariable SetVariable SetVariable SetVariable SetVariable IfBlockStart MouseClick BlockEnd", "", "script: set x 1\nset y 2\nset n 3\nset c FF0000\nset d 5\nif $c == FF0000 {\nclick current left\n}" },
        { "set x 1\nset y 2\nset n 3\nset c FF0000\nset d 5\nif $x == true {\nclick left\n}", "SetVariable SetVariable SetVariable SetVariable SetVariable IfBlockStart MouseClick BlockEnd", "", "script: set x 1\nset y 2\nset n 3\nset c FF0000\nset d 5\nif $x == true {\nclick current left\n}" },
        { "set x 1\nset y 2\nset n 3\nset c FF0000\nset d 5\nif $x == fast {\nclick left\n}", "SetVariable SetVariable SetVariable SetVariable SetVariable IfBlockStart MouseClick BlockEnd", "", "script: set x 1\nset y 2\nset n 3\nset c FF0000\nset d 5\nif $x == fast {\nclick current left\n}" },
        { "if $$foo == $$bar {\nclick left\n}", "IfBlockStart MouseClick BlockEnd", "", "throws: Run script did not produce any executable events. Add at least one runtime step (move/click/down/up/scroll/key/tap/type)." },
        { "set x 1\nset y 2\nset n 3\nset c FF0000\nset d 5\nif $x ?? 1 {\nclick left\n}", "SetVariable SetVariable SetVariable SetVariable SetVariable IfBlockStart MouseClick BlockEnd", "", "throws: Step 6: Unsupported condition operator. Allowed: ==, !=, >, >=, <, <=." },
        { "if {", "RawScriptStep", "if {:Unsupported step restored as raw script text.", "throws: Step 1: Condition cannot be empty." },
        { "set x 1\nset y 2\nset n 3\nset c FF0000\nset d 5\nwhile $y > 10 {\nclick left\n}", "SetVariable SetVariable SetVariable SetVariable SetVariable WhileBlockStart MouseClick BlockEnd", "", "script: set x 1\nset y 2\nset n 3\nset c FF0000\nset d 5\nwhile $y > 10 {\nclick current left\n}" },
        { "while count > 0 {\nclick left\n}", "WhileBlockStart MouseClick BlockEnd", "", "throws: Step 1: Operator '>' requires numeric operands. Got 'count' and '0'." },
        { "for i from 1 to 3 {\nclick left\n}", "ForBlockStart MouseClick BlockEnd", "", "script: for i from 1 to 3 {\nclick current left\n}" },
        { "for i from 1 to 10 step 2 {\nclick left\n}", "ForBlockStart MouseClick BlockEnd", "", "script: for i from 1 to 10 step 2 {\nclick current left\n}" },
        { "set x 1\nset y 2\nset n 3\nset c FF0000\nset d 5\nfor i from $x + 1 to $y * 2 step $n - 1 {\nclick left\n}", "SetVariable SetVariable SetVariable SetVariable SetVariable ForBlockStart MouseClick BlockEnd", "", "script: set x 1\nset y 2\nset n 3\nset c FF0000\nset d 5\nfor i from $x + 1 to $y * 2 step $n - 1 {\nclick current left\n}" },
        { "for i 1 to 3 {\nclick left\n}", "ForBlockStart MouseClick BlockEnd", "", "throws: Step 1: Invalid for syntax. Expected: for <var> from <start> to <end> [step <n>] {" },
        { "for item in values {\nclick left\n}", "ForBlockStart MouseClick BlockEnd", "", "throws: Step 1: Invalid for syntax. Expected: for <var> from <start> to <end> [step <n>] {" },
        { "for i from abc to 3 {\nclick left\n}", "ForBlockStart MouseClick BlockEnd", "", "throws: Step 1: Invalid for start 'abc'. Expected integer." },
        { "else {\nclick left\n}", "ElseBlockStart MouseClick BlockEnd", "", "throws: Step 1: unexpected 'else' block." },
        { "}", "BlockEnd", "", "throws: Step 1: unexpected closing brace '}'." },
        { "break", "Break", "", "throws: Step 1: 'break' can only be used inside repeat/while/for blocks." },
        { "continue", "Continue", "", "throws: Step 1: 'continue' can only be used inside repeat/while/for blocks." },
        { "clipboard get clip", "ClipboardGet", "", "script: clipboard get clip" },
        { "clipboard get 1bad", "RawScriptStep", "clipboard get 1bad:Unsupported step restored as raw script text.", "throws: Step 1: Invalid variable name '1bad'. Allowed pattern: [A-Za-z_][A-Za-z0-9_]*" },
        { "clipboard set hello", "ClipboardSet", "", "script: clipboard set hello" },
        { "clipboard set literal $$clipText", "ClipboardSet", "", "script: clipboard set literal $$clipText" },
        { "clipboard capture ctrl+c selectedText", "CopySelectionToVariable", "", "script: clipboard capture ctrl+c selectedText" },
        { "clipboard capture ctrl+shift+c $selectedText", "CopySelectionToVariable", "", "script: clipboard capture ctrl+shift+c selectedText" },
        { "clipboard capture ctrl+c 1destination", "RawScriptStep", "clipboard capture ctrl+c 1destination:Unsupported step restored as raw script text.", "throws: Step 1: Syntax: clipboard capture <ctrl+c|ctrl+shift+c> <var>" },
        { "clipboard hint", "RawScriptStep", "clipboard hint:Unsupported step restored as raw script text.", "throws: Step 1: Unknown clipboard subcommand: HINT" },
        { "shell \"echo hi\"", "ShellCommand", "", "script: shell \"echo hi\"" },
        { "shell capture \"echo hi\" ec so se", "ShellCommand", "", "script: shell capture \"echo hi\" ec so se" },
        { "shell input \"in\" \"cat\"", "ShellCommand", "", "script: shell input \"in\" \"cat\"" },
        { "shell capture-input \"in\" \"cat\" ec so se 2 50 1000", "ShellCommand", "", "script: shell capture-input \"in\" \"cat\" ec so se 2 50 1000" },
        { "shell echo legacy", "RawScriptStep", "shell echo legacy:Unsupported step restored as raw script text.", "script: shell echo legacy" },
        { "shell \"echo hi\" 2 50 1000", "ShellCommand", "", "script: shell \"echo hi\" 2 50 1000" },
        { "screenshot output shot.png", "Screenshot", "", "script: screenshot output shot.png" },
        { "screenshot region 1 2 3 4 output shot.png clipboard", "Screenshot", "", "script: screenshot region 1 2 3 4 output shot.png clipboard" },
        { "screenshot clipboard", "Screenshot", "", "script: screenshot clipboard" },
        { "screenshot", "RawScriptStep", "screenshot:Malformed screenshot step restored as raw script text.", "throws: Step 1: Screenshot requires at least one destination: output <path> or clipboard." },
        { "screenshot bogus", "RawScriptStep", "screenshot bogus:Malformed screenshot step restored as raw script text.", "throws: Step 1: Unknown screenshot token 'bogus'." },
        { "window active title t", "WindowCommand", "", "script: window active title t" },
        { "window active class c", "WindowCommand", "", "script: window active class c" },
        { "window bogus", "RawScriptStep", "window bogus:Malformed window step restored as raw script text.", "throws: Step 1: Unknown window sub-command 'bogus'. Expected: active, search, focus, close, wait, move, resize, fullscreen, maximize, float, center, getdesktop, setdesktop, setdesktopforwindow." },
        { "window active", "RawScriptStep", "window active:Malformed window step restored as raw script text.", "throws: Step 1: Syntax: window active title|class|address|fullscreen|maximize|float|pinned|hidden|geometry <var>" },
        { "pixelcolor 10 20 color", "PixelColor", "", "script: pixelcolor 10 20 color" },
        { "pixelcolor rel -1 2 under", "PixelColor", "", "script: pixelcolor rel -1 2 under" },
        { "pixelcolor 1 2", "PixelColor", "", "script: pixelcolor 1 2 color" },
        { "waitcolor 1 2 FF0000 100 ok", "WaitColor", "", "script: waitcolor 1 2 FF0000 100 ok" },
        { "set x 1\nset y 2\nset n 3\nset c FF0000\nset d 5\nwaitcolor 1 2 $c 100 ok", "SetVariable SetVariable SetVariable SetVariable SetVariable WaitColor", "", "script: set x 1\nset y 2\nset n 3\nset c FF0000\nset d 5\nwaitcolor 1 2 $c 100 ok" },
        { "waitcolor 1 2 $1bad 100 ok", "RawScriptStep", "waitcolor 1 2 $1bad 100 ok:Unsupported step restored as raw script text.", "throws: Step 1: Invalid variable name '$1bad'. Allowed pattern: [A-Za-z_][A-Za-z0-9_]*" },
        { "pixelsearch 0 0 10 10 123456 found found_x found_y timeout 5000 tolerance 26", "PixelSearch", "", "script: pixelsearch 0 0 10 10 123456 found found_x found_y timeout 5000 tolerance 26" },
        { "pixelsearch 0 0 10 10 FF0000 found x y", "PixelSearch", "", "script: pixelsearch 0 0 10 10 FF0000 found x y timeout 5000 tolerance 0" },
        { "set x 1\nset y 2\nset n 3\nset c FF0000\nset d 5\npixelsearch 0 0 10 10 $c found fx fy tolerance 3", "SetVariable SetVariable SetVariable SetVariable SetVariable PixelSearch", "", "script: set x 1\nset y 2\nset n 3\nset c FF0000\nset d 5\npixelsearch 0 0 10 10 $c found fx fy timeout 5000 tolerance 3" },
        { "pixelsearch 0 0 0 10 FF0000", "RawScriptStep", "pixelsearch 0 0 0 10 FF0000:Unsupported step restored as raw script text.", "script: pixelsearch 0 0 0 10 FF0000" },
        { "pixelsearch 0 0 10 10 nothex", "RawScriptStep", "pixelsearch 0 0 10 10 nothex:Unsupported step restored as raw script text.", "throws: Step 1: Invalid color. Expected 6 hexadecimal RGB characters (RRGGBB) or $variable." },
        { "imageclick 0 0 100 100 Target button right similarity 0.9", "ImageClick", "", "script: imageclick 0 0 100 100 Target found found_x found_y button right timeout 5000 similarity 0.9" },
        { "imageclick Target", "ImageClick", "", "script: imageclick 0 0 1920 1080 Target found found_x found_y button left timeout 5000 similarity 0.95" },
        { "set x 1\nset y 2\nset n 3\nset c FF0000\nset d 5\nimageclick region $x $y 100 100 TargetImage button right timeout 5000", "SetVariable SetVariable SetVariable SetVariable SetVariable ImageClick", "", "script: set x 1\nset y 2\nset n 3\nset c FF0000\nset d 5\nimageclick region $x $y 100 100 TargetImage found found_x found_y button right timeout 5000 similarity 0.95" },
        { "imageclick region 0 nope 10 10 TargetImage", "RawScriptStep", "imageclick region 0 nope 10 10 TargetImage:Unsupported step restored as raw script text.", "throws: Step 1: Invalid imageclick region. Expected integer or $variable left/top and positive integer or $variable width/height." },
        { "imagesearch 0 0 10 10 TargetImage found found_x found_y similarity 0.85", "ImageSearch", "", "script: imagesearch 0 0 10 10 TargetImage found found_x found_y similarity 0.85" },
        { "imagesearch 0 0 10 10 img found x y similarity 1 matchmode BEST", "ImageSearch", "", "script: imagesearch 0 0 10 10 img found x y similarity 1 matchmode best" },
        { "waitimage 0 0 10 10 img timeout 100", "WaitImage", "", "script: waitimage 0 0 10 10 img found found_x found_y timeout 100 similarity 0.95" },
        { "imagesearch 0 0 a 5 img", "RawScriptStep", "imagesearch 0 0 a 5 img:Unsupported step restored as raw script text.", "throws: Step 1: Invalid imagesearch bounds. Expected integer x1 y1 x2 y2." },
        { "frobnicate 1 2", "RawScriptStep", "frobnicate 1 2:Unsupported step restored as raw script text.", "throws: Step 1: unsupported step syntax 'frobnicate 1 2'." },
        { "set x 1\n\n   inc x\n  repeat 2 {\n    delay 5\n  }", "SetVariable IncrementVariable RepeatBlockStart Delay BlockEnd", "", "script: set x 1\ninc x 1\nrepeat 2 {\ndelay 5\n}" },
        { "tap ctrl+c", "RawScriptStep", "tap ctrl+c:Unsupported step restored as raw script text.", "throws: Step 1: tap expects either exactly one non-modifier key (example: ctrl+c) or a single modifier key." },
    };

    [Theory]
    [MemberData(nameof(Cases))]
    public void RestoreThenCompile_ProducesPinnedProjection(string script, string expectedTypes, string expectedWarnings, string expectedOutput)
    {
        ArgumentNullException.ThrowIfNull(script);
        var converter = new EditorActionConverter(Substitute.For<IKeyCodeMapper>());
        var sequence = new MacroSequence();
        sequence.ReplaceScriptSteps(script.Split('\n'));

        var result = converter.FromMacroSequenceWithDiagnostics(sequence);

        Assert.True(result.RestoredFromScriptSteps);
        Assert.Equal(expectedTypes, string.Join(' ', result.Actions.Select(action => action.Type)));
        Assert.Equal(expectedWarnings, string.Join(" | ", result.Warnings.Select(warning => warning.Step + ":" + warning.Message)));
        Assert.Equal(expectedOutput, Compile(converter, result.Actions));
    }

    private static string Compile(EditorActionConverter converter, IReadOnlyList<EditorAction> actions)
    {
        try
        {
            var compiled = converter.ToMacroSequence(actions, "characterization", isAbsolute: false);
            return compiled.ScriptSteps.Count > 0
                ? "script: " + string.Join('\n', compiled.ScriptSteps)
                : "events: " + string.Join(';', compiled.Events.Select(Describe));
        }
        catch (InvalidOperationException ex)
        {
            return "throws: " + ex.Message;
        }
    }

    private static string Describe(MacroEvent ev) =>
        string.Join('/', ev.Type, ev.X.ToString(CultureInfo.InvariantCulture), ev.Y.ToString(CultureInfo.InvariantCulture), ev.Button, ev.DelayMicroseconds.ToString(CultureInfo.InvariantCulture));
}
