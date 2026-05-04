using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Media;

namespace CrossMacro.UI.Icons;

public static class AppIcons
{
    private static readonly IReadOnlyDictionary<AppIcon, Lazy<Geometry>> Geometries = Enum.GetValues<AppIcon>()
        .ToDictionary(icon => icon, icon => new Lazy<Geometry>(() => StreamGeometry.Parse(GetPath(icon))));

    public static Geometry Get(AppIcon icon)
    {
        if (!Geometries.TryGetValue(icon, out var geometry))
        {
            throw new ArgumentOutOfRangeException(nameof(icon), icon, "Unknown application icon.");
        }

        return geometry.Value;
    }

    public static string GetPath(AppIcon icon)
    {
        return icon switch
        {
            AppIcon.Add => Add,
            AppIcon.ArrowDown => ArrowDown,
            AppIcon.ArrowNorthEast => ArrowNorthEast,
            AppIcon.ArrowRight => ArrowRight,
            AppIcon.ArrowUp => ArrowUp,
            AppIcon.Calendar => Calendar,
            AppIcon.Cancel => Cancel,
            AppIcon.Check => Check,
            AppIcon.Clock => Clock,
            AppIcon.Close => Close,
            AppIcon.Clipboard => Clipboard,
            AppIcon.Delete => Delete,
            AppIcon.Edit => Edit,
            AppIcon.EditNote => EditNote,
            AppIcon.FolderOpen => FolderOpen,
            AppIcon.GitHub => GitHub,
            AppIcon.Info => Info,
            AppIcon.Keyboard => Keyboard,
            AppIcon.Location => Location,
            AppIcon.Menu => Menu,
            AppIcon.Minus => Minus,
            AppIcon.Mouse => Mouse,
            AppIcon.Play => Play,
            AppIcon.Record => Record,
            AppIcon.Redo => Redo,
            AppIcon.Save => Save,
            AppIcon.Settings => Settings,
            AppIcon.Stop => Stop,
            AppIcon.Success => Success,
            AppIcon.Timer => Timer,
            AppIcon.Tip => Tip,
            AppIcon.Tools => Tools,
            AppIcon.Undo => Undo,
            AppIcon.Warning => Warning,
            _ => throw new ArgumentOutOfRangeException(nameof(icon), icon, "Unknown application icon.")
        };
    }

