using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using System;
using System.Linq;
using System.Collections.Generic;
using _2vdm_spec_generator.ViewModel;

#if WINDOWS
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
#endif

namespace _2vdm_spec_generator.View
{
    /// <summary>
    /// GraphicsView を ScrollView に入れ、ポインタ/パンイベントでノードドラッグ（Yのみ）を実現。
    /// Windows はネイティブ Pointer、非Windows は PanGesture を使用。
    /// </summary>
    public class GuiDiagramRenderer : ContentView
    {
        private readonly GraphicsView _graphicsView;
        private readonly GuiDiagramDrawable _drawable;
        private readonly ScrollView _scrollView;

        private GuiElement _draggedNode = null;
        private PointF _dragOffset;

        // スナップグリッド（Y方向）
        private const float SnapSize = 40f;

        // 左右固定X
        private const float LeftColumnX = 40f;
        private const float RightColumnX = 350f;

        // 外部に最終座標を渡すためのコールバック
        public Action<IEnumerable<GuiElement>> PositionsChanged { get; set; }

        public void SetElements(IEnumerable<GuiElement> elements)
        {
            _drawable.Elements = elements.ToList();


            _drawable.ArrangeNodes();
            _graphicsView.Invalidate();
        }



        public GuiDiagramRenderer()
        {
            _drawable = new GuiDiagramDrawable();

            _graphicsView = new GraphicsView
            {
                Drawable = _drawable,
                BackgroundColor = Colors.White,
                InputTransparent = false
            };

            // ScrollView でラップしてスクロール可能にする（両方向）
            _scrollView = new ScrollView
            {
                Orientation = ScrollOrientation.Both,
                Content = _graphicsView,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Always,
                VerticalScrollBarVisibility = ScrollBarVisibility.Always
            };

            Content = _scrollView;

            // HandlerChanged でプラットフォーム固有イベントをつける
            _graphicsView.HandlerChanged += GraphicsView_HandlerChanged;

#if !WINDOWS
            // 非WindowsはPanGestureでドラッグ（スクロールとの衝突がある場合は調整が必要）
            var pan = new PanGestureRecognizer();
            pan.PanUpdated += NonWindows_PanUpdated;
            _graphicsView.GestureRecognizers.Add(pan);
#endif
        }

        private void GraphicsView_HandlerChanged(object sender, EventArgs e)
        {
#if WINDOWS
            // 既存の登録解除（安全対策）
            if (_graphicsView.Handler?.PlatformView is UIElement oldEl)
            {
                oldEl.PointerPressed -= Platform_PointerPressed;
                oldEl.PointerMoved -= Platform_PointerMoved;
                oldEl.PointerReleased -= Platform_PointerReleased;
            }

            // 新しく登録
            if (_graphicsView.Handler?.PlatformView is UIElement el)
            {
                el.PointerPressed += Platform_PointerPressed;
                el.PointerMoved += Platform_PointerMoved;
                el.PointerReleased += Platform_PointerReleased;
            }
#endif
        }

#if WINDOWS
        private void Platform_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            if (!(sender is UIElement ui)) return;
            var pt = e.GetCurrentPoint(ui).Position;
            // Convert pointer position to GraphicsView logical coords:
            // UIElement is the platform view that hosts GraphicsView; coordinates are suitable to use.
            TryStartDrag((float)pt.X, (float)pt.Y);
            // Optional: Capture pointer if desired (omitted - MAUI will handle)
        }

        private void Platform_PointerMoved(object sender, PointerRoutedEventArgs e)
        {
            if (_draggedNode == null) return;
            if (!(sender is UIElement ui)) return;
            var pt = e.GetCurrentPoint(ui).Position;
            ContinueDrag((float)pt.X, (float)pt.Y);
        }

        private void Platform_PointerReleased(object sender, PointerRoutedEventArgs e)
        {
            if (_draggedNode != null)
            {
                FinishDrag(_draggedNode);
            }
        }
#endif

#if !WINDOWS
        // 非WindowsのPanハンドラ（シンプル実装）
        private PointF? _panStartOriginalPos = null;
        private GuiElement _panTarget = null;

        private void NonWindows_PanUpdated(object sender, PanUpdatedEventArgs e)
        {
            switch (e.StatusType)
            {
                case GestureStatus.Started:
                    // Find nearest draggable node based on the current viewport center + pointer (approx)
                    // Using simple heuristic: find first movable element under vertical offset
                    _panTarget = _drawable.Elements.Where(CanDrag).OrderBy(el => Math.Abs(el.Y)).FirstOrDefault();
                    if (_panTarget != null)
                        _panStartOriginalPos = new PointF(_panTarget.X, _panTarget.Y);
                    break;

                case GestureStatus.Running:
                    if (_panTarget != null && _panStartOriginalPos.HasValue)
                    {
                        // Move only Y (preserve X per type)
                        var newY = _panStartOriginalPos.Value.Y + (float)e.TotalY;
                        _panTarget.Y = newY;
                        ApplyFixedX(_panTarget);
                        _graphicsView.Invalidate();
                    }
                    break;

                case GestureStatus.Completed:
                case GestureStatus.Canceled:
                    if (_panTarget != null)
                    {
                        FinishDrag(_panTarget);
                    }
                    _panTarget = null;
                    _panStartOriginalPos = null;
                    break;
            }
        }
#endif

