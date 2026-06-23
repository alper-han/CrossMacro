using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Selection;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using CrossMacro.UI.ViewModels;

namespace CrossMacro.UI.Views.Tabs;

public static class ListBoxSelectedActionIndices
{
    private static readonly AttachedProperty<bool> IsSynchronizingSelectionProperty =
        AvaloniaProperty.RegisterAttached<ListBox, bool>(
            "IsSynchronizingSelection",
            typeof(ListBoxSelectedActionIndices));

    private static readonly AttachedProperty<NotifyCollectionChangedEventHandler?> BoundSelectionChangedHandlerProperty =
        AvaloniaProperty.RegisterAttached<ListBox, NotifyCollectionChangedEventHandler?>(
            "BoundSelectionChangedHandler",
            typeof(ListBoxSelectedActionIndices));

    private static readonly AttachedProperty<NotifyCollectionChangedEventHandler?> BoundItemsChangedHandlerProperty =
        AvaloniaProperty.RegisterAttached<ListBox, NotifyCollectionChangedEventHandler?>(
            "BoundItemsChangedHandler",
            typeof(ListBoxSelectedActionIndices));

    private static readonly AttachedProperty<bool> IsWritingExplicitEmptySelectionProperty =
        AvaloniaProperty.RegisterAttached<ListBox, bool>(
            "IsWritingExplicitEmptySelection",
            typeof(ListBoxSelectedActionIndices));

    private static readonly AttachedProperty<bool> IsWritingUserSelectionProperty =
        AvaloniaProperty.RegisterAttached<ListBox, bool>(
            "IsWritingUserSelection",
            typeof(ListBoxSelectedActionIndices));

    public static readonly AttachedProperty<IList<int>?> SelectedUnderlyingIndicesProperty =
        AvaloniaProperty.RegisterAttached<ListBox, IList<int>?>(
            "SelectedUnderlyingIndices",
            typeof(ListBoxSelectedActionIndices));

    static ListBoxSelectedActionIndices()
    {
        SelectedUnderlyingIndicesProperty.Changed.AddClassHandler<ListBox>(OnSelectedUnderlyingIndicesChanged);
    }

    public static IList<int>? GetSelectedUnderlyingIndices(ListBox element)
    {
        ArgumentNullException.ThrowIfNull(element);
        return element.GetValue(SelectedUnderlyingIndicesProperty);
    }

    public static void SetSelectedUnderlyingIndices(ListBox element, IList<int>? value)
    {
        ArgumentNullException.ThrowIfNull(element);
        element.SetValue(SelectedUnderlyingIndicesProperty, value);
    }

    private static void OnSelectedUnderlyingIndicesChanged(ListBox listBox, AvaloniaPropertyChangedEventArgs args)
    {
        DetachHandlers(listBox, args.OldValue);

        if (args.NewValue is IList<int>)
        {
            if (args.NewValue is INotifyCollectionChanged newCollection)
            {
                NotifyCollectionChangedEventHandler newHandler = (_, _) => SyncListBoxSelection(listBox);
                listBox.SetValue(BoundSelectionChangedHandlerProperty, newHandler);
                newCollection.CollectionChanged += newHandler;
            }

            if (listBox.Items is INotifyCollectionChanged itemsCollection)
            {
                NotifyCollectionChangedEventHandler itemsHandler = (_, _) => SyncListBoxSelection(listBox);
                listBox.SetValue(BoundItemsChangedHandlerProperty, itemsHandler);
                itemsCollection.CollectionChanged += itemsHandler;
            }

            listBox.SelectionChanged += OnSelectionChanged;
            listBox.AddHandler(InputElement.PointerPressedEvent, OnPointerPressed, RoutingStrategies.Tunnel, handledEventsToo: true);
            listBox.DetachedFromVisualTree += OnDetachedFromVisualTree;
            SyncListBoxSelection(listBox);
        }
    }