    private const string Add = "M19,13H13V19H11V13H5V11H11V5H13V11H19V13Z";
    private const string ArrowDown = "M4,12L5.41,10.59L11,16.17V4H13V16.17L18.59,10.58L20,12L12,20L4,12Z";
    private const string ArrowNorthEast = "M7,7H17V17H15V10.41L6.41,19L5,17.59L13.59,9H7V7Z";
    private const string ArrowRight = "M12,4L10.59,5.41L16.17,11H4V13H16.17L10.58,18.59L12,20L20,12L12,4Z";
    private const string ArrowUp = "M4,12L12,4L20,12L18.59,13.41L13,7.83V20H11V7.83L5.41,13.42L4,12Z";
    private const string Calendar = "M7,2V4H5C3.89,4 3,4.9 3,6V20C3,21.1 3.89,22 5,22H19C20.1,22 21,21.1 21,20V6C21,4.9 20.1,4 19,4H17V2H15V4H9V2H7M5,9H19V20H5V9Z";
    private const string Cancel = "M12,2A10,10 0,1 0,12,22A10,10 0,1 0,12,2M15.59,7L17,8.41L13.41,12L17,15.59L15.59,17L12,13.41L8.41,17L7,15.59L10.59,12L7,8.41L8.41,7L12,10.59L15.59,7Z";
    private const string Check = "M9,16.17L4.83,12L3.41,13.41L9,19L21,7L19.59,5.59L9,16.17Z";
    private const string Clock = "M12,20A8,8 0,1 0,12,4A8,8 0,1 0,12,20M12,2A10,10 0,1 1,12,22A10,10 0,1 1,12,2M12.5,7H11V13L16.2,16.2L17,14.9L12.5,12.2V7Z";
    private const string Close = "M18.3,5.71L16.89,4.29L12,9.17L7.11,4.29L5.7,5.71L10.59,10.59L5.7,15.48L7.11,16.89L12,12L16.89,16.89L18.3,15.48L13.41,10.59L18.3,5.71Z";
    private const string Clipboard = "M19,3H14.82C14.4,1.84 13.3,1 12,1C10.7,1 9.6,1.84 9.18,3H5C3.9,3 3,3.9 3,5V19C3,20.1 3.9,21 5,21H19C20.1,21 21,20.1 21,19V5C21,3.9 20.1,3 19,3M12,3A1,1 0,1 1,12,5A1,1 0,1 1,12,3M19,19H5V5H7V8H17V5H19V19Z";
    private const string Delete = "M9,3V4H4V6H5V19C5,20.1 5.9,21 7,21H17C18.1,21 19,20.1 19,19V6H20V4H15V3H9M7,6H17V19H7V6M9,8V17H11V8H9M13,8V17H15V8H13Z";
    private const string Edit = "M3,17.25V21H6.75L17.81,9.94L14.06,6.19L3,17.25M20.71,7.04C21.1,6.65 21.1,6.02 20.71,5.63L18.37,3.29C17.98,2.9 17.35,2.9 16.96,3.29L15.13,5.12L18.88,8.87L20.71,7.04Z";
    private const string EditNote = "M3,17.25V21H6.75L17.81,9.94L14.06,6.19L3,17.25M20.71,7.04C21.1,6.65 21.1,6.02 20.71,5.63L18.37,3.29C17.98,2.9 17.35,2.9 16.96,3.29L15.13,5.12L18.88,8.87L20.71,7.04M4,5H13V7H4V5M4,9H10V11H4V9Z";
    private const string FolderOpen = "M19,20H4C2.9,20 2,19.1 2,18V6C2,4.9 2.9,4 4,4H10L12,6H20C21.1,6 22,6.9 22,8V9H4V18L6.33,11H23L20.67,18C20.4,19.17 19.8,20 19,20Z";
    private const string GitHub = "M12 .297c-6.63 0-12 5.373-12 12 0 5.303 3.438 9.8 8.205 11.385.6.113.82-.258.82-.577 0-.285-.01-1.04-.015-2.04-3.338.724-4.042-1.61-4.042-1.61C4.422 18.07 3.633 17.7 3.633 17.7c-1.087-.744.084-.729.084-.729 1.205.084 1.838 1.236 1.838 1.236 1.07 1.835 2.809 1.305 3.495.998.108-.776.417-1.305.76-1.605-2.665-.3-5.466-1.332-5.466-5.93 0-1.31.465-2.38 1.235-3.22-.135-.303-.54-1.523.105-3.176 0 0 1.005-.322 3.3 1.23.96-.267 1.98-.399 3-.405 1.02.006 2.04.138 3 .405 2.28-1.552 3.285-1.23 3.285-1.23.645 1.653.24 2.873.12 3.176.765.84 1.23 1.91 1.23 3.22 0 4.61-2.805 5.625-5.475 5.92.42.36.81 1.096.81 2.22 0 1.606-.015 2.896-.015 3.286 0 .315.21.69.825.57C20.565 22.092 24 17.592 24 12.297c0-6.627-5.373-12-12-12";
    private const string Info = "M11,17H13V11H11V17M12,2A10,10 0,1 0,12,22A10,10 0,1 0,12,2M11,7H13V9H11V7Z";
    private const string Keyboard = "M20,5H4C2.9,5 2,5.9 2,7V17C2,18.1 2.9,19 4,19H20C21.1,19 22,18.1 22,17V7C22,5.9 21.1,5 20,5M4,7H20V17H4V7M6,9H8V11H6V9M9,9H11V11H9V9M12,9H14V11H12V9M15,9H17V11H15V9M18,9H20V11H18V9M6,12H8V14H6V12M9,12H11V14H9V12M12,12H14V14H12V12M15,12H20V14H15V12Z";
    private const string Location = "M12,2A7,7 0,0 0,5,9C5,14.25 12,22 12,22C12,22 19,14.25 19,9A7,7 0,0 0,12,2M12,11.5A2.5,2.5 0,1 1,12,6.5A2.5,2.5 0,1 1,12,11.5Z";
    private const string Menu = "M3,6H21V8H3V6M3,11H21V13H3V11M3,16H21V18H3V16Z";
    private const string Minus = "M5,11H19V13H5V11Z";
    private const string Mouse = "M12,2A6,6 0,0 0,6,8V16A6,6 0,0 0,18,16V8A6,6 0,0 0,12,2M11,4.1V9H8V8A4,4 0,0 1,11,4.1M13,4.1A4,4 0,0 1,16,8V9H13V4.1M8,11H16V16A4,4 0,0 1,8,16V11Z";
    private const string Play = "M8,5V19L19,12L8,5Z";
    private const string Record = "M12,2A10,10 0,1 0,12,22A10,10 0,1 0,12,2M12,7A5,5 0,1 1,12,17A5,5 0,1 1,12,7Z";
    private const string Redo = "M12,5V2L17,7L12,12V9C8.69,9 6,11.69 6,15C6,16.01 6.25,16.96 6.7,17.8L5.24,19.26C4.46,18.03 4,16.57 4,15C4,10.58 7.58,7 12,7V5Z";
    private const string Save = "M17,3H5C3.89,3 3,3.9 3,5V19C3,20.1 3.89,21 5,21H19C20.1,21 21,20.1 21,19V7L17,3M12,19A3,3 0,1 1,12,13A3,3 0,1 1,12,19M6,5H15V9H6V5Z";
    private const string Settings = "M19.43,12.98C19.47,12.66 19.5,12.34 19.5,12C19.5,11.66 19.47,11.33 19.42,11L21.54,9.35L19.54,5.88L17.05,6.88C16.5,6.46 15.91,6.12 15.25,5.86L14.88,3.21H10.88L10.5,5.86C9.85,6.12 9.25,6.47 8.71,6.88L6.22,5.88L4.22,9.35L6.34,11C6.29,11.33 6.25,11.66 6.25,12C6.25,12.34 6.29,12.67 6.34,13L4.22,14.65L6.22,18.12L8.71,17.12C9.26,17.54 9.85,17.88 10.5,18.14L10.88,20.79H14.88L15.25,18.14C15.91,17.88 16.5,17.53 17.05,17.12L19.54,18.12L21.54,14.65L19.43,12.98M12.88,15.5A3.5,3.5 0,1 1,12.88,8.5A3.5,3.5 0,1 1,12.88,15.5Z";
    private const string Stop = "M6,6H18V18H6V6Z";
    private const string Success = "M12,2A10,10 0,1 0,12,22A10,10 0,1 0,12,2M10,15.17L6.83,12L5.41,13.41L10,18L19,9L17.59,7.59L10,15.17Z";
    private const string Timer = "M15,1H9V3H15V1M11,13H13V7H11V13M12,4A9,9 0,1 0,12,22A9,9 0,1 0,12,4M12,20A7,7 0,1 1,12,6A7,7 0,1 1,12,20Z";
    private const string Tip = "M9,21H15V19H9V21M12,2A7,7 0,0 0,8,14C8.8,14.8 9.5,15.8 10,17H14C14.5,15.8 15.2,14.8 16,14A7,7 0,0 0,12,2M14.85,12.6C13.78,13.38 13.17,14.4 12.82,15H11.18C10.83,14.4 10.22,13.38 9.15,12.6A5,5 0,1 1,14.85,12.6Z";
    private const string Tools = "M22.7,19L13.6,9.9C14.5,7.6 14,4.9 12,2.9C9.9,0.8 6.8,0.4 4.3,1.8L8.6,6.1L6.1,8.6L1.7,4.3C0.4,6.8 0.8,9.9 2.9,12C4.9,14 7.6,14.5 9.9,13.6L19,22.7C19.4,23.1 20,23.1 20.4,22.7L22.7,20.4C23.1,20 23.1,19.4 22.7,19Z";
    private const string Undo = "M12,5V2L7,7L12,12V9C15.31,9 18,11.69 18,15C18,16.01 17.75,16.96 17.3,17.8L18.76,19.26C19.54,18.03 20,16.57 20,15C20,10.58 16.42,7 12,7V5Z";
    private const string Warning = "M1,21H23L12,2L1,21M13,18H11V16H13V18M13,14H11V10H13V14Z";
}
