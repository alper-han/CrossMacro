using CrossMacro.Core.Models.Automation.TextExpansion;

namespace CrossMacro.UI.Tests.ViewModels;

public sealed class TextExpansionViewModelTests : IDisposable
{
    private readonly ITextExpansionStore _storageService;
    private readonly IDialogService _dialogService;
    private readonly IEnvironmentInfoProvider _environmentInfoProvider;
    private readonly ILocalizationService _localizationService;
    private readonly TextExpansionViewModel _viewModel;

    public TextExpansionViewModelTests()
    {
        _storageService = Substitute.For<ITextExpansionStore>();
        _dialogService = Substitute.For<IDialogService>();
        _environmentInfoProvider = Substitute.For<IEnvironmentInfoProvider>();
        _localizationService = TestLocalization.Echo(
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["TextExpansion_Items"] = "{0} items",
                ["TextExpansion_DeleteTitle"] = "Delete Expansion",
                ["TextExpansion_DeleteMessage"] = "Are you sure you want to delete the expansion '{0}'?",
                ["TextExpansion_StatusInitFailed"] = "load failed: {0}",
                ["TextExpansion_StatusAddFailed"] = "add failed: {0}",
                ["TextExpansion_StatusRemoveFailed"] = "remove failed: {0}",
                ["TextExpansion_StatusToggleFailed"] = "toggle failed: {0}",
            },
            culture: CultureInfo.GetCultureInfo("en"));

        _ = _storageService.LoadAsync().Returns(new List<TextExpansionEntry>());