    private static void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not ListBox listBox)
        {
            return;
        }

        var selectionMode = listBox.SelectionMode;
        if (selectionMode != SelectionMode.Multiple || (selectionMode & SelectionMode.Toggle) == SelectionMode.Toggle)
        {
            return;
        }

        var point = e.GetCurrentPoint(listBox);
        if (!point.Properties.IsLeftButtonPressed)
        {
            return;
        }

        var item = FindAncestor<ListBoxItem>(e.Source as Visual);
        if (item?.DataContext is not EditorActionListItem actionItem || !actionItem.RepresentsSourceAction)
        {
            return;
        }

        MarkSelectionChangeAsUserInitiated(listBox);

        if (e.KeyModifiers != KeyModifiers.None)
        {
            return;
        }

        if (!TryDeselectSelectedSourceAction(listBox, actionItem))
        {
            return;
        }

        listBox.SetValue(IsWritingExplicitEmptySelectionProperty, true);
        try
        {
            SyncSelectionToViewModel(listBox);
        }
        finally
        {
            listBox.SetValue(IsWritingExplicitEmptySelectionProperty, false);
            listBox.SetValue(IsWritingUserSelectionProperty, false);
        }

        e.Handled = true;
    }

    internal static bool TryDeselectSelectedSourceAction(ListBox listBox, EditorActionListItem actionItem)
    {
        ArgumentNullException.ThrowIfNull(listBox);
        ArgumentNullException.ThrowIfNull(actionItem);

        if (!actionItem.RepresentsSourceAction || listBox.SelectedItems?.Contains(actionItem) != true)
        {
            return false;
        }

        listBox.SelectedItems.Remove(actionItem);
        return true;
    }

    internal static void MarkSelectionChangeAsUserInitiated(ListBox listBox)
    {
        ArgumentNullException.ThrowIfNull(listBox);
        listBox.SetValue(IsWritingUserSelectionProperty, true);
    }

    internal static IReadOnlyList<EditorActionListItem> GetVisibleSelectedSourceItems(
        IEnumerable<object?> items,
        IEnumerable<int> selectedUnderlyingIndices)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(selectedUnderlyingIndices);

        var targetSet = selectedUnderlyingIndices.ToHashSet();
        return items
            .OfType<EditorActionListItem>()
            .Where(item => item.RepresentsSourceAction && targetSet.Contains(item.UnderlyingIndex))
            .GroupBy(item => item.UnderlyingIndex)
            .Select(group => group.First())
            .ToArray();
    }

    private static void OnDetachedFromVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        if (sender is ListBox listBox)
        {
            DetachHandlers(listBox, GetSelectedUnderlyingIndices(listBox));
        }
    }

    private static void DetachHandlers(ListBox listBox, object? boundCollection)
    {
        listBox.SelectionChanged -= OnSelectionChanged;
        listBox.RemoveHandler(InputElement.PointerPressedEvent, OnPointerPressed);
        listBox.DetachedFromVisualTree -= OnDetachedFromVisualTree;

        var oldHandler = listBox.GetValue(BoundSelectionChangedHandlerProperty);
        if (boundCollection is INotifyCollectionChanged oldCollection && oldHandler != null)
        {
            oldCollection.CollectionChanged -= oldHandler;
        }

        var oldItemsHandler = listBox.GetValue(BoundItemsChangedHandlerProperty);
        if (listBox.Items is INotifyCollectionChanged itemsCollection && oldItemsHandler != null)
        {
            itemsCollection.CollectionChanged -= oldItemsHandler;
        }

        listBox.SetValue(BoundSelectionChangedHandlerProperty, null);
        listBox.SetValue(BoundItemsChangedHandlerProperty, null);
    }

    private static void OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (sender is ListBox listBox && !listBox.GetValue(IsSynchronizingSelectionProperty))
        {
            try
            {
                SyncSelectionToViewModel(listBox);
            }
            finally
            {
                listBox.SetValue(IsWritingUserSelectionProperty, false);
            }
        }
    }

    private static void SyncSelectionToViewModel(ListBox listBox)
    {
        var target = GetSelectedUnderlyingIndices(listBox);
        if (target == null)
        {
            return;
        }

        if (listBox.SelectedItems == null)
        {
            target.Clear();
            return;
        }

        var selectedIndices = new List<int>();
        foreach (var selectedItem in listBox.SelectedItems)
        {
            if (selectedItem is EditorActionListItem actionItem && actionItem.RepresentsSourceAction)
            {
                selectedIndices.Add(actionItem.UnderlyingIndex);
            }
        }

        var normalizedSelectedIndices = selectedIndices
            .Distinct()
            .OrderBy(index => index)
            .ToArray();

        if (normalizedSelectedIndices.Length == 0
            && target.Count > 0
            && !listBox.GetValue(IsWritingUserSelectionProperty)
            && !listBox.GetValue(IsWritingExplicitEmptySelectionProperty))
        {
            return;
        }

        if (normalizedSelectedIndices.Length > 0
            && target.Count > normalizedSelectedIndices.Length
            && !listBox.GetValue(IsWritingUserSelectionProperty)
            && normalizedSelectedIndices.All(target.Contains))
        {
            return;
        }

        if (listBox.DataContext is EditorViewModel editorViewModel)
        {
            editorViewModel.ReplaceSelectedActionUnderlyingIndices(normalizedSelectedIndices);
            return;
        }

        target.Clear();
        foreach (var selectedIndex in normalizedSelectedIndices)
        {
            target.Add(selectedIndex);
        }
    }

    private static void SyncListBoxSelection(ListBox listBox)
    {
        var target = GetSelectedUnderlyingIndices(listBox);
        if (target == null || listBox.SelectedItems == null)
        {
            return;
        }

        var targetSet = target.ToHashSet();
        var desiredActionItems = GetVisibleSelectedSourceItems(listBox.Items.Cast<object?>(), targetSet);

        listBox.SetValue(IsWritingUserSelectionProperty, false);
        listBox.SetValue(IsSynchronizingSelectionProperty, true);
        try
        {
            var desiredItems = desiredActionItems
                .Cast<object>()
                .ToArray();
            foreach (var selectedItem in listBox.SelectedItems.Cast<object>().ToArray())
            {
                listBox.SelectedItems.Remove(selectedItem);
            }

            foreach (var item in desiredItems)
            {
                listBox.SelectedItems.Add(item);
            }

            var seenSelectedItems = new HashSet<object>();
            var seenUnderlyingIndices = new HashSet<int>();
            foreach (var selectedItem in listBox.SelectedItems.Cast<object>().ToArray())
            {
                var duplicateSourceIndex = selectedItem is EditorActionListItem actionItem
                    && !seenUnderlyingIndices.Add(actionItem.UnderlyingIndex);
                if (!seenSelectedItems.Add(selectedItem) || duplicateSourceIndex)
                {
                    listBox.SelectedItems.Remove(selectedItem);
                }
            }
        }
        finally
        {
            listBox.SetValue(IsSynchronizingSelectionProperty, false);
        }
    }

    private static T? FindAncestor<T>(Visual? element)
        where T : Visual
    {
        while (element != null)
        {
            if (element is T typed)
            {
                return typed;
            }

            element = element.GetVisualParent();
        }

        return null;
    }
}
