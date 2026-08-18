
namespace CrossMacro.UI.Tests.ViewModels;

[Collection(LocalizationGlobalStateCollection.Name)]
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
        _localizationService = Substitute.For<ILocalizationService>();
        _ = _localizationService.CurrentCulture.Returns(System.Globalization.CultureInfo.GetCultureInfo("en"));
        _ = _localizationService[Arg.Any<string>()].Returns(call => call.Arg<string>() switch
        {
            "TextExpansion_Items" => "{0} items",
            "TextExpansion_DeleteTitle" => "Delete Expansion",
            "TextExpansion_DeleteMessage" => "Are you sure you want to delete the expansion '{0}'?",
            _ => call.Arg<string>(),
        });

        // Setup initial load
        _ = _storageService.LoadAsync().Returns(new List<TextExpansionEntry>());

        _viewModel = new TextExpansionViewModel(_storageService, _dialogService, _environmentInfoProvider, _localizationService);
    }

    public void Dispose()
    {
        _viewModel.Dispose();
    }

    [Fact]
    public async Task Construction_LoadsExpansions()
    {
        // Arrange
        var list = new List<TextExpansionEntry> { new TextExpansionEntry(":test", "result") };
        _ = _storageService.LoadAsync().Returns(list);

        // Re-create VM to trigger constructor load
        var vm = new TextExpansionViewModel(_storageService, _dialogService, _environmentInfoProvider, _localizationService);

        // Wait for async load deterministically
        await vm.InitializationTask;

        // Assert
        _ = vm.Expansions.Should().HaveCount(1);
        _ = vm.Expansions[0].Trigger.Should().Be(":test");
    }

    [Fact]
    public async Task Construction_WhenSavedExpansionsExist_RefreshesComputedCountProperties()
    {
        var list = new List<TextExpansionEntry>
        {
            new(":a", "first"),
            new(":b", "second"),
        };
        _ = _storageService.LoadAsync().Returns(list);

        var vm = new TextExpansionViewModel(_storageService, _dialogService, _environmentInfoProvider, _localizationService);

        await vm.InitializationTask;

        _ = vm.HasExpansions.Should().BeTrue();
        _ = vm.ExpansionCountText.Should().Be("2 items");
    }

    [Fact]
    public async Task RawStoreBoundary_PreservesRefreshNotificationsAndDesignPreview()
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
        _ = _storageService.LoadAsync().Returns(initial, refreshed);
        var vm = new TextExpansionViewModel(_storageService, _dialogService, _environmentInfoProvider, _localizationService);
        var changedProperties = new List<string?>();
        vm.PropertyChanged += (_, args) => changedProperties.Add(args.PropertyName);

        await vm.InitializationTask;
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
        await vm.RefreshProfileDataAsync();

        _ = vm.Expansions.Select(expansion => expansion.Trigger)
            .Should().Equal(":profile");
        _ = changedProperties.Should().Contain(nameof(TextExpansionViewModel.HasExpansions));
        _ = changedProperties.Should().Contain(nameof(TextExpansionViewModel.ExpansionCountText));
        await _storageService.Received().SaveAsync(Arg.Any<IEnumerable<TextExpansionEntry>>());

        var designViewModel = new DesignTextExpansionViewModel();
        await designViewModel.InitializationTask;
        _ = designViewModel.TriggerInput.Should().Be(":sync-ok");
        _ = designViewModel.Expansions.Should().NotBeEmpty();
    }

    [Fact]
    public async Task AddExpansion_AddsToListAndSaves()
    {
        // Arrange
        _viewModel.TriggerInput = ":new";
        _viewModel.ReplacementInput = "value";

        // Act
        // Execute the command directly
        if (_viewModel.AddExpansionCommand.CanExecute(parameter: null))
        {
            await _viewModel.AddExpansionCommand.ExecuteAsync(parameter: null);
        }

        // Assert
        _ = _viewModel.Expansions.Should().HaveCount(1);
        _ = _viewModel.Expansions[0].Trigger.Should().Be(":new");
        _ = _viewModel.Expansions[0].InsertionMode.Should().Be(TextInsertionMode.Paste);
        _ = _viewModel.TriggerInput.Should().BeEmpty(); // Should clear input

        await _storageService.Received(1).SaveAsync(Arg.Any<IEnumerable<TextExpansionEntry>>());
    }

    [Fact]
    public async Task AddExpansion_PreservesMultilineReplacement()
    {
        // Arrange
        const string replacement = "first line\nsecond line\nthird line";
        _viewModel.TriggerInput = ":message";
        _viewModel.ReplacementInput = replacement;

        // Act
        await _viewModel.AddExpansionCommand.ExecuteAsync(parameter: null);

        // Assert
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
        _ = manage.ListAsync().Returns([expansion]);
        var vm = CreateManagedViewModel(manage);

        await vm.InitializationTask;

        _ = vm.Expansions.Should().ContainSingle().Which.Should().BeSameAs(expansion);
        _ = await manage.Received(1).ListAsync();
    }

    [Fact]
    public async Task ManagedExpansion_AddCallsPortBeforeCommittingCollection()
    {
        var manage = Substitute.For<IManageTextExpansion>();
        var added = new TextExpansionEntry(":new", "value");
        _ = manage.ListAsync().Returns([]);
        _ = manage.AddAsync(Arg.Any<TextExpansionEntry>()).Returns(added);
        var vm = CreateManagedViewModel(manage);
        await vm.InitializationTask;
        vm.TriggerInput = ":new";
        vm.ReplacementInput = "value";

        await vm.AddExpansionCommand.ExecuteAsync(parameter: null);

        _ = vm.Expansions.Should().ContainSingle().Which.Should().BeSameAs(added);
        _ = await manage.Received(1).AddAsync(Arg.Is<TextExpansionEntry>(item => item.Trigger == ":new"));
    }

    [Fact]
    public async Task ManagedExpansion_AddCommitsCollectionAndResetsInputsOnlyAfterServiceCompletes()
    {
        var manage = Substitute.For<IManageTextExpansion>();
        var addCompletion = new TaskCompletionSource<TextExpansionEntry>(TaskCreationOptions.RunContinuationsAsynchronously);
        _ = manage.ListAsync().Returns([]);
        _ = manage.AddAsync(Arg.Any<TextExpansionEntry>()).Returns(addCompletion.Task);
        var vm = CreateManagedViewModel(manage);
        await vm.InitializationTask;
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
        _ = manage.ListAsync().Returns([existing]);
        _ = manage.AddAsync(Arg.Any<TextExpansionEntry>()).Returns<Task<TextExpansionEntry>>(_ =>
            Task.FromException<TextExpansionEntry>(new InvalidOperationException("duplicate")));
        var vm = CreateManagedViewModel(manage);
        await vm.InitializationTask;
        vm.TriggerInput = ":existing";
        vm.ReplacementInput = "replacement";

        var action = () => vm.AddExpansionCommand.ExecuteAsync(parameter: null);

        _ = await action.Should().ThrowAsync<InvalidOperationException>();
        _ = vm.Expansions.Should().ContainSingle().Which.Should().BeSameAs(existing);
        _ = await manage.Received(1).AddAsync(Arg.Is<TextExpansionEntry>(item => item.Trigger == ":existing"));
    }

    [Fact]
    public async Task ManagedExpansion_RemoveCallsPortBeforeCommittingCollection()
    {
        var expansion = new TextExpansionEntry(":remove", "value");
        var manage = Substitute.For<IManageTextExpansion>();
        _ = manage.ListAsync().Returns([expansion]);
        _ = manage.RemoveAsync(":remove").Returns(expansion);
        var vm = CreateManagedViewModel(manage);
        await vm.InitializationTask;
        _ = _dialogService.ShowConfirmationAsync(Arg.Any<string>(), Arg.Any<string>(), "Yes", "No")
            .Returns(Task.FromResult(true));

        await vm.RemoveExpansionCommand.ExecuteAsync(expansion);

        _ = vm.Expansions.Should().BeEmpty();
        _ = await manage.Received(1).RemoveAsync(":remove");
    }

    [Fact]
    public async Task ManagedExpansion_RemoveFailureLeavesCollectionUnchanged()
    {
        var expansion = new TextExpansionEntry(":remove", "value");
        var manage = Substitute.For<IManageTextExpansion>();
        _ = manage.ListAsync().Returns([expansion]);
        _ = manage.RemoveAsync(":remove").Returns<Task<TextExpansionEntry>>(_ =>
            Task.FromException<TextExpansionEntry>(new IOException("persistence failure")));
        var vm = CreateManagedViewModel(manage);
        await vm.InitializationTask;
        _ = _dialogService.ShowConfirmationAsync(Arg.Any<string>(), Arg.Any<string>(), "Yes", "No")
            .Returns(Task.FromResult(true));

        var action = () => vm.RemoveExpansionCommand.ExecuteAsync(expansion);

        _ = await action.Should().ThrowAsync<IOException>();
        _ = vm.Expansions.Should().ContainSingle().Which.Should().BeSameAs(expansion);
        _ = await manage.Received(1).RemoveAsync(":remove");
    }

    [Fact]
    public async Task ManagedExpansion_SetEnabledCallsPortAndCommitsReturnedState()
    {
        var expansion = new TextExpansionEntry(":toggle", "value", isEnabled: true);
        var updated = new TextExpansionEntry(":toggle", "value", isEnabled: false);
        var manage = Substitute.For<IManageTextExpansion>();
        _ = manage.ListAsync().Returns([expansion]);
        _ = manage.SetEnabledAsync(":toggle", enabled: false).Returns(updated);
        var vm = CreateManagedViewModel(manage);
        await vm.InitializationTask;
        expansion.IsEnabled = false;

        await vm.ToggleExpansionCommand.ExecuteAsync(expansion);

        _ = expansion.IsEnabled.Should().BeFalse();
        _ = await manage.Received(1).SetEnabledAsync(":toggle", enabled: false);
    }

    [Fact]
    public async Task ManagedExpansion_SetEnabledFailureRestoresPriorState()
    {
        var expansion = new TextExpansionEntry(":toggle", "value", isEnabled: true);
        var manage = Substitute.For<IManageTextExpansion>();
        _ = manage.ListAsync().Returns([expansion]);
        _ = manage.SetEnabledAsync(":toggle", enabled: false).Returns<Task<TextExpansionEntry>>(_ =>
            Task.FromException<TextExpansionEntry>(new IOException("persistence failure")));
        var vm = CreateManagedViewModel(manage);
        await vm.InitializationTask;
        expansion.IsEnabled = false;

        var action = () => vm.ToggleExpansionCommand.ExecuteAsync(expansion);

        _ = await action.Should().ThrowAsync<IOException>();
        _ = expansion.IsEnabled.Should().BeTrue();
        _ = await manage.Received(1).SetEnabledAsync(":toggle", enabled: false);
    }

    private TextExpansionViewModel CreateManagedViewModel(IManageTextExpansion manage)
    {
        return new TextExpansionViewModel(manage, _dialogService, _environmentInfoProvider, _localizationService);
    }

    [Fact]
    public void AddExpansion_CanExecute_ValidatesInput()
    {
        // Arrange
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
        // Arrange
        var expansion = new TextExpansionEntry(":del", "value");
        _viewModel.Expansions.Add(expansion);

        _ = _dialogService.ShowConfirmationAsync(Arg.Any<string>(), Arg.Any<string>(), "Yes", "No")
            .Returns(Task.FromResult(true));

        // Act
        await _viewModel.RemoveExpansionCommand.ExecuteAsync(expansion);

        // Assert
        _ = _viewModel.Expansions.Should().BeEmpty();
        await _storageService.Received(1).SaveAsync(Arg.Any<IEnumerable<TextExpansionEntry>>());
    }

    [Fact]
    public async Task RemoveExpansion_WhenCancelled_DoesNotRemove()
    {
        // Arrange
        var expansion = new TextExpansionEntry(":keep", "value");
        _viewModel.Expansions.Add(expansion);

        _ = _dialogService.ShowConfirmationAsync(Arg.Any<string>(), Arg.Any<string>(), "Yes", "No")
            .Returns(Task.FromResult(false));

        // Act
        await _viewModel.RemoveExpansionCommand.ExecuteAsync(expansion);

        // Assert
        _ = _viewModel.Expansions.Should().HaveCount(1);
        await _storageService.DidNotReceive().SaveAsync(Arg.Any<IEnumerable<TextExpansionEntry>>());
    }

    [Fact]
    public async Task ToggleExpansion_SavesStart()
    {
        // Arrange
        var expansion = new TextExpansionEntry(":toggle", "val");
        _viewModel.Expansions.Add(expansion);

        // Act
        await _viewModel.ToggleExpansionCommand.ExecuteAsync(expansion);

        // Assert
        // We just verify it saves the current state
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
        // Arrange
        var envProvider = Substitute.For<IEnvironmentInfoProvider>();
        _ = envProvider.CurrentEnvironment.Returns(environment);
        var vm = new TextExpansionViewModel(_storageService, _dialogService, envProvider, _localizationService);

        // Assert
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
        // Arrange
        var envProvider = Substitute.For<IEnvironmentInfoProvider>();
        _ = envProvider.CurrentEnvironment.Returns(DisplayEnvironment.LinuxX11);
        var vm = new TextExpansionViewModel(_storageService, _dialogService, envProvider, _localizationService);

        // Assert
        _ = vm.IsPasteMethodSelectorVisible.Should().BeTrue();

        // Act
        vm.SelectedInsertionMode = TextInsertionMode.DirectTyping;

        // Assert
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
        // Arrange
        _viewModel.SelectedPasteMethod = PasteMethod.ShiftInsert;
        _viewModel.TriggerInput = ":x";
        _viewModel.ReplacementInput = "value";

        // Act
        await _viewModel.AddExpansionCommand.ExecuteAsync(parameter: null);

        // Assert
        _ = _viewModel.SelectedPasteMethod.Should().Be(PasteMethod.CtrlV);
    }

    [Fact]
    public async Task AddExpansion_WhenDirectTypingSelected_PersistsInsertionModeAndResetsToDefault()
    {
        // Arrange
        _viewModel.SelectedInsertionMode = TextInsertionMode.DirectTyping;
        _viewModel.SelectedDirectTypingMethod = DirectTypingMethod.CompatibleKeyByKey;
        _viewModel.TriggerInput = ":typed";
        _viewModel.ReplacementInput = "value";

        // Act
        await _viewModel.AddExpansionCommand.ExecuteAsync(parameter: null);

        // Assert
        _ = _viewModel.Expansions.Should().ContainSingle();
        _ = _viewModel.Expansions[0].InsertionMode.Should().Be(TextInsertionMode.DirectTyping);
        _ = _viewModel.Expansions[0].DirectTypingMethod.Should().Be(DirectTypingMethod.CompatibleKeyByKey);
        _ = _viewModel.SelectedInsertionMode.Should().Be(TextInsertionMode.Paste);
        _ = _viewModel.SelectedDirectTypingMethod.Should().Be(DirectTypingMethod.FastBatch);
    }

    [Fact]
    public async Task ToggleExpansion_WhenExpansionIsNull_DoesNotSave()
    {
        // Act
        await _viewModel.ToggleExpansionCommand.ExecuteAsync(parameter: null);

        // Assert
        await _storageService.DidNotReceive().SaveAsync(Arg.Any<IEnumerable<TextExpansionEntry>>());
    }

    [Fact]
    public void CultureChanged_RaisesLocalizedProperties()
    {
        var localizationService = Substitute.For<ILocalizationService>();
        _ = localizationService.CurrentCulture.Returns(System.Globalization.CultureInfo.GetCultureInfo("en"));
        _ = localizationService["TextExpansion_Items"].Returns("{0} items");
        var vm = new TextExpansionViewModel(_storageService, _dialogService, _environmentInfoProvider, localizationService);
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

    [Theory]
    [InlineData("en", TextInsertionMode.Paste, "Paste")]
    [InlineData("en", TextInsertionMode.DirectTyping, "Direct Typing")]
    [InlineData("tr", TextInsertionMode.Paste, "Yapıştır")]
    [InlineData("tr", TextInsertionMode.DirectTyping, "Doğrudan Yazma")]
    [InlineData("fr", TextInsertionMode.Paste, "Coller")]
    [InlineData("fr", TextInsertionMode.DirectTyping, "Saisie directe")]
    public void InsertionModeDisplayText_ReturnsExpectedLocalizedLabel(
        string cultureName,
        TextInsertionMode mode,
        string expected)
    {
        using var cultureScope = new LocalizationCultureScope(cultureName);

        var result = TextExpansionConverters.InsertionModeDisplayText.Convert(
            mode,
            typeof(string),
parameter: null,
            CultureInfo.InvariantCulture);

        _ = result.Should().Be(expected);
    }

    [Theory]
    [InlineData(PasteMethod.CtrlV, "Ctrl+V")]
    [InlineData(PasteMethod.CtrlShiftV, "Ctrl+Shift+V")]
    [InlineData(PasteMethod.ShiftInsert, "Shift+Insert")]
    public void PasteMethodDisplayText_ReturnsExpectedLabel(PasteMethod method, string expected)
    {
        var result = TextExpansionConverters.PasteMethodDisplayText.Convert(
            method,
            typeof(string),
parameter: null,
            CultureInfo.InvariantCulture);

        _ = result.Should().Be(expected);
    }

    [Theory]
    [InlineData(DirectTypingMethod.FastBatch, "Fast (batched)")]
    [InlineData(DirectTypingMethod.CompatibleKeyByKey, "Compatible (key-by-key)")]
    public void DirectTypingMethodDisplayText_ReturnsExpectedLabel(DirectTypingMethod method, string expected)
    {
        var result = TextExpansionConverters.DirectTypingMethodDisplayText.Convert(
            method,
            typeof(string),
parameter: null,
            CultureInfo.InvariantCulture);

        _ = result.Should().Be(expected);
    }

    [Theory]
    [InlineData(TextInsertionMode.Paste, true)]
    [InlineData(TextInsertionMode.DirectTyping, false)]
    public void IsPasteMode_ReturnsWhetherModeUsesClipboard(TextInsertionMode mode, bool expected)
    {
        var result = TextExpansionConverters.IsPasteMode.Convert(
            mode,
            typeof(bool),
parameter: null,
            CultureInfo.InvariantCulture);

        _ = result.Should().Be(expected);
    }

    [Theory]
    [InlineData(TextInsertionMode.Paste, false)]
    [InlineData(TextInsertionMode.DirectTyping, true)]
    public void IsDirectTypingMode_ReturnsWhetherModeUsesDirectTyping(TextInsertionMode mode, bool expected)
    {
        var result = TextExpansionConverters.IsDirectTypingMode.Convert(
            mode,
            typeof(bool),
parameter: null,
            CultureInfo.InvariantCulture);

        _ = result.Should().Be(expected);
    }
}
