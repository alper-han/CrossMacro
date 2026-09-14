namespace CrossMacro.UI.ViewModels.Editor;

/// <summary>
/// Owns the editor documents that are open for the current application session.
/// </summary>
public class EditorWorkspaceViewModel : ViewModelBase, IDisposable
{
    private const string MacroFileExtension = ".macro";
    private static readonly StringComparison PathComparison = OperatingSystem.IsWindows()
        ? StringComparison.OrdinalIgnoreCase
        : StringComparison.Ordinal;
    private readonly EditorViewModel _initialDocument;
    private readonly IDialogService _dialogService;
    private readonly ILocalizationService _localizationService;
    private bool _disposed;
    private int _nextUntitledNumber = 1;

    public EditorWorkspaceViewModel(
        EditorViewModel initialDocument,
        IDialogService dialogService,
        ILocalizationService localizationService)
    {
        _initialDocument = initialDocument ?? throw new ArgumentNullException(nameof(initialDocument));
        _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
        _localizationService = localizationService ?? throw new ArgumentNullException(nameof(localizationService));
        Documents = new ObservableCollection<EditorViewModel>();
        AddDocument(_initialDocument);
    }

    public ObservableCollection<EditorViewModel> Documents { get; }

    public EditorViewModel? ActiveDocument
    {
        get;
        set
        {
            if (ReferenceEquals(field, value))
            {
                return;
            }

            if (field is { } previousDocument)
            {
                previousDocument.IsActive = false;
                if (previousDocument.IsCapturing)
                {
                    previousDocument.CancelCapture();
                }
            }

            field = value;
            if (field is { } activeDocument)
            {
                activeDocument.IsActive = true;
                StatusChanged?.Invoke(this, activeDocument.Status);
            }

            OnPropertyChanged();
        }
    }

    public event EventHandler<EditorDocumentMacroCreatedEventArgs>? MacroCreated;

    public event EventHandler<EditorDocumentPlaybackAddRequestedEventArgs>? PlaybackAddRequested;

    public event EventHandler<string>? StatusChanged;

    public void NewTab()
    {
        ThrowIfDisposed();
        var document = CreateDocument();
        Documents.Add(document);
        ActiveDocument = document;
    }

    public async Task LoadMacroAsync()
    {
        ThrowIfDisposed();
        var filters = new[]
        {
            new FileDialogFilter { Name = _localizationService["Editor_MacroFileDialogName"], Extensions = [MacroFileExtension.TrimStart('.')] },
        };
        var filePath = await _dialogService.ShowOpenFileDialogAsync(_localizationService["Editor_LoadDialogTitle"], filters).ConfigureAwait(true);
        if (!string.IsNullOrWhiteSpace(filePath))
        {
            await OpenMacroFileAsync(filePath).ConfigureAwait(true);
        }
    }

    public async Task OpenMacroFileAsync(string filePath)
    {
        ThrowIfDisposed();
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        var normalizedPath = Path.GetFullPath(filePath);
        var existing = Documents.FirstOrDefault(document => string.Equals(document.SourcePath, normalizedPath, PathComparison));
        if (existing is not null)
        {
            ActiveDocument = existing;
            return;
        }

        var target = ActiveDocument is { IsEmptyAndClean: true }
            ? ActiveDocument
            : CreateDocument();
        var isNewDocument = !Documents.Contains(target);
        if (await target.LoadMacroFromFileAsync(normalizedPath).ConfigureAwait(true))
        {
            await RunOnUiThreadAsync(() =>
            {
                if (isNewDocument)
                {
                    Documents.Add(target);
                }

                ActiveDocument = target;
            }).ConfigureAwait(true);
            return;
        }

        if (isNewDocument)
        {
            target.Dispose();
        }
    }

    public Task CloseDocumentAsync(EditorViewModel? document) => CloseDocumentCoreAsync(document);

    public Task CloseOtherDocumentsAsync(EditorViewModel? document) => CloseManyAsync(Documents.Where(item => !ReferenceEquals(item, document)).ToArray());

    public Task CloseAllDocumentsAsync() => CloseManyAsync(Documents.ToArray());

    public async Task<bool> ConfirmApplicationCloseAsync()
    {
        foreach (var document in Documents)
        {
            if (!await ConfirmDocumentCloseAsync(document).ConfigureAwait(true))
            {
                return false;
            }
        }

        return true;
    }

    public void MoveDocumentLeft(EditorViewModel? document)
    {
        if (document is not null)
        {
            MoveDocument(document, Documents.IndexOf(document) - 1);
        }
    }

    public void MoveDocumentRight(EditorViewModel? document)
    {
        if (document is not null)
        {
            MoveDocument(document, Documents.IndexOf(document) + 1);
        }
    }

    public void MoveDocument(EditorViewModel? document, int destinationIndex)
    {
        if (document is null)
        {
            return;
        }

        var sourceIndex = Documents.IndexOf(document);
        if (sourceIndex < 0)
        {
            return;
        }

        var targetIndex = Math.Clamp(destinationIndex, 0, Documents.Count - 1);
        if (sourceIndex != targetIndex)
        {
            Documents.Move(sourceIndex, targetIndex);
        }
    }

