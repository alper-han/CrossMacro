using Avalonia.Controls;
using Avalonia.VisualTree;
using CrossMacro.UI.Views.Tabs;

namespace CrossMacro.UI.Tests.Views;

[Collection(TestCollections.HeadlessStartup)]
public sealed class EditorDocumentViewTests
{
    [Fact]
    public Task MovementCondensationThreshold_IsVisibleBeforePreviewIsEnabled() =>
        HeadlessTestRunner.RunAsync(_ =>
        {
            using var viewModel = CreateViewModel();
            viewModel.Actions.Add(AbsoluteMove(100, 100));
            viewModel.Actions.Add(FixedDelay(12_000));
            viewModel.Actions.Add(AbsoluteMove(120, 120));

            viewModel.MovementCondensationThresholdMilliseconds.Should().Be(10);
            viewModel.CanSimplifyMovement.Should().BeFalse();
            viewModel.ShowSimplifyMovementToggle.Should().BeFalse();

            var view = new EditorDocumentView { DataContext = viewModel };
            var window = new Window { Content = view };
            window.Show();
            try
            {
                var thresholdInput = view.GetVisualDescendants()
                    .OfType<NumericUpDown>()
                    .Should()
                    .ContainSingle()
                    .Which;

                thresholdInput.IsEffectivelyVisible.Should().BeTrue();
                thresholdInput.Value.Should().Be(10m);
                thresholdInput.Bounds.Width.Should().BeGreaterThan(100);
                thresholdInput.Value = 20m;

                viewModel.MovementCondensationThresholdMilliseconds.Should().Be(20);
                viewModel.ShowSimplifyMovementToggle.Should().BeTrue();
            }
            finally
            {
                window.Close();
            }

            return Task.CompletedTask;
        });

    [Fact]
    public Task MovementCleanupControls_HideWhenEmptyAndKeepFiltersForLaterActions() =>
        HeadlessTestRunner.RunAsync(_ =>
        {
            using var viewModel = CreateViewModel();
            viewModel.HideMouseMoves = true;
            viewModel.HideShortWaits = true;
            viewModel.SimplifyMovement = true;
            viewModel.MovementCondensationThresholdMilliseconds = 25;

            var view = new EditorDocumentView { DataContext = viewModel };
            var window = new Window { Content = view };
            window.Show();
            try
            {
                var thresholdInput = view.GetVisualDescendants()
                    .OfType<NumericUpDown>()
                    .Should()
                    .ContainSingle()
                    .Subject;

                thresholdInput.IsEffectivelyVisible.Should().BeFalse();
                viewModel.HideMouseMoves.Should().BeTrue();
                viewModel.HideShortWaits.Should().BeTrue();
                viewModel.SimplifyMovement.Should().BeTrue();
                viewModel.MovementCondensationThresholdMilliseconds.Should().Be(25);

                viewModel.Actions.Add(AbsoluteMove(100, 100));
                viewModel.Actions.Add(AbsoluteMove(120, 120));

                viewModel.ActionListItems.Should().BeEmpty();
                thresholdInput.IsEffectivelyVisible.Should().BeTrue();
                thresholdInput.Value.Should().Be(25m);
                viewModel.HideMouseMoves.Should().BeTrue();
                viewModel.HideShortWaits.Should().BeTrue();
                viewModel.SimplifyMovement.Should().BeTrue();
            }
            finally
            {
                window.Close();
            }
            return Task.CompletedTask;
        });

    private static EditorViewModel CreateViewModel()
    {
        var localization = TestLocalization.Bracketed();
        var imageAssetCodec = new ImageAssetCodec();
        return new EditorViewModel(
            Substitute.For<IEditorActionConverter>(),
            Substitute.For<IEditorActionValidator>(),
            Substitute.For<ICoordinateCaptureService>(),
            Substitute.For<IMacroFileManager>(),
            Substitute.For<IDialogService>(),
            Substitute.For<IKeyCodeMapper>(),
            Substitute.For<IMacroPlayer>(),
            localization,
            new EditorActionDisplayFormatter(localization),
            Substitute.For<IScreenPixelReader>(),
            imageAssetCodec,
            new ImageAssetPreviewDecoder(imageAssetCodec),
            uiDispatcher: ImmediateUiDispatcher.Instance);
    }

    private static EditorAction AbsoluteMove(int x, int y) => new()
    {
        Type = EditorActionType.MouseMove,
        X = x,
        Y = y,
        IsAbsolute = true,
    };

    private static EditorAction FixedDelay(long microseconds) => new()
    {
        Type = EditorActionType.Delay,
        DelayMicroseconds = microseconds,
    };
}
