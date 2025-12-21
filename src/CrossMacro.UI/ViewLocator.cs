using System;
using System.Diagnostics.CodeAnalysis;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using CrossMacro.UI.ViewModels;

namespace CrossMacro.UI;

/// <summary>
/// Given a view model, returns the corresponding view if possible.
/// </summary>
// [RequiresUnreferencedCode] removed to avoid warning in App.axaml. Warns internally instead, which we suppress.
public class ViewLocator : IDataTemplate
{
    [UnconditionalSuppressMessage("Trimming", "IL2057", Justification = "ViewModel to View mapping by convention")]
    [UnconditionalSuppressMessage("Trimming", "IL2096", Justification = "View creation via reflection")]
    public Control? Build(object? param)
    {
        if (param is null)
            return null;
        
        var fullName = param.GetType().FullName;
        if (fullName == null)
            return new TextBlock { Text = "Error: Type has no FullName" };
        
        var name = fullName.Replace("ViewModel", "View", StringComparison.Ordinal);
        var type = Type.GetType(name);

        if (type != null)
        {
            var instance = Activator.CreateInstance(type);
            if (instance is Control control)
            {
                return control;
            }
        }
        
        return new TextBlock { Text = "Not Found: " + name };
    }

    public bool Match(object? data)
    {
        return data is ViewModelBase;
    }
}