    public void ClearLoadedMacroSessionLink(Guid sessionId)
    {
        foreach (var document in Documents.Where(document => document.LinkedLoadedMacroSessionId == sessionId))
        {
            document.ClearLoadedMacroSessionLink();
        }
    }

    private EditorViewModel CreateDocument()
    {
        var document = _initialDocument.CreateNewDocument();
        document.SetUntitledTabTitle($"Untitled {_nextUntitledNumber.ToString(CultureInfo.InvariantCulture)}");
        _nextUntitledNumber++;
        document.MacroCreated += OnDocumentMacroCreated;
        document.PlaybackAddRequested += OnDocumentPlaybackAddRequested;
        document.LoadRequested += OnDocumentLoadRequested;
        document.StatusChanged += OnDocumentStatusChanged;
        return document;
    }

    private void AddDocument(EditorViewModel document)
    {
        document.SetUntitledTabTitle($"Untitled {_nextUntitledNumber.ToString(CultureInfo.InvariantCulture)}");
        _nextUntitledNumber++;
        document.MacroCreated += OnDocumentMacroCreated;
        document.PlaybackAddRequested += OnDocumentPlaybackAddRequested;
        document.LoadRequested += OnDocumentLoadRequested;
        document.StatusChanged += OnDocumentStatusChanged;
        Documents.Add(document);
        ActiveDocument = document;
    }

    private void OnDocumentLoadRequested(object? sender, EventArgs e)
    {
        _ = LoadMacroAsync();
    }

    private async Task CloseManyAsync(IReadOnlyList<EditorViewModel> documents)
    {
        foreach (var document in documents)
        {
            if (!Documents.Contains(document)
                || !await ConfirmDocumentCloseAsync(document).ConfigureAwait(true))
            {
                return;
            }
        }

        await RunOnUiThreadAsync(() =>
        {
            foreach (var document in documents)
            {
                RemoveDocument(document);
            }
        }).ConfigureAwait(true);
    }

    private async Task<bool> CloseDocumentCoreAsync(EditorViewModel? document)
    {
        if (document is null || !Documents.Contains(document) || document.IsRunningTest || document.IsRunningSelectedTest)
        {
            return false;
        }

        if (!await ConfirmDocumentCloseAsync(document).ConfigureAwait(true))
        {
            return false;
        }

        await RunOnUiThreadAsync(() => RemoveDocument(document)).ConfigureAwait(true);
        return true;
    }

    private void RemoveDocument(EditorViewModel document)
    {
        var index = Documents.IndexOf(document);
        if (index < 0)
        {
            return;
        }

        Documents.RemoveAt(index);
        document.MacroCreated -= OnDocumentMacroCreated;
        document.PlaybackAddRequested -= OnDocumentPlaybackAddRequested;
        document.LoadRequested -= OnDocumentLoadRequested;
        document.StatusChanged -= OnDocumentStatusChanged;
        if (ReferenceEquals(ActiveDocument, document))
        {
            ActiveDocument = Documents.Count is 0 ? null : Documents[Math.Min(index, Documents.Count - 1)];
        }

        document.Dispose();
    }

    private async Task<bool> ConfirmDocumentCloseAsync(EditorViewModel document)
    {
        if (document.IsRunningTest || document.IsRunningSelectedTest)
        {
            return false;
        }

        if (!document.IsDirty)
        {
            return true;
        }

        var choice = await _dialogService.ShowUnsavedChangesAsync(
            _localizationService["Editor_UnsavedChangesTitle"],
            string.Format(_localizationService.CurrentCulture, _localizationService["Editor_UnsavedChangesMessage"], document.TabTitle),
            _localizationService["Editor_Save"],
            _localizationService["Editor_Discard"],
            _localizationService["Editor_Cancel"]).ConfigureAwait(true);
        if (choice is UnsavedChangesChoice.Cancel)
        {
            return false;
        }

        if (choice is UnsavedChangesChoice.Save && !await document.SaveMacroForCloseAsync().ConfigureAwait(true))
        {
            return false;
        }

        return true;
    }

    private void OnDocumentMacroCreated(object? sender, EditorMacroCreatedEventArgs e)
    {
        if (sender is EditorViewModel document)
        {
            MacroCreated?.Invoke(this, new EditorDocumentMacroCreatedEventArgs(document, e));
        }
    }

    private void OnDocumentPlaybackAddRequested(object? sender, EditorMacroPlaybackRequestedEventArgs e)
    {
        if (sender is EditorViewModel document)
        {
            PlaybackAddRequested?.Invoke(this, new EditorDocumentPlaybackAddRequestedEventArgs(document, e));
        }
    }

    private void OnDocumentStatusChanged(object? sender, string status)
    {
        if (ReferenceEquals(sender, ActiveDocument))
        {
            StatusChanged?.Invoke(this, status);
        }
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        foreach (var document in Documents)
        {
            document.MacroCreated -= OnDocumentMacroCreated;
            document.PlaybackAddRequested -= OnDocumentPlaybackAddRequested;
            document.LoadRequested -= OnDocumentLoadRequested;
            document.StatusChanged -= OnDocumentStatusChanged;
            document.Dispose();
        }

        Documents.Clear();
        ActiveDocument = null;
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }
}
