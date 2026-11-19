using Avalonia.Controls;
using Avalonia.Threading;
using CrossMacro.UI.Behaviors;

namespace CrossMacro.UI.Tests.Behaviors;

[Collection(TestCollections.HeadlessStartup)]
public sealed class ListBoxSelectedActionIndicesTests
{
    [Fact]
    public Task TryDeselectSelectedSourceAction_WhenSelectedSourceRowClicked_RemovesRowFromListBoxSelection() =>
        HeadlessTestRunner.RunAsync(token =>
        {
            var row = CreateRow(underlyingIndex: 0, representsSourceAction: true);
            var listBox = new ListBox { SelectionMode = SelectionMode.Multiple };
            _ = listBox.SelectedItems!.Add(row);

            var removed = ListBoxSelectedActionIndices.TryDeselectSelectedSourceAction(listBox, row);

            Assert.True(removed);
            Assert.Empty(SelectedRows(listBox));
            return Task.CompletedTask;
        });

    [Fact]
    public Task TryDeselectSelectedSourceAction_WhenRowIsNotSourceAction_DoesNotChangeSelection() =>
        HeadlessTestRunner.RunAsync(token =>
        {
            var row = CreateRow(underlyingIndex: 0, representsSourceAction: false);
            var listBox = new ListBox { SelectionMode = SelectionMode.Multiple };
            _ = listBox.SelectedItems!.Add(row);

            var removed = ListBoxSelectedActionIndices.TryDeselectSelectedSourceAction(listBox, row);

            Assert.False(removed);
            Assert.Same(row, Assert.Single(SelectedRows(listBox)));
            return Task.CompletedTask;
        });

    [Fact]
    public Task SelectedUnderlyingIndices_WhenListBoxLeavesAndRejoinsTheVisualTree_ResyncsSelectionOnlyWhileAttached() =>
        HeadlessTestRunner.RunAsync(token =>
        {
            var rows = new ObservableCollection<EditorActionListItem>
            {
                CreateRow(underlyingIndex: 0, representsSourceAction: true),
                CreateRow(underlyingIndex: 1, representsSourceAction: true),
            };
            var selectedIndices = new ObservableCollection<int> { 0 };
            var listBox = new ListBox { SelectionMode = SelectionMode.Multiple, ItemsSource = rows };
            ListBoxSelectedActionIndices.SetSelectedUnderlyingIndices(listBox, selectedIndices);
            var window = new Window { Content = listBox };
            window.Show();
            try
            {
                Assert.Same(rows[0], Assert.Single(SelectedRows(listBox)));

                window.Content = null;
                selectedIndices.Clear();
                selectedIndices.Add(1);
                Dispatcher.UIThread.RunJobs();
                Assert.Same(rows[0], Assert.Single(SelectedRows(listBox)));

                window.Content = listBox;
                Assert.Same(rows[1], Assert.Single(SelectedRows(listBox)));

                selectedIndices.Clear();
                selectedIndices.Add(0);
                Dispatcher.UIThread.RunJobs();
                Assert.Same(rows[0], Assert.Single(SelectedRows(listBox)));
            }
            finally
            {
                window.Close();
            }

            return Task.CompletedTask;
        });

    private static object[] SelectedRows(ListBox listBox) => listBox.SelectedItems!.Cast<object>().ToArray();

    private static EditorActionListItem CreateRow(int underlyingIndex, bool representsSourceAction) => new(
        new EditorAction { Type = EditorActionType.MouseClick, X = 1, Y = 1 },
        index: underlyingIndex,
        underlyingIndex: underlyingIndex,
        indentLevel: 0,
        displayName: "Action",
        condensedHint: string.Empty,
        visualKind: EditorActionVisualKind.PointerInput,
        isImportant: false,
        isCleanupEligible: false,
        condensedHiddenCount: 0,
        representsSourceAction: representsSourceAction);
}
