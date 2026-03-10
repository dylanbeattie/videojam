using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using VideoJam.UI.ViewModels;

namespace VideoJam.UI.Behaviours;

/// <summary>
/// An attached behaviour that enables drag-and-drop reordering of <see cref="ListBox"/> items.
/// Drag events are routed to <see cref="MainViewModel.ReorderSongCommand"/> via the DataContext,
/// keeping all business logic in the ViewModel and the code-behind free of mutation logic.
/// </summary>
/// <remarks>
/// <para>Usage in XAML: <c>&lt;ListBox behaviours:DragDropBehaviour.IsEnabled="True" /&gt;</c></para>
/// <para>
/// Per-instance drag state is stored in a <see cref="ConditionalWeakTable{TKey,TValue}"/> keyed
/// by the <see cref="ListBox"/> object, so multiple lists can coexist without state leakage.
/// </para>
/// <para>
/// A <see cref="DropIndicatorAdorner"/> draws a horizontal line between items during a drag
/// to show the operator where the song will be inserted on drop.
/// </para>
/// <para>
/// Item reordering uses <see cref="System.Collections.ObjectModel.ObservableCollection{T}.Move"/>
/// (single atomic operation) to avoid index-shift bugs that occur with remove-then-insert patterns.
/// </para>
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

	// ── Per-instance drag state ───────────────────────────────────────────────

	/// <summary>
	/// Stores drag state keyed by <see cref="ListBox"/> instance so multiple lists
	/// never share state, and GC can collect entries when a list is collected.
	/// </summary>
	private static readonly ConditionalWeakTable<ListBox, DragState> States = new();

	private sealed class DragState {
		public int SourceIndex = -1;
		public DropIndicatorAdorner? Adorner;
	}

	// ── Property change handler ───────────────────────────────────────────────

	private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
		if (d is not ListBox listBox) return;

		if ((bool)e.NewValue) {
			listBox.AllowDrop = true;
			listBox.PreviewMouseMove += OnPreviewMouseMove;
			listBox.DragOver += OnDragOver;
			listBox.DragLeave += OnDragLeave;
			listBox.Drop += OnDrop;
		} else {
			listBox.AllowDrop = false;
			listBox.PreviewMouseMove -= OnPreviewMouseMove;
			listBox.DragOver -= OnDragOver;
			listBox.DragLeave -= OnDragLeave;
			listBox.Drop -= OnDrop;
			RemoveAdorner(listBox);
		}
	}

	// ── Drag initiation ───────────────────────────────────────────────────────

	private static void OnPreviewMouseMove(object sender, MouseEventArgs e) {
		if (e.LeftButton != MouseButtonState.Pressed) return;
		if (sender is not ListBox listBox) return;
		if (listBox.SelectedIndex < 0) return;

		var state = States.GetOrCreateValue(listBox);
		state.SourceIndex = listBox.SelectedIndex;

		EnsureAdorner(listBox, state);
		DragDrop.DoDragDrop(listBox, listBox.SelectedItem!, DragDropEffects.Move);

		// DoDragDrop is synchronous — clean up adorner after the drag ends.
		RemoveAdorner(listBox);
		if (States.TryGetValue(listBox, out var s)) s.SourceIndex = -1;
	}

	// ── Drag-over: update drop indicator ─────────────────────────────────────

	private static void OnDragOver(object sender, DragEventArgs e) {
		if (sender is not ListBox listBox) return;
		e.Effects = DragDropEffects.Move;
		e.Handled = true;

		if (!States.TryGetValue(listBox, out var state)) return;

		var targetIndex = GetInsertionIndex(listBox, e.GetPosition(listBox));
		EnsureAdorner(listBox, state);
		state.Adorner?.SetTargetIndex(targetIndex);
	}

	private static void OnDragLeave(object sender, DragEventArgs e) {
		if (sender is not ListBox listBox) return;
		if (States.TryGetValue(listBox, out var state))
			state.Adorner?.SetTargetIndex(-1);
	}

	// ── Drop handling ─────────────────────────────────────────────────────────

	private static void OnDrop(object sender, DragEventArgs e) {
		if (sender is not ListBox listBox) return;
		if (!States.TryGetValue(listBox, out var state)) return;
		if (state.SourceIndex < 0) return;

		var targetIndex = GetInsertionIndex(listBox, e.GetPosition(listBox));
		var fromIndex = state.SourceIndex;

		// Normalise: insertion-point semantics → target item index.
		// When the insertion point is after the source, the actual target is one below it.
		var moveToIndex = targetIndex > fromIndex ? targetIndex - 1 : targetIndex;

		if (moveToIndex != fromIndex && moveToIndex >= 0 && moveToIndex < listBox.Items.Count) {
			if (listBox.DataContext is MainViewModel vm) {
				vm.ReorderSongCommand.Execute((fromIndex, moveToIndex));
				listBox.SelectedIndex = moveToIndex;
			}
		}

		RemoveAdorner(listBox);
		state.SourceIndex = -1;
	}

	// ── Adorner management ────────────────────────────────────────────────────

	private static void EnsureAdorner(ListBox listBox, DragState state) {
		if (state.Adorner is not null) return;
		var layer = AdornerLayer.GetAdornerLayer(listBox);
		if (layer is null) return;
		state.Adorner = new DropIndicatorAdorner(listBox);
		layer.Add(state.Adorner);
	}

	private static void RemoveAdorner(ListBox listBox) {
		if (!States.TryGetValue(listBox, out var state) || state.Adorner is null) return;
		var layer = AdornerLayer.GetAdornerLayer(listBox);
		layer?.Remove(state.Adorner);
		state.Adorner = null;
	}

	// ── Insertion index helpers ───────────────────────────────────────────────

	/// <summary>
	/// Returns the insertion index (0 = before first item, Count = after last item)
	/// corresponding to <paramref name="dropPosition"/> within the <see cref="ListBox"/>.
	/// Uses the midpoint of each item to decide whether the insertion is above or below it.
	/// </summary>
	private static int GetInsertionIndex(ListBox listBox, Point dropPosition) {
		for (int i = 0; i < listBox.Items.Count; i++) {
			if (listBox.ItemContainerGenerator.ContainerFromIndex(i) is not ListBoxItem item) continue;

			var topLeft = item.TranslatePoint(new Point(), listBox);
			var midY = topLeft.Y + item.ActualHeight / 2.0;

			if (dropPosition.Y < midY)
				return i;
		}
		return listBox.Items.Count;
	}

	// ── Drop indicator adorner ────────────────────────────────────────────────

	/// <summary>
	/// Renders a horizontal blue insertion-line between setlist items during a drag operation
	/// to indicate where the song will be placed on drop.
	/// </summary>
	private sealed class DropIndicatorAdorner : Adorner {
		private static readonly Pen IndicatorPen = new(
			new SolidColorBrush(Color.FromRgb(0x00, 0x78, 0xD4)), // Windows accent blue
			2.0) { DashStyle = DashStyles.Solid };

		private readonly ListBox _listBox;
		private int _targetIndex = -1;

		public DropIndicatorAdorner(ListBox listBox) : base(listBox) {
			_listBox = listBox;
			IsHitTestVisible = false;
		}

		/// <summary>Updates the insertion index and triggers a redraw.</summary>
		public void SetTargetIndex(int index) {
			_targetIndex = index;
			InvalidateVisual();
		}

		/// <inheritdoc />
		protected override void OnRender(DrawingContext drawingContext) {
			if (_targetIndex < 0) return;

			double y;
			if (_targetIndex < _listBox.Items.Count) {
				// Draw line above the target item.
				if (_listBox.ItemContainerGenerator.ContainerFromIndex(_targetIndex) is not ListBoxItem item) return;
				y = item.TranslatePoint(new Point(), _listBox).Y;
			} else {
				// Draw line below the last item.
				if (_listBox.Items.Count == 0) return;
				if (_listBox.ItemContainerGenerator.ContainerFromIndex(_listBox.Items.Count - 1)
					is not ListBoxItem lastItem) return;
				var pos = lastItem.TranslatePoint(new Point(), _listBox);
				y = pos.Y + lastItem.ActualHeight;
			}

			const double CapRadius = 4.0;
			var width = _listBox.ActualWidth;

			drawingContext.DrawLine(IndicatorPen, new Point(CapRadius, y), new Point(width - CapRadius, y));
			// Small circular caps at each end for a polished look.
			drawingContext.DrawEllipse(IndicatorPen.Brush, null, new Point(CapRadius, y), CapRadius, CapRadius);
			drawingContext.DrawEllipse(IndicatorPen.Brush, null, new Point(width - CapRadius, y), CapRadius, CapRadius);
		}
	}
}
