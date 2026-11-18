using CrossMacro.UI.Controls;
using CrossMacro.UI.Views.Tabs;

namespace CrossMacro.UI.Tests.Views.Tabs;

[Collection(TestCollections.HeadlessStartup)]
public sealed class ShortcutTabViewTests
{
    [Fact]
    public Task HotkeyValidation_FollowsTheCurrentDataContext() =>
        HeadlessTestRunner.RunAsync(async token =>
        {
            var localization = Substitute.For<ILocalizationService>();
            _ = localization.CurrentCulture.Returns(CultureInfo.InvariantCulture);
            _ = localization[Arg.Any<string>()].Returns(call => call.Arg<string>() + " {0}");
            var runtime = Substitute.For<IShortcutService>();
            _ = runtime.Tasks.Returns(new ObservableCollection<ShortcutTask>());
            using var viewModel = new ShortcutViewModel(
                Substitute.For<IManageShortcut>(),
                runtime,
                Substitute.For<IDialogService>(),
                Substitute.For<IGlobalHotkeyService>(),
                localization,
                new AcceptingShortcutHotkeyNormalizer(),
                uiDispatcher: new SerializedUiDispatcher());
            var view = new ShortcutTabView();
            var window = new Window { Content = view };
            window.Show();

            try
            {
                await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() => { });
                var capture = view.FindControl<HotkeyCapture>("HotkeyInput");
                Assert.NotNull(capture);
                Assert.Null(capture.ValidationFunc);

                view.DataContext = viewModel;
                Assert.NotNull(capture.ValidationFunc);

                view.DataContext = null;
                Assert.Null(capture.ValidationFunc);
            }
            finally
            {
                window.Close();
            }
        });
}
