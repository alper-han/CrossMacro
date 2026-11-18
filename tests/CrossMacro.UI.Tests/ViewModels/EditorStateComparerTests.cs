using System.Reflection;

namespace CrossMacro.UI.Tests.ViewModels;

public sealed class EditorStateComparerTests
{
    [Fact]
    public void DocumentSession_WhenOnlyClipboardCopyShortcutChanges_IsDirty()
    {
        var session = new EditorDocumentSession();
        var action = new EditorAction
        {
            Type = EditorActionType.CopySelectionToVariable,
            ScriptVariableName = "selection",
        };
        session.Actions.Add(action);
        session.MarkClean(session.Capture("macro", skipInitialZeroZero: false));
        Assert.False(session.IsDirty("macro", skipInitialZeroZero: false));

        action.ClipboardCopyShortcut = ClipboardCopyShortcut.CtrlShiftC;

        Assert.True(session.IsDirty("macro", skipInitialZeroZero: false));
    }

    [Fact]
    public void AreActionsEquivalent_DetectsChangeOfEveryEditableActionProperty()
    {
        // Identity and list position are not document content; every other writable
        // property must take part in dirty tracking and undo coalescing.
        string[] notContent = [nameof(EditorAction.Id), nameof(EditorAction.Index)];
        var properties = typeof(EditorAction)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(property => property.GetIndexParameters().Length is 0
                && property.GetSetMethod() is not null
                && !notContent.Contains(property.Name, StringComparer.Ordinal))
            .ToList();

        var verified = 0;
        var unverifiable = new List<string>();
        foreach (var property in properties)
        {
            var baseline = new EditorAction();
            var changed = new EditorAction();
            var original = property.GetValue(changed);
            property.SetValue(changed, DifferentValue(property.PropertyType, original));
            if (Equals(property.GetValue(changed), original))
            {
                unverifiable.Add(property.Name);
                continue;
            }

            Assert.False(
                EditorStateComparer.AreActionsEquivalent(baseline, changed),
                $"{property.Name} is not part of the editor state comparison.");
            verified++;
        }

        Assert.True(verified > 90, string.Create(System.Globalization.CultureInfo.InvariantCulture, $"Only {verified} properties were verified; skipped: {string.Join(", ", unverifiable)}"));
    }

    private static object? DifferentValue(Type type, object? current)
    {
        if (type == typeof(bool)) { return !(bool)current!; }
        if (type == typeof(int)) { return (int)current! + 3; }
        if (type == typeof(long)) { return (long)current! + 3000; }
        if (type == typeof(double)) { return (double)current! + 0.25; }
        if (type == typeof(string)) { return (current as string ?? string.Empty) + "7"; }
        if (type.IsEnum)
        {
            return Enum.GetValues(type).Cast<object>().First(value => !value.Equals(current));
        }

        throw new InvalidOperationException($"Unsupported property type {type}.");
    }
}
