namespace CrossMacro.UI.Views.Tabs;

public partial class EditorTabView : UserControl
{
    private EditorViewModel? _draggedDocument;
    private Point _dragStart;
    private bool _isDragging;

    public EditorTabView()
    {
        InitializeComponent();
        AddHandler(PointerMovedEvent, OnPointerMoved, RoutingStrategies.Tunnel);
        AddHandler(PointerReleasedEvent, OnPointerReleased, RoutingStrategies.Tunnel);
    }

    private EditorWorkspaceViewModel? Workspace => DataContext as EditorWorkspaceViewModel;

    private void OnNewTabClicked(object? sender, RoutedEventArgs e)
    {
        Workspace?.NewTab();
    }

    private void OnDocumentTabClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { Tag: EditorViewModel document }
            && Workspace is { } workspace)
        {
            workspace.ActiveDocument = document;
        }
    }

    private void OnDocumentTabDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (sender is not Control { Tag: EditorViewModel document }
            || Workspace is not { } workspace)
        {
            return;
        }

        workspace.ActiveDocument = document;
        document.BeginTabRename();
        Dispatcher.UIThread.Post(() => FocusTabRenameTextBox(document), DispatcherPriority.Input);
        e.Handled = true;
    }

    private void OnTabRenameTextBoxGotFocus(object? sender, RoutedEventArgs e)
    {
        if (sender is TextBox textBox)
        {
            textBox.SelectAll();
        }
    }

    private void OnTabRenameTextBoxLostFocus(object? sender, RoutedEventArgs e)
    {
        if (sender is TextBox { Tag: EditorViewModel document })
        {
            document.CommitTabRename();
        }
    }

    private void OnTabRenameTextBoxKeyDown(object? sender, KeyEventArgs e)
    {
        if (sender is not TextBox { Tag: EditorViewModel document })
        {
            return;
        }

        if (e.Key is Key.Enter)
        {
            document.CommitTabRename();
            e.Handled = true;
        }
        else if (e.Key is Key.Escape)
        {
            document.CancelTabRename();
            e.Handled = true;
        }
    }

#pragma warning disable MA0155 // Avalonia routed-event handlers must return void.
    private async void OnCloseTabClicked(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        if (sender is Control { Tag: EditorViewModel document }
            && Workspace is { } workspace)
        {
            await workspace.CloseDocumentAsync(document).ConfigureAwait(true);
        }
    }

    private async void OnCloseOtherTabsClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { Tag: EditorViewModel document }
            && Workspace is { } workspace)
        {
            await workspace.CloseOtherDocumentsAsync(document).ConfigureAwait(true);
        }
    }

    private async void OnCloseAllTabsClicked(object? sender, RoutedEventArgs e)
    {
        if (Workspace is { } workspace)
        {
            await workspace.CloseAllDocumentsAsync().ConfigureAwait(true);
        }
    }

#pragma warning restore MA0155

    private void OnMoveTabLeftClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { Tag: EditorViewModel document })
        {
            Workspace?.MoveDocumentLeft(document);
        }
    }

    private void OnMoveTabRightClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { Tag: EditorViewModel document })
        {
            Workspace?.MoveDocumentRight(document);
        }
    }

    private void OnDocumentTabPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.PointerUpdateKind is not PointerUpdateKind.LeftButtonPressed
            || sender is not Border { Tag: EditorViewModel document })
        {
            return;
        }

        _draggedDocument = document;
        _dragStart = e.GetPosition(this);
        _isDragging = false;
        if (Workspace is { } workspace)
        {
            workspace.ActiveDocument = document;
        }
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_draggedDocument is null || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }

        var position = e.GetPosition(this);
        if (!_isDragging && Math.Abs(position.X - _dragStart.X) < 6)
        {
            return;
        }

        _isDragging = true;
        var destination = GetDestinationIndex(position.X);
        if (destination >= 0)
        {
            Workspace?.MoveDocument(_draggedDocument, destination);
        }
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        _draggedDocument = null;
        _isDragging = false;
    }

    private int GetDestinationIndex(double pointerX)
    {
        var workspace = Workspace;
        if (workspace is null)
        {
            return -1;
        }

        var tabs = this.GetVisualDescendants()
            .OfType<Border>()
            .Where(border => border.Classes.Contains("editor-document-tab") && border.Tag is EditorViewModel)
            .OrderBy(border => border.TranslatePoint(default, this)?.X ?? double.MaxValue)
            .ToArray();
        for (var index = 0; index < tabs.Length; index++)
        {
            var left = tabs[index].TranslatePoint(default, this)?.X ?? double.MaxValue;
            if (pointerX < left + (tabs[index].Bounds.Width / 2))
            {
                return index;
            }
        }

        return tabs.Length - 1;
    }

    private void FocusTabRenameTextBox(EditorViewModel document)
    {
        var textBox = this.GetVisualDescendants()
            .OfType<TextBox>()
            .FirstOrDefault(control => ReferenceEquals(control.Tag, document));
        if (textBox is not null)
        {
            _ = textBox.Focus();
        }
    }
}
