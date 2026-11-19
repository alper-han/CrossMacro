namespace CrossMacro.Platform.Linux.Tests.DisplayServer.Wayland.DBus;

public sealed class DBusVariantUnboxerTests
{
    [Fact]
    public void Unbox_PreservesPrimitiveTypes()
    {
        Assert.Equal(7U, DBusVariantUnboxer.Unbox(VariantValue.UInt32(7)));
        Assert.Equal(-3, DBusVariantUnboxer.Unbox(VariantValue.Int32(-3)));
        Assert.Equal(-9_000_000_000L, DBusVariantUnboxer.Unbox(VariantValue.Int64(-9_000_000_000L)));
        Assert.Equal("text", DBusVariantUnboxer.Unbox(VariantValue.String("text")));
        Assert.True(Assert.IsType<bool>(DBusVariantUnboxer.Unbox(VariantValue.Bool(value: true))));
    }

    [Fact]
    public void Unbox_UnwrapsNestedVariants()
    {
        var nested = VariantValue.Variant(VariantValue.Variant(VariantValue.UInt32(5)));

        Assert.Equal(5U, DBusVariantUnboxer.Unbox(nested));
    }

    [Fact]
    public void Unbox_ConvertsTwoFieldStructsToValueTuples()
    {
        var position = (VariantValue)new Struct<int, int>(-1920, 40);

        var unboxed = DBusVariantUnboxer.Unbox(position);

        Assert.Equal(((object)-1920, (object)40), Assert.IsType<ValueTuple<object, object>>(unboxed));
    }
}