        _viewModel = CreateViewModel();
    }

    public void Dispose()
    {
        _viewModel.Dispose();
    }

    [Fact]
    public async Task InitializeAsync_LoadsStoredExpansions()
    {
        var list = new List<TextExpansionEntry> { new TextExpansionEntry(":test", "result") };
        _ = _storageService.LoadAsync().Returns(list);

        using var vm = CreateViewModel();

        await vm.InitializeAsync();
        _ = vm.Expansions.Should().HaveCount(1);
        _ = vm.Expansions[0].Trigger.Should().Be(":test");
    }

    [Fact]
    public async Task InitializeAsync_WhenSavedExpansionsExist_RefreshesComputedCountProperties()
    {
        var list = new List<TextExpansionEntry>
        {
            new(":a", "first"),
            new(":b", "second"),
        };
        _ = _storageService.LoadAsync().Returns(list);

        using var vm = CreateViewModel();

        await vm.InitializeAsync();

        _ = vm.HasExpansions.Should().BeTrue();
        _ = vm.ExpansionCountText.Should().Be("2 items");
    }

    [Fact]
    public async Task ProfileRefresh_AfterAddToggleAndRemove_ReplacesExpansionsAndRaisesNotifications()
    {
        var initial = new List<TextExpansionEntry>
        {
            new(":old", "old"),
            new(":keep", "keep"),
        };
        var refreshed = new List<TextExpansionEntry>
        {
            new(":profile", "profile"),
        };
        _ = _storageService.LoadAsync().Returns(initial);
        _ = _storageService.SaveAsync(Arg.Any<IEnumerable<TextExpansionEntry>>()).Returns(call =>
        {
            initial = call.Arg<IEnumerable<TextExpansionEntry>>().ToList();
            _ = _storageService.LoadAsync().Returns(_ => initial);
            return Task.CompletedTask;
        });
        using var vm = CreateViewModel();
        var changedProperties = new List<string?>();
        vm.PropertyChanged += (_, args) => changedProperties.Add(args.PropertyName);

        await vm.InitializeAsync();
        vm.TriggerInput = ":new";
        vm.ReplacementInput = "value";
        await vm.AddExpansionCommand.ExecuteAsync(parameter: null);
        _ = vm.Expansions[0].Trigger.Should().Be(":new");
        _ = vm.Expansions.Should().HaveCount(3);

        vm.Expansions[0].Replacement = "edited";
        vm.Expansions[0].IsEnabled = false;
        await vm.ToggleExpansionCommand.ExecuteAsync(vm.Expansions[0]);
        _ = vm.Expansions[0].IsEnabled.Should().BeFalse();
        vm.Expansions[0].IsEnabled = true;
        await vm.ToggleExpansionCommand.ExecuteAsync(vm.Expansions[0]);
        _ = vm.Expansions[0].IsEnabled.Should().BeTrue();
        _ = _dialogService.ShowConfirmationAsync(Arg.Any<string>(), Arg.Any<string>(), "Yes", "No")
            .Returns(Task.FromResult(true));
        await vm.RemoveExpansionCommand.ExecuteAsync(vm.Expansions[1]);
        _ = _storageService.LoadAsync().Returns(refreshed);
        await vm.RefreshProfileDataAsync();

        _ = vm.Expansions.Select(expansion => expansion.Trigger)
            .Should().Equal(":profile");
        _ = changedProperties.Should().Contain(nameof(TextExpansionViewModel.HasExpansions));
        _ = changedProperties.Should().Contain(nameof(TextExpansionViewModel.ExpansionCountText));
        await _storageService.Received().SaveAsync(Arg.Any<IEnumerable<TextExpansionEntry>>());
    }

    [Fact]
    public async Task AddExpansion_AddsToListAndSaves()
    {
        _viewModel.TriggerInput = ":new";
        _viewModel.ReplacementInput = "value";
        await _viewModel.AddExpansionCommand.ExecuteAsync(parameter: null);
        _ = _viewModel.Expansions.Should().HaveCount(1);
        _ = _viewModel.Expansions[0].Trigger.Should().Be(":new");
        _ = _viewModel.Expansions[0].InsertionMode.Should().Be(TextInsertionMode.Paste);
        _ = _viewModel.TriggerInput.Should().BeEmpty();

        await _storageService.Received(1).SaveAsync(Arg.Any<IEnumerable<TextExpansionEntry>>());
    }

    [Fact]
    public async Task AddExpansion_PreservesMultilineReplacement()
    {
        const string replacement = "first line\nsecond line\nthird line";
        _viewModel.TriggerInput = ":message";
        _viewModel.ReplacementInput = replacement;
        await _viewModel.AddExpansionCommand.ExecuteAsync(parameter: null);
        _ = _viewModel.Expansions.Should().ContainSingle();
        _ = _viewModel.Expansions[0].Replacement.Should().Be(replacement);
        await _storageService.Received(1).SaveAsync(Arg.Is<IEnumerable<TextExpansionEntry>>(expansions =>
            expansions.Single().Replacement == replacement));
    }

    [Fact]
    public async Task ManagedExpansion_ListCallsPortAndLoadsCollection()
    {
        var expansion = new TextExpansionEntry(":managed", "value");
        var manage = Substitute.For<IManageTextExpansion>();
        _ = manage.ListAsync(cancellationToken: CancellationToken.None).Returns([expansion]);
        using var vm = CreateManagedViewModel(manage);

        await vm.InitializeAsync();

        _ = vm.Expansions.Should().ContainSingle().Which.Should().BeSameAs(expansion);
        _ = await manage.Received(1).ListAsync(cancellationToken: CancellationToken.None);
    }

    [Fact]
    public async Task ManagedExpansion_AddCallsPortBeforeCommittingCollection()
    {
        var manage = Substitute.For<IManageTextExpansion>();
        var added = new TextExpansionEntry(":new", "value");
        _ = manage.ListAsync(cancellationToken: CancellationToken.None).Returns([]);
        _ = manage.AddAsync(Arg.Any<TextExpansionEntry>(), cancellationToken: CancellationToken.None).Returns(added);
        using var vm = CreateManagedViewModel(manage);
        await vm.InitializeAsync();
        vm.TriggerInput = ":new";
        vm.ReplacementInput = "value";

        await vm.AddExpansionCommand.ExecuteAsync(parameter: null);

        _ = vm.Expansions.Should().ContainSingle().Which.Should().BeSameAs(added);
        _ = await manage.Received(1).AddAsync(Arg.Is<TextExpansionEntry>(item => item.Trigger == ":new"), cancellationToken: CancellationToken.None);
    }

    [Fact]
    public async Task ManagedExpansion_AddCommitsCollectionAndResetsInputsOnlyAfterServiceCompletes()
    {
        var manage = Substitute.For<IManageTextExpansion>();
        var addCompletion = new TaskCompletionSource<TextExpansionEntry>(TaskCreationOptions.RunContinuationsAsynchronously);
        _ = manage.ListAsync(cancellationToken: CancellationToken.None).Returns([]);
        _ = manage.AddAsync(Arg.Any<TextExpansionEntry>(), cancellationToken: CancellationToken.None).Returns(addCompletion.Task);
        using var vm = CreateManagedViewModel(manage);
        await vm.InitializeAsync();
        vm.TriggerInput = ":new";
        vm.ReplacementInput = "value";

        var add = vm.AddExpansionCommand.ExecuteAsync(parameter: null);

        _ = vm.Expansions.Should().BeEmpty();
        _ = vm.TriggerInput.Should().Be(":new");
        _ = vm.ReplacementInput.Should().Be("value");

        var added = new TextExpansionEntry(":new", "value");
        _ = addCompletion.TrySetResult(added);
        await add;

        _ = vm.Expansions.Should().ContainSingle().Which.Should().BeSameAs(added);
        _ = vm.TriggerInput.Should().BeEmpty();
        _ = vm.ReplacementInput.Should().BeEmpty();
    }

    [Fact]
    public async Task ManagedExpansion_AddFailureLeavesCollectionUnchanged()
    {
        var existing = new TextExpansionEntry(":existing", "value");
        var manage = Substitute.For<IManageTextExpansion>();
        _ = manage.ListAsync(cancellationToken: CancellationToken.None).Returns([existing]);
        _ = manage.AddAsync(Arg.Any<TextExpansionEntry>(), cancellationToken: CancellationToken.None).Returns<Task<TextExpansionEntry>>(_ =>
            Task.FromException<TextExpansionEntry>(new InvalidOperationException("duplicate")));
        using var vm = CreateManagedViewModel(manage);
        await vm.InitializeAsync();
        vm.TriggerInput = ":existing";
        vm.ReplacementInput = "replacement";

        var statuses = new List<string>();
        vm.StatusChanged += (_, status) => statuses.Add(status);

        await vm.AddExpansionCommand.ExecuteAsync(parameter: null);

        _ = statuses.Should().ContainSingle().Which.Should().Be("add failed: duplicate");
        _ = vm.Expansions.Should().ContainSingle().Which.Should().BeSameAs(existing);
        _ = await manage.Received(1).AddAsync(Arg.Is<TextExpansionEntry>(item => item.Trigger == ":existing"), cancellationToken: CancellationToken.None);
    }

    [Fact]
    public async Task AddExpansion_WhenTriggerAlreadyExists_ReportsStatusInsteadOfThrowing()
    {
        _ = _storageService.LoadAsync().Returns(new List<TextExpansionEntry> { new(":dup", "first") });
        using var vm = CreateViewModel();
        await vm.InitializeAsync();
        var statuses = new List<string>();
        vm.StatusChanged += (_, status) => statuses.Add(status);
        vm.TriggerInput = ":dup";
        vm.ReplacementInput = "second";

        await vm.AddExpansionCommand.ExecuteAsync(parameter: null);

        _ = statuses.Should().ContainSingle().Which.Should().StartWith("add failed: ");
        _ = vm.Expansions.Should().ContainSingle().Which.Replacement.Should().Be("first");
        _ = vm.TriggerInput.Should().Be(":dup");
    }

    [Fact]
    public async Task InitializeAsync_WhenStoreFails_ReportsStatusAndCompletes()
    {
        var manage = Substitute.For<IManageTextExpansion>();
        _ = manage.ListAsync(cancellationToken: CancellationToken.None).Returns<Task<IReadOnlyList<TextExpansionEntry>>>(_ =>
            Task.FromException<IReadOnlyList<TextExpansionEntry>>(new IOException("corrupt store")));
        using var vm = CreateManagedViewModel(manage);
        var statuses = new List<string>();
        vm.StatusChanged += (_, status) => statuses.Add(status);

        await vm.InitializeAsync();

        _ = statuses.Should().ContainSingle().Which.Should().Be("load failed: corrupt store");
        _ = vm.Expansions.Should().BeEmpty();
    }

    [Fact]
    public async Task ManagedExpansion_RemoveCallsPortBeforeCommittingCollection()
    {
        var expansion = new TextExpansionEntry(":remove", "value");
        var manage = Substitute.For<IManageTextExpansion>();
        _ = manage.ListAsync(cancellationToken: CancellationToken.None).Returns([expansion]);
        _ = manage.RemoveAsync(":remove", cancellationToken: CancellationToken.None).Returns(expansion);
        using var vm = CreateManagedViewModel(manage);
        await vm.InitializeAsync();
        _ = _dialogService.ShowConfirmationAsync(Arg.Any<string>(), Arg.Any<string>(), "Yes", "No")
            .Returns(Task.FromResult(true));

        await vm.RemoveExpansionCommand.ExecuteAsync(expansion);

        _ = vm.Expansions.Should().BeEmpty();
        _ = await manage.Received(1).RemoveAsync(":remove", cancellationToken: CancellationToken.None);
    }

    [Fact]
    public async Task ManagedExpansion_RemoveFailureLeavesCollectionUnchanged()
    {
        var expansion = new TextExpansionEntry(":remove", "value");
        var manage = Substitute.For<IManageTextExpansion>();
        _ = manage.ListAsync(cancellationToken: CancellationToken.None).Returns([expansion]);
        _ = manage.RemoveAsync(":remove", cancellationToken: CancellationToken.None).Returns<Task<TextExpansionEntry>>(_ =>
            Task.FromException<TextExpansionEntry>(new IOException("persistence failure")));
        using var vm = CreateManagedViewModel(manage);
        await vm.InitializeAsync();
        _ = _dialogService.ShowConfirmationAsync(Arg.Any<string>(), Arg.Any<string>(), "Yes", "No")
            .Returns(Task.FromResult(true));

        var statuses = new List<string>();
        vm.StatusChanged += (_, status) => statuses.Add(status);

        await vm.RemoveExpansionCommand.ExecuteAsync(expansion);

        _ = statuses.Should().ContainSingle().Which.Should().Be("remove failed: persistence failure");
        _ = vm.Expansions.Should().ContainSingle().Which.Should().BeSameAs(expansion);
        _ = await manage.Received(1).RemoveAsync(":remove", cancellationToken: CancellationToken.None);
    }

    [Fact]
    public async Task ManagedExpansion_SetEnabledCallsPortAndCommitsReturnedState()
    {
        var expansion = new TextExpansionEntry(":toggle", "value", isEnabled: true);
        var updated = new TextExpansionEntry(":toggle", "value", isEnabled: false);
        var manage = Substitute.For<IManageTextExpansion>();
        _ = manage.ListAsync(cancellationToken: CancellationToken.None).Returns([expansion]);
        _ = manage.SetEnabledAsync(":toggle", enabled: false, cancellationToken: CancellationToken.None).Returns(updated);
        using var vm = CreateManagedViewModel(manage);
        await vm.InitializeAsync();
        expansion.IsEnabled = false;

        await vm.ToggleExpansionCommand.ExecuteAsync(expansion);

        _ = expansion.IsEnabled.Should().BeFalse();
        _ = await manage.Received(1).SetEnabledAsync(":toggle", enabled: false, cancellationToken: CancellationToken.None);
    }

    [Fact]
    public async Task ManagedExpansion_SetEnabledFailureRestoresPriorState()
    {
        var expansion = new TextExpansionEntry(":toggle", "value", isEnabled: true);
        var manage = Substitute.For<IManageTextExpansion>();
        _ = manage.ListAsync(cancellationToken: CancellationToken.None).Returns([expansion]);
        _ = manage.SetEnabledAsync(":toggle", enabled: false, cancellationToken: CancellationToken.None).Returns<Task<TextExpansionEntry>>(_ =>
            Task.FromException<TextExpansionEntry>(new IOException("persistence failure")));
        using var vm = CreateManagedViewModel(manage);
        await vm.InitializeAsync();
        expansion.IsEnabled = false;

        var statuses = new List<string>();
        vm.StatusChanged += (_, status) => statuses.Add(status);

        await vm.ToggleExpansionCommand.ExecuteAsync(expansion);

        _ = statuses.Should().ContainSingle().Which.Should().Be("toggle failed: persistence failure");
        _ = expansion.IsEnabled.Should().BeTrue();
        _ = await manage.Received(1).SetEnabledAsync(":toggle", enabled: false, cancellationToken: CancellationToken.None);
    }

    private TextExpansionViewModel CreateViewModel(
        IEnvironmentInfoProvider? environmentInfoProvider = null,
        ILocalizationService? localizationService = null) =>
        new(
            UiAutomationTestComposition.Create(_storageService),
            _dialogService,
            environmentInfoProvider ?? _environmentInfoProvider,
            localizationService ?? _localizationService,
            uiDispatcher: ImmediateUiDispatcher.Instance);

    private TextExpansionViewModel CreateManagedViewModel(IManageTextExpansion manage) =>
        new(manage, _dialogService, _environmentInfoProvider, _localizationService, uiDispatcher: ImmediateUiDispatcher.Instance);

    [Fact]
    public void AddExpansion_CanExecute_ValidatesInput()
    {
        _viewModel.TriggerInput = "";
        _viewModel.ReplacementInput = "val";
        _ = _viewModel.AddExpansionCommand.CanExecute(parameter: null).Should().BeFalse();

        _viewModel.TriggerInput = ":key";
        _viewModel.ReplacementInput = "";
        _ = _viewModel.AddExpansionCommand.CanExecute(parameter: null).Should().BeFalse();

        _viewModel.TriggerInput = ":key";
        _viewModel.ReplacementInput = "val";
        _ = _viewModel.AddExpansionCommand.CanExecute(parameter: null).Should().BeTrue();
    }

    [Fact]
    public async Task RemoveExpansion_WhenConfirmed_RemovesAndSaves()
    {
        var expansion = new TextExpansionEntry(":del", "value");
        _ = _storageService.LoadAsync().Returns(new List<TextExpansionEntry> { expansion });
        _viewModel.Expansions.Add(expansion);

        _ = _dialogService.ShowConfirmationAsync(Arg.Any<string>(), Arg.Any<string>(), "Yes", "No")
            .Returns(Task.FromResult(true));
        await _viewModel.RemoveExpansionCommand.ExecuteAsync(expansion);
        _ = _viewModel.Expansions.Should().BeEmpty();
        await _storageService.Received(1).SaveAsync(Arg.Any<IEnumerable<TextExpansionEntry>>());
    }

    [Fact]
    public async Task RemoveExpansion_WhenCancelled_DoesNotRemove()
    {
        var expansion = new TextExpansionEntry(":keep", "value");
        _ = _storageService.LoadAsync().Returns(new List<TextExpansionEntry> { expansion });
        _viewModel.Expansions.Add(expansion);

        _ = _dialogService.ShowConfirmationAsync(Arg.Any<string>(), Arg.Any<string>(), "Yes", "No")
            .Returns(Task.FromResult(false));
        await _viewModel.RemoveExpansionCommand.ExecuteAsync(expansion);
        _ = _viewModel.Expansions.Should().HaveCount(1);
        await _storageService.DidNotReceive().SaveAsync(Arg.Any<IEnumerable<TextExpansionEntry>>());
    }

    [Fact]
    public async Task ToggleExpansion_SavesCurrentState()
    {
        var expansion = new TextExpansionEntry(":toggle", "val");
        _ = _storageService.LoadAsync().Returns(new List<TextExpansionEntry> { expansion });
        _viewModel.Expansions.Add(expansion);
        await _viewModel.ToggleExpansionCommand.ExecuteAsync(expansion);
        await _storageService.Received(1).SaveAsync(Arg.Any<IEnumerable<TextExpansionEntry>>());
    }

    [Theory]
    [InlineData(DisplayEnvironment.LinuxX11, true)]
    [InlineData(DisplayEnvironment.LinuxWayland, true)]
    [InlineData(DisplayEnvironment.LinuxWayfire, true)]
    [InlineData(DisplayEnvironment.Windows, false)]
    [InlineData(DisplayEnvironment.MacOS, false)]
    public void IsPasteMethodVisible_ReflectsEnvironment(DisplayEnvironment environment, bool expected)
    {
        var envProvider = Substitute.For<IEnvironmentInfoProvider>();
        _ = envProvider.CurrentEnvironment.Returns(environment);
        using var vm = CreateViewModel(envProvider);
        _ = vm.IsPasteMethodVisible.Should().Be(expected);
    }

    [Fact]
    public void SelectedInsertionMode_DefaultsToPaste()
    {
        _ = _viewModel.SelectedInsertionMode.Should().Be(TextInsertionMode.Paste);
    }

    [Fact]
    public void IsPasteMethodSelectorVisible_HidesWhenDirectTypingIsSelected()
    {
        var envProvider = Substitute.For<IEnvironmentInfoProvider>();
        _ = envProvider.CurrentEnvironment.Returns(DisplayEnvironment.LinuxX11);
        using var vm = CreateViewModel(envProvider);
        _ = vm.IsPasteMethodSelectorVisible.Should().BeTrue();
        vm.SelectedInsertionMode = TextInsertionMode.DirectTyping;
        _ = vm.IsPasteMethodSelectorVisible.Should().BeFalse();
    }

    [Fact]
    public void IsDirectTypingMethodSelectorVisible_ShowsOnlyWhenDirectTypingIsSelected()
    {
        _ = _viewModel.IsDirectTypingMethodSelectorVisible.Should().BeFalse();

        _viewModel.SelectedInsertionMode = TextInsertionMode.DirectTyping;

        _ = _viewModel.IsDirectTypingMethodSelectorVisible.Should().BeTrue();
    }

    [Fact]
    public async Task AddExpansion_ResetsSelectedPasteMethodToDefault()
    {
        _viewModel.SelectedPasteMethod = PasteMethod.ShiftInsert;
        _viewModel.TriggerInput = ":x";
        _viewModel.ReplacementInput = "value";
        await _viewModel.AddExpansionCommand.ExecuteAsync(parameter: null);
        _ = _viewModel.SelectedPasteMethod.Should().Be(PasteMethod.CtrlV);
    }

    [Fact]
    public async Task AddExpansion_WhenDirectTypingSelected_PersistsInsertionModeAndResetsToDefault()
    {
        _viewModel.SelectedInsertionMode = TextInsertionMode.DirectTyping;
        _viewModel.SelectedDirectTypingMethod = DirectTypingMethod.CompatibleKeyByKey;
        _viewModel.TriggerInput = ":typed";
        _viewModel.ReplacementInput = "value";
        await _viewModel.AddExpansionCommand.ExecuteAsync(parameter: null);
        _ = _viewModel.Expansions.Should().ContainSingle();
        _ = _viewModel.Expansions[0].InsertionMode.Should().Be(TextInsertionMode.DirectTyping);
        _ = _viewModel.Expansions[0].DirectTypingMethod.Should().Be(DirectTypingMethod.CompatibleKeyByKey);
        _ = _viewModel.SelectedInsertionMode.Should().Be(TextInsertionMode.Paste);
        _ = _viewModel.SelectedDirectTypingMethod.Should().Be(DirectTypingMethod.FastBatch);
    }

    [Fact]
    public async Task ToggleExpansion_WhenExpansionIsNull_DoesNotSave()
    {
        await _viewModel.ToggleExpansionCommand.ExecuteAsync(parameter: null);
        await _storageService.DidNotReceive().SaveAsync(Arg.Any<IEnumerable<TextExpansionEntry>>());
    }

    [Fact]
    public void CultureChanged_RaisesLocalizedProperties()
    {
        var localizationService = Substitute.For<ILocalizationService>();
        _ = localizationService.CurrentCulture.Returns(CultureInfo.GetCultureInfo("en"));
        _ = localizationService["TextExpansion_Items"].Returns("{0} items");
        using var vm = CreateViewModel(localizationService: localizationService);
        var changedProperties = new List<string?>();
        vm.PropertyChanged += (_, args) => changedProperties.Add(args.PropertyName);

        localizationService.CultureChanged += Raise.Event<EventHandler>(localizationService, EventArgs.Empty);

        _ = changedProperties.Should().Contain(nameof(TextExpansionViewModel.ExpansionCountText));
        _ = changedProperties.Should().Contain(nameof(TextExpansionViewModel.InsertionModes));
        _ = changedProperties.Should().Contain(nameof(TextExpansionViewModel.PasteMethods));
        _ = changedProperties.Should().Contain(nameof(TextExpansionViewModel.DirectTypingMethods));
    }

    [Fact]
    public void CultureChanged_ReplacesEnumCollections_ToTriggerConverterReevaluation()
    {
        var originalInsertionModes = _viewModel.InsertionModes;
        var originalPasteMethods = _viewModel.PasteMethods;
        var originalDirectTypingMethods = _viewModel.DirectTypingMethods;

        _localizationService.CultureChanged += Raise.Event<EventHandler>(_localizationService, EventArgs.Empty);

        _ = _viewModel.InsertionModes.Should().NotBeSameAs(originalInsertionModes);
        _ = _viewModel.PasteMethods.Should().NotBeSameAs(originalPasteMethods);
        _ = _viewModel.DirectTypingMethods.Should().NotBeSameAs(originalDirectTypingMethods);
    }
}
