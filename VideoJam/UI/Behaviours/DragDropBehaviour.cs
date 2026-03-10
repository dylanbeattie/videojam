using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using VideoJam.UI.ViewModels;

namespace VideoJam.UI.Behaviours;

/// <summary>
/// An attached behaviour that enables drag-and-drop reordering of <see cref="ListBox"/> items.
/// Drag events are routed to <see cref="MainViewModel.ReorderSongCommand"/> via the DataContext,
/// keeping all business logic in the ViewModel and the code-behind free of mutation logic.
/// </summary>
/// <remarks>
/// Usage in XAML:
/// <code>
/// &lt;ListBox behaviours:DragDropBehaviour.IsEnabled="True" /&gt;
/// </code>
/// The attached property wires up WPF drag-drop events. Item reordering uses
/// <see cref="System.Collections.ObjectModel.ObservableCollection{T}.Move"/> (a single
/// atomic operation) to avoid index-shift bugs that occur with remove-then-insert patterns.
/// </remarks>
public static class DragDropBehaviour {
	// ── IsEnabled attached property ───────────────────────────────────────────

	/// <summary>
	/// Gets or sets whether drag-and-drop reordering is enabled on the attached <see cref="ListBox"/>.
	/// </summary>
	public static readonly DependencyProperty IsEnabledProperty =
		DependencyProperty.RegisterAttached(
			"IsEnabled",
			typeof(bool),
			typeof(DragDropBehaviour),
			new PropertyMetadata(false, OnIsEnabledChanged));

	/// <summary>Gets the value of the <see cref="IsEnabledProperty"/> attached property.</summary>
	public static bool GetIsEnabled(DependencyObject obj) =>
		(bool)obj.GetValue(IsEnabledProperty);

	/// <summary>Sets the value of the <see cref="IsEnabledProperty"/> attached property.</summary>
	public static void SetIsEnabled(DependencyObject obj, bool value) =>
		obj.SetValue(IsEnabledProperty, value);

	// ── Drag state ────────────────────────────────────────────────────────────

	private static int _dragSourceIndex = -1;

	// ── Property change handler ───────────────────────────────────────────────

	private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
		if (d is not ListBox listBox) return;

		if ((bool)e.NewValue) {
			listBox.AllowDrop = true;
			listBox.PreviewMouseMove += OnPreviewMouseMove;
			listBox.Drop += OnDrop;
		} else {
			listBox.AllowDrop = false;
			listBox.PreviewMouseMove -= OnPreviewMouseMove;
			listBox.Drop -= OnDrop;
		}
	}

	// ── Drag initiation ───────────────────────────────────────────────────────

	private static void OnPreviewMouseMove(object sender, MouseEventArgs e) {
		if (e.LeftButton != MouseButtonState.Pressed) return;
		if (sender is not ListBox listBox) return;
		if (listBox.SelectedIndex < 0) return;

		_dragSourceIndex = listBox.SelectedIndex;
		DragDrop.DoDragDrop(listBox, listBox.SelectedItem!, DragDropEffects.Move);
	}

	// ── Drop handling ─────────────────────────────────────────────────────────

	private static void OnDrop(object sender, DragEventArgs e) {
		if (sender is not ListBox listBox) return;
		if (_dragSourceIndex < 0) return;

		var targetIndex = GetDropTargetIndex(listBox, e.GetPosition(listBox));
		if (targetIndex < 0 || targetIndex == _dragSourceIndex) {
			_dragSourceIndex = -1;
			return;
		}

		// Route to the ViewModel command — this is the ONLY mutation path.
		if (listBox.DataContext is MainViewModel vm) {
			vm.ReorderSongCommand.Execute((_dragSourceIndex, targetIndex));
			listBox.SelectedIndex = targetIndex;
		}

		_dragSourceIndex = -1;
	}

	// ── Hit-test helper ───────────────────────────────────────────────────────

	/// <summary>
	/// Returns the index of the <see cref="ListBoxItem"/> at <paramref name="dropPosition"/>,
	/// or -1 if no item is at that position.
	/// </summary>
	private static int GetDropTargetIndex(ListBox listBox, Point dropPosition) {
		for (int i = 0; i < listBox.Items.Count; i++) {
			if (listBox.ItemContainerGenerator.ContainerFromIndex(i) is ListBoxItem item) {
				var itemBounds = new Rect(
					item.TranslatePoint(new Point(), listBox),
					new Size(item.ActualWidth, item.ActualHeight));

				if (itemBounds.Contains(dropPosition))
					return i;
			}
		}
		return -1;
	}
}
