namespace CrossMacro.UI.Tests.Views.Tabs;

public sealed class EditorScreenReadingFieldsTests
{
    [Fact]
    public void ImageSearchSection_BindsRegionCaptureControls()
    {
        var xaml = ReadRepoFile("src/CrossMacro.UI/Views/Tabs/EditorScreenReadingFields.axaml");
        const string marker = "<!-- Image search -->";

        var markerIndex = xaml.IndexOf(marker, StringComparison.Ordinal);
        Assert.True(markerIndex >= 0, "Image search section should exist in screen reading fields XAML.");

        var section = xaml[markerIndex..];
        var widthIndex = section.IndexOf("SelectedAction.ScreenWidth", StringComparison.Ordinal);
        var heightIndex = section.IndexOf("SelectedAction.ScreenHeight", StringComparison.Ordinal);
        var assetIndex = section.IndexOf("SelectedAction.ImageAssetName", StringComparison.Ordinal);
        var previewIndex = section.IndexOf("SelectedImageAssetPreview", StringComparison.Ordinal);
        var topLeftIndex = section.IndexOf("Command=\"{Binding CapturePixelSearchTopLeftAsync}\"", StringComparison.Ordinal);
        var bottomRightIndex = section.IndexOf("Command=\"{Binding CapturePixelSearchBottomRightAsync}\"", StringComparison.Ordinal);

        Assert.True(widthIndex >= 0, "Image search section should bind region width.");
        Assert.True(heightIndex > widthIndex, "Image search height should follow width.");
        Assert.True(previewIndex > assetIndex, "Image preview should follow the selected asset.");
        Assert.Contains("ShowSelectedImageAssetPreview", section, StringComparison.Ordinal);
        Assert.Contains("ItemsSource=\"{Binding ImageMatchModes}\"", section, StringComparison.Ordinal);
        Assert.Contains("SelectedItem=\"{Binding SelectedImageSearchMatchMode, Mode=TwoWay}\"", section, StringComparison.Ordinal);
        Assert.Contains("HorizontalAlignment=\"Stretch\"", GetOpeningTag(section, "<ComboBox ItemsSource=\"{Binding ImageMatchModes}\""), StringComparison.Ordinal);
        Assert.Contains("Editor_ImageAssetPreview", section, StringComparison.Ordinal);
        Assert.Contains("Stretch=\"Uniform\"", section, StringComparison.Ordinal);
        Assert.Contains("MaxWidth=\"640\"", section, StringComparison.Ordinal);
        Assert.Contains("MaxHeight=\"360\"", section, StringComparison.Ordinal);
        Assert.True(topLeftIndex > heightIndex, "Top-left capture controls should follow image region fields.");
        Assert.True(bottomRightIndex > topLeftIndex, "Bottom-right capture controls should follow top-left controls.");
        Assert.Contains("IsVisible=\"{Binding !IsCapturingPixelSearchTopLeft}\"", section, StringComparison.Ordinal);
        Assert.Contains("IsVisible=\"{Binding IsCapturingPixelSearchTopLeft}\"", section, StringComparison.Ordinal);
        Assert.Contains("IsVisible=\"{Binding !IsCapturingPixelSearchBottomRight}\"", section, StringComparison.Ordinal);
        Assert.Contains("IsVisible=\"{Binding IsCapturingPixelSearchBottomRight}\"", section, StringComparison.Ordinal);
        Assert.Equal(2, CountOccurrences(section, "Command=\"{Binding CancelCapture}\""));
        Assert.Equal(1, CountOccurrences(section, "Editor_CaptureRegionTopLeft"));
        Assert.Equal(1, CountOccurrences(section, "Editor_CaptureRegionBottomRight"));
        Assert.Equal(2, CountOccurrences(section, "Editor_CancelCapture"));
        Assert.Contains("IsVisible=\"{Binding ShowImageWaitTimeoutField}\"", section, StringComparison.Ordinal);
    }

    [Fact]
    public void PixelColorSection_DoesNotExposeTimeout()
    {
        var xaml = ReadRepoFile("src/CrossMacro.UI/Views/Tabs/EditorScreenReadingFields.axaml");
        const string start = "<!-- Pixel color -->";
        const string end = "<!-- Wait color -->";
        var sectionStart = xaml.IndexOf(start, StringComparison.Ordinal);
        var sectionEnd = xaml.IndexOf(end, StringComparison.Ordinal);

        Assert.True(sectionStart >= 0 && sectionEnd > sectionStart, "Pixel color section should be bounded by the wait color section.");
        Assert.DoesNotContain("SelectedAction.ScreenTimeoutMs", xaml[sectionStart..sectionEnd], StringComparison.Ordinal);
    }

    private static int CountOccurrences(string value, string search)
    {
        var count = 0;
        var index = 0;
        while ((index = value.IndexOf(search, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += search.Length;
        }

        return count;
    }

    private static string GetOpeningTag(string value, string startMarker)
    {
        var start = value.IndexOf(startMarker, StringComparison.Ordinal);
        Assert.True(start >= 0, $"Could not find XAML opening tag starting with {startMarker}.");
        var end = value.IndexOf('>', start);
        Assert.True(end > start, "XAML opening tag should be complete.");
        return value[start..(end + 1)];
    }

    private static string ReadRepoFile(string relativePath)
    {
        return File.ReadAllText(Path.Combine(FindRepoRoot(), relativePath));
    }

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "CrossMacro.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate repository root from test output directory.");
    }
}
