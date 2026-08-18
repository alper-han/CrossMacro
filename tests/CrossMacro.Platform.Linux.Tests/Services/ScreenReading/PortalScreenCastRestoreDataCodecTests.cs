namespace CrossMacro.Platform.Linux.Tests.Services.ScreenReading;

public sealed class PortalScreenCastRestoreDataCodecTests
{
    [Fact]
    public void RoundTrip_PreservesWlrDictionaryRestoreData()
    {
        var payload = new Dict<string, VariantValue>();
        payload.Add("output_name", VariantValue.Variant(VariantValue.String("DP-1")));
        var value = (VariantValue)new Struct<string, uint, VariantValue>("wlroots", 1, VariantValue.Variant(payload));

        var serialized = PortalScreenCastRestoreDataCodec.TrySerialize(value);

        Assert.True(PortalScreenCastRestoreDataCodec.TryDeserialize(serialized, out var restored));
        Assert.Equal(VariantValueType.Struct, restored.Type);
        Assert.Equal("wlroots", restored.GetItem(0).GetString());
        Assert.Equal(1U, restored.GetItem(1).GetUInt32());
        var restoredPayload = restored.GetItem(2).GetVariantValue();
        Assert.Equal(VariantValueType.Dictionary, restoredPayload.Type);
        Assert.Equal("output_name", restoredPayload.GetDictionaryEntry(0).Key.GetString());
        Assert.Equal("DP-1", restoredPayload.GetDictionaryEntry(0).Value.GetString());
    }

    [Fact]
    public void RoundTrip_PreservesGnomeStreamArrayRestoreData()
    {
        var streams = new Array<Struct<uint, uint, VariantValue>>();
        streams.Add(new Struct<uint, uint, VariantValue>(7, 1, VariantValue.Variant(VariantValue.String("HDMI-A-1"))));
        var payload = VariantValue.Struct(
            VariantValue.Int64(10),
            VariantValue.Int64(20),
            streams);
        var value = (VariantValue)new Struct<string, uint, VariantValue>("GNOME", 1, VariantValue.Variant(payload));

        var serialized = PortalScreenCastRestoreDataCodec.TrySerialize(value);

        Assert.True(PortalScreenCastRestoreDataCodec.TryDeserialize(serialized, out var restored));
        var restoredStreams = restored.GetItem(2).GetVariantValue().GetItem(2);
        Assert.Equal(VariantValueType.Array, restoredStreams.Type);
        Assert.Equal(VariantValueType.Struct, restoredStreams.ItemType);
        Assert.Equal(7U, restoredStreams.GetItem(0).GetItem(0).GetUInt32());
        Assert.Equal("HDMI-A-1", restoredStreams.GetItem(0).GetItem(2).GetVariantValue().GetString());
    }

    [Fact]
    public void BuildSelectSourcesOptions_WhenLegacyRestoreDataIsValid_AddsTypedRestoreData()
    {
        var payload = (VariantValue)new Struct<Array<string>, Array<string>>(
            new Array<string>(["DP-1"]),
            new Array<string>());
        var value = (VariantValue)new Struct<string, uint, VariantValue>("COSMIC", 1, VariantValue.Variant(payload));
        var serialized = PortalScreenCastRestoreDataCodec.TrySerialize(value);

        var options = PortalScreenCastClient.BuildSelectSourcesOptions("handle", null, serialized);

        Assert.Equal(VariantValueType.Struct, options["restore_data"].Type);
        Assert.Equal("COSMIC", options["restore_data"].GetItem(0).GetString());
    }

    [Fact]
    public void RoundTrip_PreservesHyprlandV2RestoreData()
    {
        var payload = (VariantValue)new Struct<string, uint, string, bool, ulong>(
            "session-token",
            12,
            "DP-1",
            false,
            42);
        var value = (VariantValue)new Struct<string, uint, VariantValue>("hyprland", 2, VariantValue.Variant(payload));

        var serialized = PortalScreenCastRestoreDataCodec.TrySerialize(value);

        Assert.True(PortalScreenCastRestoreDataCodec.TryDeserialize(serialized, out var restored));
        Assert.True(PortalScreenCastRestoreDataCodec.IsSupportedEnvelope(restored));
        Assert.Equal("hyprland", restored.GetItem(0).GetString());
        Assert.Equal(2U, restored.GetItem(1).GetUInt32());
        Assert.Equal("DP-1", restored.GetItem(2).GetVariantValue().GetItem(2).GetString());
    }

    [Fact]
    public void RoundTrip_PreservesKdeDictionaryRestoreData()
    {
        var payload = new Dict<string, VariantValue>();
        payload.Add("outputs", VariantValue.Array(new[] { "DP-1", "HDMI-1" }));
        payload.Add("windows", VariantValue.Array(Array.Empty<string>()));
        var value = (VariantValue)new Struct<string, uint, VariantValue>("KDE", 1, VariantValue.Variant(payload));

        var serialized = PortalScreenCastRestoreDataCodec.TrySerialize(value);

        Assert.True(PortalScreenCastRestoreDataCodec.TryDeserialize(serialized, out var restored));
        Assert.True(PortalScreenCastRestoreDataCodec.IsSupportedEnvelope(restored));
        Assert.Equal("KDE", restored.GetItem(0).GetString());
        Assert.Equal(2, restored.GetItem(2).GetVariantValue().Count);
    }

    [Fact]
    public void TryGetResponseRestoreData_WhenValueIsUnsupported_ReturnsNull()
    {
        var results = new Dictionary<string, VariantValue>(StringComparer.Ordinal)
        {
            ["restore_data"] = VariantValue.UnixFd(new SafeFileHandle(new IntPtr(-1), ownsHandle: false)),
        };

        Assert.Null(PortalScreenCastClient.TryGetResponseRestoreData(results));
    }

    [Fact]
    public void BuildSelectSourcesOptions_WhenIssuerIsUnknown_DoesNotSendRestoreData()
    {
        var value = (VariantValue)new Struct<string, uint, VariantValue>("unknown", 1, VariantValue.Variant(VariantValue.String("payload")));
        var serialized = PortalScreenCastRestoreDataCodec.TrySerialize(value);

        var options = PortalScreenCastClient.BuildSelectSourcesOptions("handle", null, serialized);

        Assert.False(options.ContainsKey("restore_data"));
    }
}
