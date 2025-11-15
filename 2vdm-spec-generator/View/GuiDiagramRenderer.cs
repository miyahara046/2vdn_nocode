using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using _2vdm_spec_generator.ViewModel;

#if WINDOWS
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
#endif

namespace _2vdm_spec_generator.View
{
    public class GuiDiagramRenderer : ContentView
    {
        private readonly GraphicsView _graphicsView;
        private readonly GuiDiagramDrawable _drawable;

        private GuiElement _draggedNode = null;
        private PointF _dragOffset;
        private const float GridSize = 20f;
        private const float NodeWidth = GuiDiagramDrawable.NodeWidth;
        private const float NodeHeight = GuiDiagramDrawable.NodeHeight;

        public GuiDiagramRenderer()
        {
            _drawable = new GuiDiagramDrawable();
            _graphicsView = new GraphicsView
            {
                Drawable = _drawable,
                BackgroundColor = Colors.Transparent
            };

            Content = _graphicsView;

            _graphicsView.HandlerChanged += GraphicsView_HandlerChanged;

#if !WINDOWS
            var pan = new PanGestureRecognizer();
            pan.PanUpdated += Pan_PanUpdated;
            _graphicsView.GestureRecognizers.Add(pan);
#endif
        }

        private void GraphicsView_HandlerChanged(object sender, EventArgs e)
        {
#if WINDOWS
            if (_graphicsView.Handler?.PlatformView is UIElement oldEl)
            {
                oldEl.PointerPressed -= Platform_PointerPressed;
                oldEl.PointerMoved -= Platform_PointerMoved;
                oldEl.PointerReleased -= Platform_PointerReleased;
            }

            if (_graphicsView.Handler?.PlatformView is UIElement el)
            {
                el.PointerPressed += Platform_PointerPressed;
                el.PointerMoved += Platform_PointerMoved;
                el.PointerReleased += Platform_PointerReleased;
            }
#endif
        }

#if !WINDOWS
        // 簡易フォールバック（実機で微調整が必要）
        private PointF? _panStartOriginalPos = null;
        private GuiElement _panTarget = null;
        private void Pan_PanUpdated(object sender, PanUpdatedEventArgs e)
        {
            switch (e.StatusType)
            {
                case GestureStatus.Started:
                    _panTarget = _drawable.Elements
                        .Where(el => el.Type != GuiElementType.Timeout && el.Type != GuiElementType.Event)
                        .FirstOrDefault();
                    if (_panTarget != null)
                        _panStartOriginalPos = new PointF(_panTarget.X, _panTarget.Y);
                    break;
                case GestureStatus.Running:
                    if (_panTarget != null && _panStartOriginalPos.HasValue)
                    {
                        _panTarget.X = _panStartOriginalPos.Value.X + (float)e.TotalX;
                        _panTarget.Y = _panStartOriginalPos.Value.Y + (float)e.TotalY;
                        _graphicsView.Invalidate();
                    }
                    break;
                case GestureStatus.Completed:
                case GestureStatus.Canceled:
                    _panTarget = null;
                    _panStartOriginalPos = null;
                    break;
            }
        }
#endif

#if WINDOWS
        private void Platform_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            if (!(sender is UIElement uiElement)) return;
            var pt = e.GetCurrentPoint(uiElement).Position;
            TryStartDrag((float)pt.X, (float)pt.Y);
        }

        private void Platform_PointerMoved(object sender, PointerRoutedEventArgs e)
        {
            if (_draggedNode == null) return;
            if (!(sender is UIElement uiElement)) return;
            var pt = e.GetCurrentPoint(uiElement).Position;
            ContinueDrag((float)pt.X, (float)pt.Y);
        }

        private void Platform_PointerReleased(object sender, PointerRoutedEventArgs e)
        {
            EndDrag();
        }
#endif

        private void TryStartDrag(float pointerX, float pointerY)
        {
            // 上にあるノード優先で逆順検索
            foreach (var el in _drawable.Elements.AsEnumerable().Reverse())
            {
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


        private void ContinueDrag(float pointerX, float pointerY)
        {
            if (_draggedNode == null) return;

            _draggedNode.X = pointerX - _dragOffset.X;
            _draggedNode.Y = pointerY - _dragOffset.Y;

            ResolveCollisions(_draggedNode);

            _graphicsView.Invalidate();
        }


        private void EndDrag()
        {
            if (_draggedNode != null)
            {
                // スナップ処理
                _draggedNode.X = Snap(_draggedNode.X, GridSize);
                _draggedNode.Y = Snap(_draggedNode.Y, GridSize);

                _draggedNode.IsSelected = false;
                _draggedNode = null;

                _graphics_view_invalidate_safe();
            }
        }

        private float Snap(float value, float grid)
        {
            return (float)(Math.Round(value / grid) * grid);
        }

        // 安全に Invalidate を呼ぶヘルパー（nullチェック）
        private void _graphics_view_invalidate_safe()
        {
            _graphicsView?.Invalidate();
        }

        // ここに Distance ヘルパーを実装（もし距離判定が必要なら使える）
        private static float Distance(float x1, float y1, float x2, float y2)
            => MathF.Sqrt((x1 - x2) * (x1 - x2) + (y1 - y2) * (y1 - y2));

        // Render API
        public void Render(IEnumerable<GuiElement> elements)
        {
            var list = elements?.ToList() ?? new();
            _drawable.Elements = list;

            // Arrange 初期化（タイムアウト/イベントの初期配置）
            _drawable.ArrangeNodes();

            // Canvas 高さ調整（空コレクション対応）
            float maxY = _drawable.Elements.Select(e => e.Y + GuiDiagramDrawable.NodeHeight).DefaultIfEmpty(0).Max();
            float height = Math.Max(maxY + 50f, 100f);

            _graphicsView.HeightRequest = height;
            this.HeightRequest = height;
            InvalidateMeasure();
            _graphicsView.Invalidate();
        }

        private bool IsOverlapping(GuiElement a, GuiElement b)
        {
            return !(a.X + NodeWidth < b.X ||
                     b.X + NodeWidth < a.X ||
                     a.Y + NodeHeight < b.Y ||
                     b.Y + NodeHeight < a.Y);
        }

        private void ResolveCollisions(GuiElement target)
{
    foreach (var other in _drawable.Elements)
    {
        if (other == target) continue;

        if (IsOverlapping(target, other))
        {
            // “押し返す距離” を計算
            float dx = (target.X - other.X);
            float dy = (target.Y - other.Y);

            if (Math.Abs(dx) < 1) dx = 1;
            if (Math.Abs(dy) < 1) dy = 1;

            target.X += dx * 0.3f;
            target.Y += dy * 0.3f;
        }
    }
}




    }
}
