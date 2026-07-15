using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CrossMacro.Application.Automation;
using CrossMacro.Core.Models;
using CrossMacro.Core.Services;
using CrossMacro.UI.Localization;
using CrossMacro.UI.Services;

namespace CrossMacro.UI.ViewModels;

internal sealed class DesignEditorActionConverter : IEditorActionConverter
{
    public List<MacroEvent> ToMacroEvents(EditorAction action) => new();

    public EditorAction FromMacroEvent(MacroEvent ev, MacroEvent? nextEvent = null) => new() { Type = EditorActionType.Delay, DelayMs = ev.DelayMs };

    public MacroSequence ToMacroSequence(IEnumerable<EditorAction> actions, string name, bool isAbsolute, bool skipInitialZeroZero = false)
    {
        var macro = DesignPreviewSamples.CreateMacro(name);
        macro.IsAbsoluteCoordinates = isAbsolute;
        macro.SkipInitialZeroZero = skipInitialZeroZero;
        return macro;
    }

    public List<EditorAction> FromMacroSequence(MacroSequence sequence) => DesignPreviewSamples.CreateEditorActions().ToList();

    public EditorActionRestoreResult FromMacroSequenceWithDiagnostics(MacroSequence sequence)
    {
        return new EditorActionRestoreResult(DesignPreviewSamples.CreateEditorActions().ToList(), new List<EditorActionRestoreWarning>(), restoredFromScriptSteps: true);
    }
}
