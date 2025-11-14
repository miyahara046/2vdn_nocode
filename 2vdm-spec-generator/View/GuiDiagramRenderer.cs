using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using System.Collections.Generic;
using System.Linq;
using _2vdm_spec_generator.ViewModel;
using System;

namespace _2vdm_spec_generator.View
{
    public class GuiDiagramRenderer : ContentView
    {
        private readonly GraphicsView _graphicsView;
        private readonly GuiDiagramDrawable _drawable;

        private GuiElement _draggedNode = null;
        private PointF _dragOffset;

        public GuiDiagramRenderer()
        {
            _drawable = new GuiDiagramDrawable();
            _graphicsView = new GraphicsView
            {
                Drawable = _drawable,
                BackgroundColor = Colors.Transparent,
                InputTransparent = false // タッチを受け取る
            };

            Content = _graphicsView;

            // パンジェスチャーを追加
            var panGesture = new PanGestureRecognizer();
            panGesture.PanUpdated += OnPanUpdated;
            _graphicsView.GestureRecognizers.Add(panGesture);
        }

        private void OnPanUpdated(object sender, PanUpdatedEventArgs e)
        {
            const float nodeW = 160, nodeH = 50;

            switch (e.StatusType)
            {
                case GestureStatus.Started:
                    {
                        // タッチ開始位置でノードを探す
                        foreach (var el in _drawable.Elements)
                        {
                            if (el.Type == GuiElementType.Timeout) continue; // タイムアウトは固定

                            var rect = new RectF(el.X, el.Y, nodeW, nodeH);
                            if (rect.Contains((float)e.TotalX, (float)e.TotalY))
                            {
                                _draggedNode = el;
                                _dragOffset = new PointF((float)e.TotalX - el.X, (float)e.TotalY - el.Y);
                                break;
                            }
                        }
                    }
                    break;

                case GestureStatus.Running:
                    {
                        if (_draggedNode != null)
                        {
                            _draggedNode.X = (float)e.TotalX - _dragOffset.X;
                            _draggedNode.Y = (float)e.TotalY - _dragOffset.Y;
                            _graphicsView.Invalidate();
                        }
                    }
                    break;

                case GestureStatus.Completed:
                case GestureStatus.Canceled:
                    {
                        _draggedNode = null;
                    }
                    break;
            }
        }

        public void Render(IEnumerable<GuiElement> elements)
        {
            var elementList = elements?.ToList() ?? new();
            _drawable.Elements = elementList;

            // ノードの初期位置を設定
            _drawable.ArrangeNodes();

            // Canvas 高さを自動調整
            float maxY = _drawable.Elements.Select(e => e.Y + 50).DefaultIfEmpty(0).Max();
            float height = Math.Max(maxY + 50, 100);

            _graphicsView.HeightRequest = height;
            this.HeightRequest = height;
            this.InvalidateMeasure();
            _graphicsView.Invalidate();
        }
    }
}