        // 判定：ドラッグ可能か（Timeout は不可）
        private bool CanDrag(GuiElement el) => el != null && el.Type != GuiElementType.Timeout;

        // Try to start drag: iterate reverse order so that topmost elements are hit first
        private void TryStartDrag(float pointerX, float pointerY)
        {
            foreach (var el in _drawable.Elements.AsEnumerable().Reverse())
            {
                if (!CanDrag(el)) continue;

                var rect = new RectF(el.X, el.Y, GuiDiagramDrawable.NodeWidth, GuiDiagramDrawable.NodeHeight);
                if (rect.Contains(pointerX, pointerY))
                {
                    _draggedNode = el;
                    _dragOffset = new PointF(pointerX - el.X, pointerY - el.Y);
                    el.IsSelected = true;
                    _graphicsView.Invalidate();
                    break;
                }
            }
        }

        // Continue drag: update only Y, then force X according to type
        private void ContinueDrag(float pointerX, float pointerY)
        {
            if (_draggedNode == null) return;

            float newY = pointerY - _dragOffset.Y;
            _draggedNode.Y = newY;
            ApplyFixedX(_draggedNode);

            // Live redraw
            _graphicsView.Invalidate();
        }


        // Finish drag: snap, reflow ordering, notify
        private void FinishDrag(GuiElement finishedNode)
        {
            if (finishedNode == null) return;

            // Snap to grid
            finishedNode.Y = Snap(finishedNode.Y, SnapSize);

            // Re-apply fixed X
            ApplyFixedX(finishedNode);

            // Reflow ordering to remove overlaps and make nice ordering
            ReflowVerticalOrdering();

            // Deselect
            finishedNode.IsSelected = false;

            // Notify external (ViewModel can persist)
            PositionsChanged?.Invoke(_drawable.Elements);

            // Redraw
            _graphicsView.Invalidate();

            _draggedNode = null;
        }

        private void ApplyFixedX(GuiElement el)
        {
            if (el == null) return;

            switch (el.Type)
            {
                case GuiElementType.Screen:
                case GuiElementType.Button:
                case GuiElementType.Operation:
                    el.X = LeftColumnX;
                    break;
                case GuiElementType.Event:
                    el.X = RightColumnX;
                    break;
                case GuiElementType.Timeout:
                    el.IsFixed = true;
                    el.X = LeftColumnX;
                    break;
            }
        }

        // Reflow: timeout on top, then others sorted by Y, spaced by SnapSize
        private void ReflowVerticalOrdering()
        {
            var timeouts = _drawable.Elements.Where(e => e.Type == GuiElementType.Timeout).OrderBy(e => e.Y).ToList();
            var movable = _drawable.Elements.Where(e => e.Type != GuiElementType.Timeout).OrderBy(e => e.Y).ToList();

            float baseY = 8f;
            if (timeouts.Any())
            {
                var lastT = timeouts.Last();
                baseY = lastT.Y + GuiDiagramDrawable.NodeHeight + 8f;
            }

            float y = baseY;
            foreach (var el in movable)
            {
                el.Y = y;
                ApplyFixedX(el);
                y += SnapSize;
            }

            _graphicsView.Invalidate();
        }

        private static float Snap(float value, float grid) => (float)(Math.Round(value / grid) * grid);

        // public Render API
        public void Render(IEnumerable<GuiElement> elements)
        {
            var list = elements?.ToList() ?? new();
            _drawable.Elements = list;

            // arrange nodes if unpositioned
            _drawable.ArrangeNodes();

            // compute content size and set GraphicsView size accordingly so ScrollView can scroll
            float maxY = 0f;
            if (_drawable.Elements.Any())
                maxY = _drawable.Elements.Max(e => e.Y + GuiDiagramDrawable.NodeHeight);

            var height = Math.Max(maxY + 120f, 300f);
            var width = 1000f; // sufficiently wide; adjust as needed

            _graphicsView.HeightRequest = height;
            _graphicsView.WidthRequest = width;
            this.HeightRequest = Math.Min(height, 800); // allow the page to determine visible area; adjust if needed

            InvalidateMeasure();
            _graphicsView.Invalidate();
        }

        // Public API to programmatically finish any current drag
        public void FinishCurrentDrag() => FinishDrag(_draggedNode);
    }
}
