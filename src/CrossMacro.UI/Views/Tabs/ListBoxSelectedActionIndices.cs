using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Selection;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
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

    private static readonly AttachedProperty<bool> IsWritingSelectionToViewModelProperty =
        AvaloniaProperty.RegisterAttached<ListBox, bool>(
            "IsWritingSelectionToViewModel",
            typeof(ListBoxSelectedActionIndices));

    private static readonly AttachedProperty<bool> IsSelectionSyncPendingProperty =
        AvaloniaProperty.RegisterAttached<ListBox, bool>(
            "IsSelectionSyncPending",
            typeof(ListBoxSelectedActionIndices));

    private static readonly AttachedProperty<bool> AreSelectionHandlersAttachedProperty =
        AvaloniaProperty.RegisterAttached<ListBox, bool>(
            "AreSelectionHandlersAttached",
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

        if (args.NewValue is IList<int> selectedUnderlyingIndices)
        {
            listBox.DetachedFromVisualTree += OnDetachedFromVisualTree;
            listBox.AttachedToVisualTree += OnAttachedToVisualTree;
            AttachSelectionHandlers(listBox, selectedUnderlyingIndices);
            SyncListBoxSelection(listBox);
        }
    }

    private static void AttachSelectionHandlers(ListBox listBox, IList<int> selectedUnderlyingIndices)
    {
        if (listBox.GetValue(AreSelectionHandlersAttachedProperty))
        {
            return;
        }

        if (selectedUnderlyingIndices is INotifyCollectionChanged selectedIndicesCollection)
        {
            NotifyCollectionChangedEventHandler selectedIndicesHandler = (_, _) =>
            {
                if (!listBox.GetValue(IsWritingSelectionToViewModelProperty))
                {
                    RequestListBoxSelectionSync(listBox);
                }
            };
            listBox.SetValue(BoundSelectionChangedHandlerProperty, selectedIndicesHandler);
            selectedIndicesCollection.CollectionChanged += selectedIndicesHandler;
        }

        if (listBox.Items is INotifyCollectionChanged itemsCollection)
        {
            NotifyCollectionChangedEventHandler itemsHandler = (_, _) => RequestListBoxSelectionSync(listBox);
            listBox.SetValue(BoundItemsChangedHandlerProperty, itemsHandler);
            itemsCollection.CollectionChanged += itemsHandler;
        }

        listBox.SelectionChanged += OnSelectionChanged;
        listBox.AddHandler(InputElement.PointerPressedEvent, OnPointerPressed, RoutingStrategies.Tunnel, handledEventsToo: true);
        listBox.SetValue(AreSelectionHandlersAttachedProperty, value: true);
    }

    private static void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not ListBox listBox)
        {
            return;
        }

        var selectionMode = listBox.SelectionMode;
        if (selectionMode is not SelectionMode.Multiple || (selectionMode & SelectionMode.Toggle) is SelectionMode.Toggle)
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

        if (e.KeyModifiers is not KeyModifiers.None)
        {
            return;
        }

        if (!TryDeselectSelectedSourceAction(listBox, actionItem))
        {
            return;
        }

        listBox.SetValue(IsWritingExplicitEmptySelectionProperty, value: true);
        try
        {
            SyncSelectionToViewModel(listBox);
        }
        finally
        {
            listBox.SetValue(IsWritingExplicitEmptySelectionProperty, value: false);
            listBox.SetValue(IsWritingUserSelectionProperty, value: false);
        }

        e.Handled = true;
    }

    internal static bool TryDeselectSelectedSourceAction(ListBox listBox, EditorActionListItem actionItem)
    {
        ArgumentNullException.ThrowIfNull(listBox);
        ArgumentNullException.ThrowIfNull(actionItem);

        if (!actionItem.RepresentsSourceAction || (listBox.SelectedItems?.Contains(actionItem)) is not true)
        {
            return false;
        }

        listBox.SelectedItems.Remove(actionItem);
        return true;
    }

    internal static void MarkSelectionChangeAsUserInitiated(ListBox listBox)
    {
        ArgumentNullException.ThrowIfNull(listBox);
        listBox.SetValue(IsWritingUserSelectionProperty, value: true);
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
            DetachSelectionHandlers(listBox, GetSelectedUnderlyingIndices(listBox));
        }
    }

    private static void OnAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        if (sender is ListBox listBox && GetSelectedUnderlyingIndices(listBox) is { } selectedUnderlyingIndices)
        {
            AttachSelectionHandlers(listBox, selectedUnderlyingIndices);
            SyncListBoxSelection(listBox);
        }
    }

    private static void DetachHandlers(ListBox listBox, object? boundCollection)
    {
        DetachSelectionHandlers(listBox, boundCollection);
        listBox.DetachedFromVisualTree -= OnDetachedFromVisualTree;
        listBox.AttachedToVisualTree -= OnAttachedToVisualTree;
    }

    private static void DetachSelectionHandlers(ListBox listBox, object? boundCollection)
    {
        listBox.SelectionChanged -= OnSelectionChanged;
        listBox.RemoveHandler(InputElement.PointerPressedEvent, OnPointerPressed);

        var oldHandler = listBox.GetValue(BoundSelectionChangedHandlerProperty);
        if (boundCollection is INotifyCollectionChanged oldCollection && oldHandler is not null)
        {
            oldCollection.CollectionChanged -= oldHandler;
        }

        var oldItemsHandler = listBox.GetValue(BoundItemsChangedHandlerProperty);
        if (listBox.Items is INotifyCollectionChanged itemsCollection && oldItemsHandler is not null)
        {
            itemsCollection.CollectionChanged -= oldItemsHandler;
        }

        listBox.SetValue(BoundSelectionChangedHandlerProperty, value: null);
        listBox.SetValue(BoundItemsChangedHandlerProperty, value: null);
        listBox.SetValue(IsSelectionSyncPendingProperty, value: false);
        listBox.SetValue(AreSelectionHandlersAttachedProperty, value: false);
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
                listBox.SetValue(IsWritingUserSelectionProperty, value: false);
            }
        }
    }

    private static void SyncSelectionToViewModel(ListBox listBox)
    {
        var wasWritingSelection = listBox.GetValue(IsWritingSelectionToViewModelProperty);
        listBox.SetValue(IsWritingSelectionToViewModelProperty, value: true);
        try
        {
            var target = GetSelectedUnderlyingIndices(listBox);
            if (target is null)
            {
                return;
            }

            if (listBox.SelectedItems is null)
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

            if (normalizedSelectedIndices.Length is 0
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
        finally
        {
            listBox.SetValue(IsWritingSelectionToViewModelProperty, wasWritingSelection);
        }
    }

    private static void SyncListBoxSelection(ListBox listBox)
    {
        var target = GetSelectedUnderlyingIndices(listBox);
        if (target is null || listBox.SelectedItems is null)
        {
            return;
        }

        var targetSet = target.ToHashSet();
        var desiredActionItems = GetVisibleSelectedSourceItems(listBox.Items.Cast<object?>(), targetSet);

        listBox.SetValue(IsWritingUserSelectionProperty, value: false);
        listBox.SetValue(IsSynchronizingSelectionProperty, value: true);
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
            listBox.SetValue(IsSynchronizingSelectionProperty, value: false);
        }
    }

    private static void RequestListBoxSelectionSync(ListBox listBox)
    {
        if (listBox.GetValue(IsSelectionSyncPendingProperty))
        {
            return;
        }

        listBox.SetValue(IsSelectionSyncPendingProperty, value: true);
        Dispatcher.UIThread.Post(() =>
        {
            if (!listBox.GetValue(IsSelectionSyncPendingProperty))
            {
                return;
            }

            listBox.SetValue(IsSelectionSyncPendingProperty, value: false);
            SyncListBoxSelection(listBox);
        });
    }

    private static T? FindAncestor<T>(Visual? element)
        where T : Visual
    {
        while (element is not null)
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
