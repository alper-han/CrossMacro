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
        if (!point.Properties.IsLeftButtonPressed || e.KeyModifiers != KeyModifiers.None)
        {
            return;
        }

        var item = FindAncestor<ListBoxItem>(e.Source as Visual);
        if (item?.DataContext is not EditorActionListItem actionItem || !actionItem.RepresentsSourceAction)
        {
            return;
        }

        if (!TryDeselectSelectedSourceAction(listBox, actionItem))
        {
            return;
        }

        SyncSelectionToViewModel(listBox);
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

        listBox.SetValue(BoundSelectionChangedHandlerProperty, null);
    }

    private static void OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (sender is ListBox listBox && !listBox.GetValue(IsSynchronizingSelectionProperty))
        {
            SyncSelectionToViewModel(listBox);
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
        var hasVisibleSourceActions = false;
        foreach (var selectedItem in listBox.SelectedItems)
        {
            if (selectedItem is EditorActionListItem actionItem && actionItem.RepresentsSourceAction)
            {
                selectedIndices.Add(actionItem.UnderlyingIndex);
            }
        }

        foreach (var item in listBox.Items)
        {
            if (item is EditorActionListItem { RepresentsSourceAction: true })
            {
                hasVisibleSourceActions = true;
                break;
            }
        }

        if (selectedIndices.Count == 0 && target.Count > 0 && !hasVisibleSourceActions)
        {
            return;
        }

        if (listBox.DataContext is EditorViewModel editorViewModel)
        {
            editorViewModel.ReplaceSelectedActionUnderlyingIndices(selectedIndices);
            return;
        }

        target.Clear();
        foreach (var selectedIndex in selectedIndices)
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
        listBox.SetValue(IsSynchronizingSelectionProperty, true);
        try
        {
            listBox.SelectedItems.Clear();
            foreach (var item in listBox.Items)
            {
                if (item is EditorActionListItem actionItem
                    && actionItem.RepresentsSourceAction
                    && targetSet.Contains(actionItem.UnderlyingIndex))
                {
                    listBox.SelectedItems.Add(actionItem);
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
