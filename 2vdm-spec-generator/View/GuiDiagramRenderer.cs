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

        public GuiDiagramRenderer()
        {
            _drawable = new GuiDiagramDrawable();
            _graphicsView = new GraphicsView
            {
                Drawable = _drawable,
                BackgroundColor = Colors.Transparent,
                InputTransparent = true
            };

            // ContentView（このクラス）は通常のヒットテスト挙動（false）のままにする
            Content = _graphicsView;
        }

        // public render API
        public void Render(IEnumerable<GuiElement> elements)
        {
            var elementList = elements?.ToList() ?? new();
            _drawable.Elements = elementList;

            // ノードの数から必要な高さを計算する
            // GuiDiagramDrawableのレイアウト設定を使用: nodeH=60, spacing=120, startY=20
            const double nodeH = 60, spacing = 120, startY = 20;

            // H = startY + (要素数 * spacing) - (spacing - nodeH)
            // H = 20 + (要素数 * 120) - 60
            // 要素が1つの場合: 20 + 1 * 120 = 140。ノードの上端Y=20、ノード下端Y=80。
            // 要素がN個の場合、最後のノードの上端Yは 20 + (N-1) * 120。下端Yは 20 + (N-1) * 120 + 60

            double requiredHeight = 0;
            if (elementList.Count > 0)
            {
                // 1. 最後のノードの上端Y座標を計算
                // (最初のノード(i=0)の開始Y) + (ノード数-1) * spacing 
                double lastNodeY = startY + (elementList.Count - 1) * spacing;

                // 2. 最後のノードの下端Y座標を計算
                // lastNodeY + nodeH
                double lastNodeBottom = lastNodeY + nodeH;

                // 3. 最後のノードの下にも少し余白 (例: startYと同じ20) を加える
                requiredHeight = lastNodeBottom + startY;

                // requiredHeightが最低限必要な高さ（例: 画面に何も表示がない場合）を下回らないように
                requiredHeight = Math.Max(requiredHeight, 10); ;
            }
            else
            {
                // 要素がない場合は最低限の高さ
                requiredHeight = 10;
            }

            // 計算された高さをHeightRequestに設定
            // ※ 少なくとも親コンテナ（ScrollView）の最小サイズは確保したいが、
            // ScrollViewがFillAndExpandなので、コンテンツサイズが優先される。
            _graphicsView.HeightRequest = requiredHeight;
            this.HeightRequest = requiredHeight;
            this.InvalidateMeasure();
            _graphicsView.Invalidate();
        }


    }

    public class GuiDiagramDrawable : IDrawable
    {
        public List<GuiElement> Elements { get; set; } = new();

        public void Draw(ICanvas canvas, RectF dirtyRect)
        {
            canvas.FillColor = Colors.White;
            canvas.FillRectangle(dirtyRect);

            if (Elements == null || Elements.Count == 0) return;

            float nodeW = 160, nodeH = 60, spacing = 120;
            float startX = 50, startY = 20;

            var positions = new Dictionary<string, PointF>();
            for (int i = 0; i < Elements.Count; i++)
                positions[Elements[i].Name] = new PointF(startX, startY + i * spacing);

            // draw connections
            canvas.StrokeColor = Colors.Gray;
            canvas.StrokeSize = 2;
            foreach (var el in Elements)
            {
                if (!string.IsNullOrEmpty(el.Target) && positions.ContainsKey(el.Target))
                {
                    var f = positions[el.Name];
                    var t = positions[el.Target];
                    var s = new PointF(f.X + nodeW, f.Y + nodeH / 2);
                    var e = new PointF(t.X, t.Y + nodeH / 2);
                    canvas.DrawLine(s, e);
                    DrawArrow(canvas, s, e);
                }
            }

            // draw nodes
            foreach (var el in Elements)
            {
                var p = positions[el.Name];
                var r = new RectF(p.X, p.Y, nodeW, nodeH);
                canvas.FillColor = el.Type switch
                {
                    GuiElementType.Screen => Colors.Purple,
                    GuiElementType.Button => Colors.LightBlue,
                    GuiElementType.Event => Colors.LightGreen,
                    GuiElementType.Timeout => Colors.Pink,
                    _ => Colors.LightGray
                };
                canvas.FillRoundedRectangle(r, 8);
                canvas.StrokeColor = Colors.Black;
                canvas.DrawRoundedRectangle(r, 8);
                canvas.FontColor = Colors.Black;
                canvas.FontSize = 14;
                canvas.DrawString(el.Name, r, HorizontalAlignment.Center, VerticalAlignment.Center);
            }
        }

        private void DrawArrow(ICanvas canvas, PointF start, PointF end)
        {
            float size = 6f;
            float angle = MathF.Atan2(end.Y - start.Y, end.X - start.X);
            var p1 = new PointF(end.X - size * MathF.Cos(angle - 0.3f), end.Y - size * MathF.Sin(angle - 0.3f));
            var p2 = new PointF(end.X - size * MathF.Cos(angle + 0.3f), end.Y - size * MathF.Sin(angle + 0.3f));
            canvas.DrawLine(end, p1);
            canvas.DrawLine(end, p2);
        }


    }
}
