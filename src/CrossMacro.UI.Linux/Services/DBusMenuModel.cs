using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Tmds.DBus.Protocol;

namespace CrossMacro.UI.Linux.Services;

/// <summary>
/// Maps an Avalonia <see cref="NativeMenu"/> onto dbusmenu item ids and properties. Ids stay stable for items that
/// survive a menu change; all access must happen on the UI thread.
/// </summary>
internal sealed class DBusMenuModel : IDisposable
{
    private static readonly string[] ItemPropertyNames = ["type", "label", "enabled", "visible", "children-display", "toggle-type", "toggle-state"];
    private readonly NativeMenu _menu;
    private readonly Dictionary<int, NativeMenuItemBase> _items = [];
    private readonly Dictionary<NativeMenuItemBase, int> _ids = [];
    private readonly HashSet<NativeMenu> _menus = [];
    private int _nextId = 1;
    private bool _disposed;

    internal DBusMenuModel(NativeMenu menu)
    {
        _menu = menu;
        Rebuild(menu, previous: null);
    }

    /// <summary>Raised after the menu structure or a menu property changed.</summary>
    internal event EventHandler? Changed;

    internal bool Contains(int id) => id is 0 || _items.ContainsKey(id);

    internal IEnumerable<int> ItemIds => _items.Keys;

    public void Dispose()
    {
        if (_disposed) { return; }
        _disposed = true;
        Detach();
    }

    internal void TriggerEvent(int id, string eventName)
    {
        if (!string.Equals(eventName, "clicked", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(eventName, "activate", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (_items.TryGetValue(id, out var item)
            && item is NativeMenuItem { IsEnabled: true, IsVisible: true, Menu: null } menuItem)
        {
            if (menuItem is INativeMenuItemExporterEventsImplBridge bridge)
            {
                bridge.RaiseClicked();
            }
            else if (menuItem.Command is { } command && command.CanExecute(menuItem.CommandParameter))
            {
                command.Execute(menuItem.CommandParameter);
            }
        }
    }

    internal void WriteGroupItem(ref MessageWriter writer, int id, string[] names)
    {
        writer.WriteStructureStart();
        writer.WriteInt32(id);
        WriteItemProperties(ref writer, id, names);
    }

    internal void WriteLayout(ref MessageWriter writer, int id, int depth, string[] names)
    {
        writer.WriteStructureStart();
        writer.WriteInt32(id);
        WriteItemProperties(ref writer, id, names);
        var children = writer.WriteArrayStart(DBusType.Variant);
        var menu = id is 0 ? _menu : (_items[id] as NativeMenuItem)?.Menu;
        if (depth is not 0 && menu is not null)
        {
            foreach (var item in menu.Items)
            {
                writer.WriteSignature("(ia{sv}av)");
                WriteLayout(ref writer, _ids[item], depth < 0 ? -1 : depth - 1, names);
            }
        }
        writer.WriteArrayEnd(children);
    }

    internal bool TryGetItemProperty(int id, string name, out VariantValue value)
    {
        value = default;
        _ = _items.TryGetValue(id, out var item);
        var menuItem = item as NativeMenuItem;
        switch (name)
        {
            case "type": value = VariantValue.String(item is NativeMenuItemSeparator ? "separator" : "standard"); return true;
            case "label" when menuItem is not null: value = VariantValue.String(menuItem.Header ?? string.Empty); return true;
            case "enabled": value = VariantValue.Bool(menuItem?.IsEnabled ?? true); return true;
            case "visible": value = VariantValue.Bool(menuItem?.IsVisible ?? true); return true;
            case "children-display" when id is 0 || menuItem?.Menu is not null: value = VariantValue.String("submenu"); return true;
            case "toggle-type" when menuItem?.ToggleType is MenuItemToggleType.CheckBox: value = VariantValue.String("checkmark"); return true;
            case "toggle-type" when menuItem?.ToggleType is MenuItemToggleType.Radio: value = VariantValue.String("radio"); return true;
            case "toggle-state" when menuItem is not null && menuItem.ToggleType is not MenuItemToggleType.None: value = VariantValue.Int32(menuItem.IsChecked ? 1 : 0); return true;
            default: return false;
        }
    }

    private void WriteItemProperties(ref MessageWriter writer, int id, string[] names)
    {
        var array = writer.WriteDictionaryStart();
        foreach (var name in names.Length is 0 ? ItemPropertyNames : names)
        {
            if (!TryGetItemProperty(id, name, out var value)) { continue; }
            writer.WriteDictionaryEntryStart();
            writer.WriteString(name);
            writer.WriteVariant(value);
        }
        writer.WriteDictionaryEnd(array);
    }

    private void Detach()
    {
        foreach (var menu in _menus)
        {
            if (menu.Items is INotifyCollectionChanged collection) { collection.CollectionChanged -= OnCollectionChanged; }
        }
        foreach (var item in _ids.Keys) { item.PropertyChanged -= OnPropertyChanged; }
        _menus.Clear();
    }

    private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs args) => NotifyChanged(rebuild: true);

    private void OnPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs args) =>
        NotifyChanged(rebuild: args.Property == NativeMenuItem.MenuProperty);

    private void NotifyChanged(bool rebuild)
    {
        if (_disposed) { return; }
        if (rebuild)
        {
            // Retain IDs for surviving items but release removed items and their event subscriptions.
            Detach();
            var previous = new Dictionary<NativeMenuItemBase, int>(_ids);
            _ids.Clear();
            _items.Clear();
            Rebuild(_menu, previous);
        }
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void Rebuild(NativeMenu menu, Dictionary<NativeMenuItemBase, int>? previous)
    {
        if (!_menus.Add(menu)) { return; }
        if (menu.Items is INotifyCollectionChanged collection) { collection.CollectionChanged += OnCollectionChanged; }
        foreach (var item in menu.Items)
        {
            if (_ids.ContainsKey(item)) { continue; }
            var id = previous is not null && previous.TryGetValue(item, out var oldId) ? oldId : _nextId++;
            _ids.Add(item, id);
            _items.Add(id, item);
            item.PropertyChanged += OnPropertyChanged;
            if (item is NativeMenuItem { Menu: { } child }) { Rebuild(child, previous); }
        }
    }
}
