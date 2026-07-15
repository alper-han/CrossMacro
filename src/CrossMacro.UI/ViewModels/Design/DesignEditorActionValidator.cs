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

internal sealed class DesignEditorActionValidator : IEditorActionValidator
{
    public (bool IsValid, string? Error) Validate(EditorAction action) => (true, null);

    public (bool IsValid, List<string> Errors) ValidateAll(IEnumerable<EditorAction> actions) => (true, new List<string>());
}
