
namespace CrossMacro.Platform.Linux.Tests.Services.Keyboard;

public sealed class XkbLayoutNameResolverTests
{
    [Fact]
    public void TryResolveLayoutCode_ReturnsNormalizedLayoutCode_ForTwoLetterName()
    {
        var resolver = new XkbLayoutNameResolver([]);

        var layout = resolver.TryResolveLayoutCode("TR");

        Assert.Equal("tr", layout);
    }

    [Fact]
    public void TryResolveLayoutCode_ReturnsKnownLayoutCode_ForCommonDescription()
    {
        var resolver = new XkbLayoutNameResolver([]);

        var layout = resolver.TryResolveLayoutCode("English (US)");

        Assert.Equal("us", layout);
    }

    [Fact]
    public void TryResolveFromRulesFile_ReturnsLayoutCode_ForVariantDescription()
    {
        var rulesPath = Path.Combine(Path.GetTempPath(), $"crossmacro-xkb-rules-{Guid.NewGuid():N}.xml");
        File.WriteAllText(
            rulesPath,
            "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n"
          + "<xkbConfigRegistry>\n"
          + "  <layoutList>\n"
          + "    <layout>\n"
          + "      <configItem>\n"
          + "        <name>us</name>\n"
          + "        <description>English (US)</description>\n"
          + "      </configItem>\n"
          + "      <variantList>\n"
          + "        <variant>\n"
          + "          <configItem>\n"
          + "            <name>intl</name>\n"
          + "            <description>English (US, intl., with dead keys)</description>\n"
          + "          </configItem>\n"
          + "        </variant>\n"
          + "      </variantList>\n"
          + "    </layout>\n"
          + "  </layoutList>\n"
          + "</xkbConfigRegistry>" + '\n');

        try
        {
            var layout = XkbLayoutNameResolver.TryResolveFromRulesFile(
                "English (US, intl., with dead keys)",
                rulesPath);

            Assert.Equal("us", layout);
        }
        finally
        {
            File.Delete(rulesPath);
        }
    }
}
