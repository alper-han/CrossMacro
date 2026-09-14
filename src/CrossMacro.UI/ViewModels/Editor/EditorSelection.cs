namespace CrossMacro.UI.ViewModels.Editor;

internal sealed class EditorSelection
{
    internal EditorAction? PrimaryAction { get; set; }
    internal EditorActionListItem? SelectedRow { get; set; }
    internal ObservableCollection<int> UnderlyingIndices { get; } = [];
    internal bool IsSynchronizingIndices { get; set; }
    internal bool IsSelectingFromList { get; set; }

    internal static int[] Normalize(IEnumerable<int> indices, int actionCount) =>
        indices.Where(index => index >= 0 && index < actionCount).Distinct().Order().ToArray();

    internal void ReplaceIndices(IEnumerable<int> indices)
    {
        var snapshot = indices.ToArray();
        var previous = IsSynchronizingIndices;
        IsSynchronizingIndices = true;
        try
        {
            UnderlyingIndices.Clear();
            foreach (var index in snapshot) { UnderlyingIndices.Add(index); }
        }
        finally { IsSynchronizingIndices = previous; }
    }
}
