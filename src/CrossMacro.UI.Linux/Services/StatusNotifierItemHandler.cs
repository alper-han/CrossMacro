using System;
using System.Threading.Tasks;
using Tmds.DBus.Protocol;
using static CrossMacro.UI.Linux.Services.LinuxStatusNotifierTrayHandle;

namespace CrossMacro.UI.Linux.Services;

/// <summary>The org.kde.StatusNotifierItem interface: static item identity, icon and tooltip properties and the activation methods.</summary>
internal sealed class StatusNotifierItemHandler(LinuxStatusNotifierTrayHandle owner) : DBusInterfaceHandler(SniObjectPath, SniInterface)
{
    private static readonly string[] Properties = ["Category", "Id", "Title", "Status", "WindowId", "IconThemePath", "IconName", "IconPixmap", "OverlayIconName", "OverlayIconPixmap", "AttentionIconName", "AttentionIconPixmap", "AttentionMovieName", "ToolTip", "ItemIsMenu", "Menu"];
    private static readonly byte[] Xml =
        "<interface name=\"org.kde.StatusNotifierItem\"><property name=\"Category\" type=\"s\" access=\"read\"/><property name=\"Id\" type=\"s\" access=\"read\"/><property name=\"Title\" type=\"s\" access=\"read\"/><property name=\"Status\" type=\"s\" access=\"read\"/><property name=\"WindowId\" type=\"u\" access=\"read\"/><property name=\"IconThemePath\" type=\"s\" access=\"read\"/><property name=\"IconName\" type=\"s\" access=\"read\"/><property name=\"IconPixmap\" type=\"a(iiay)\" access=\"read\"/><property name=\"OverlayIconName\" type=\"s\" access=\"read\"/><property name=\"OverlayIconPixmap\" type=\"a(iiay)\" access=\"read\"/><property name=\"AttentionIconName\" type=\"s\" access=\"read\"/><property name=\"AttentionIconPixmap\" type=\"a(iiay)\" access=\"read\"/><property name=\"AttentionMovieName\" type=\"s\" access=\"read\"/><property name=\"ToolTip\" type=\"(sa(iiay)ss)\" access=\"read\"/><property name=\"ItemIsMenu\" type=\"b\" access=\"read\"/><property name=\"Menu\" type=\"o\" access=\"read\"/><method name=\"Activate\"><arg direction=\"in\" type=\"i\"/><arg direction=\"in\" type=\"i\"/></method><method name=\"SecondaryActivate\"><arg direction=\"in\" type=\"i\"/><arg direction=\"in\" type=\"i\"/></method><method name=\"ContextMenu\"><arg direction=\"in\" type=\"i\"/><arg direction=\"in\" type=\"i\"/></method><method name=\"Scroll\"><arg direction=\"in\" type=\"i\"/><arg direction=\"in\" type=\"s\"/></method><signal name=\"NewTitle\"/><signal name=\"NewToolTip\"/><signal name=\"NewStatus\"><arg type=\"s\"/></signal></interface>"u8.ToArray();
    protected override ReadOnlyMemory<byte> InterfaceXml => Xml;
    protected override string[] PropertyNames => Properties;

    protected override void WriteProperty(ref MessageWriter writer, string property)
    {
        switch (property)
        {
            case "Category": writer.WriteVariantString("ApplicationStatus"); break;
            case "Id": writer.WriteVariantString("CrossMacro"); break;
            case "Title": writer.WriteVariantString(owner.ToolTipText); break;
            case "Status": writer.WriteVariantString(owner.IsVisible ? "Active" : "Passive"); break;
            case "WindowId": writer.WriteVariantUInt32(0); break;
            case "ItemIsMenu": writer.WriteVariantBool(value: false); break;
            case "Menu": writer.WriteVariantObjectPath(MenuObjectPath); break;
            case "IconPixmap":
                writer.WriteSignature("a(iiay)");
                owner.WritePixmaps(ref writer);
                break;
            case "OverlayIconPixmap":
            case "AttentionIconPixmap":
                writer.WriteSignature("a(iiay)");
                var empty = writer.WriteArrayStart(DBusType.Struct);
                writer.WriteArrayEnd(empty);
                break;
            case "ToolTip":
                writer.WriteSignature("(sa(iiay)ss)");
                writer.WriteStructureStart();
                writer.WriteString(string.Empty);
                owner.WritePixmaps(ref writer);
                writer.WriteString(owner.ToolTipText);
                writer.WriteString(string.Empty);
                break;
            default: writer.WriteVariantString(string.Empty); break;
        }
    }

    protected override ValueTask DispatchAsync(MethodContext context)
    {
        var member = context.Request.MemberAsString;
        if (member is not ("Activate" or "SecondaryActivate" or "ContextMenu" or "Scroll"))
        {
            context.ReplyUnknownMethodError();
            return ValueTask.CompletedTask;
        }
        if (CheckSignature(context, member is "Scroll" ? "is" : "ii"))
        {
            if (member is "Activate") { owner.Activate(); }
            ReplyEmpty(context);
        }
        return ValueTask.CompletedTask;
    }
}
