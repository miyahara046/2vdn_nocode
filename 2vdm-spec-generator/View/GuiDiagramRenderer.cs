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

            Content = _graphicsView;
        }

        // public render API
        public void Render(IEnumerable<GuiElement> elements)
        {
            var elementList = elements?.ToList() ?? new();
            _drawable.Elements = elementList;

            // ノードの数から必要な高さを計算する
            const double nodeH = 50, spacing = 80, startY = 5;

            double requiredHeight = 0;
            if (elementList.Count > 0)
            {
                // 最後のノードの上端Y座標を計算
                double lastNodeY = startY + (elementList.Count - 1) * spacing;

                // 最後のノードの下端Y座標を計算
                double lastNodeBottom = lastNodeY + nodeH;

                // 最後のノードの下にも少し余白 (例: 50) を加える
                requiredHeight = lastNodeBottom + 50;

                requiredHeight = Math.Max(requiredHeight, 100);
            }
            else
            {
                // 要素がない場合は最低限の高さ
                requiredHeight = 100;
            }

            // 計算された高さをHeightRequestに設定
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
            // 背景を描画
            canvas.FillColor = Colors.White;
            canvas.FillRectangle(dirtyRect);

            if (Elements == null || Elements.Count == 0) return;

            // レイアウト定数
            float nodeW = 160, nodeH = 50, spacing = 80;
            float startX = 50, startY = 5;

            // ノードの位置を計算
            var positions = new Dictionary<string, PointF>();
            for (int i = 0; i < Elements.Count; i++)
                positions[Elements[i].Name] = new PointF(startX, startY + i * spacing);

            // draw connections (接続線)
            canvas.StrokeColor = Colors.Gray;
            canvas.StrokeSize = 2;
            foreach (var el in Elements)
            {
                // Targetが設定されており、かつTargetがpositionsに存在するノードに対して線を描画
                if (!string.IsNullOrEmpty(el.Target) && positions.ContainsKey(el.Target))
                {
                    var f = positions[el.Name];
                    var t = positions[el.Target];
                    // 接続点はノードの右端中央と左端中央
                    var s = new PointF(f.X + nodeW, f.Y + nodeH / 2);
                    var e = new PointF(t.X, t.Y + nodeH / 2);

                    canvas.DrawLine(s, e);
                    DrawArrow(canvas, s, e);
                }
            }

            // draw nodes (ノード本体)
            foreach (var el in Elements)
            {
                var p = positions[el.Name];
                var r = new RectF(p.X, p.Y, nodeW, nodeH);

                // 1. ノードの色を設定
                canvas.FillColor = el.Type switch
                {
                    GuiElementType.Screen => Colors.Lavender,
                    GuiElementType.Button => Colors.LightBlue,
                    GuiElementType.Event => Colors.LightGreen,
                    GuiElementType.Timeout => Colors.Pink,
                    GuiElementType.Operation => Colors.LightGreen, // OperationはEventと同じ色
                    _ => Colors.LightGray
                };

                // 2. Typeに応じて異なる図形を描画
                canvas.StrokeColor = Colors.Black;

                switch (el.Type)
                {
                    case GuiElementType.Screen:
                        // スクリーン: 角丸長方形
                        canvas.FillRoundedRectangle(r, 8);
                        canvas.DrawRoundedRectangle(r, 8);
                        break;

                    case GuiElementType.Button:
                        // ボタン: 楕円（円）
                        canvas.FillEllipse(r);
                        canvas.DrawEllipse(r);
                        break;

                    case GuiElementType.Event:
                    case GuiElementType.Operation: // OperationはEventと同じ図形
                        // イベント/Operation: ひし形
                        using (var path = new PathF())
                        {
                            path.MoveTo(r.X + r.Width / 2, r.Y);      // 上
                            path.LineTo(r.Right, r.Y + r.Height / 2); // 右
                            path.LineTo(r.X + r.Width / 2, r.Bottom); // 下
                            path.LineTo(r.X, r.Y + r.Height / 2);     // 左
                            path.Close();
                            canvas.FillPath(path);
                            canvas.DrawPath(path);
                        }
                        break;

                    case GuiElementType.Timeout:
                        // タイムアウト: 長方形
                        canvas.FillRectangle(r);
                        canvas.DrawRectangle(r);
                        break;

                    default:
                        // その他: 角丸長方形
                        canvas.FillRoundedRectangle(r, 8);
                        canvas.DrawRoundedRectangle(r, 8);
                        break;
                }

                // 3. テキストを描画
                canvas.FontColor = Colors.Black;
                canvas.FontSize = 14;
                canvas.DrawString(el.Name, r, HorizontalAlignment.Center, VerticalAlignment.Center);
            }
        }

        private void DrawArrow(ICanvas canvas, PointF start, PointF end)
        {
            float size = 6f;
            // 接続線と同じ色で矢印を描画
            canvas.StrokeColor = Colors.Gray;
            canvas.StrokeSize = 2;
            float angle = MathF.Atan2(end.Y - start.Y, end.X - start.X);
            var p1 = new PointF(end.X - size * MathF.Cos(angle - 0.3f), end.Y - size * MathF.Sin(angle - 0.3f));
            var p2 = new PointF(end.X - size * MathF.Cos(angle + 0.3f), end.Y - size * MathF.Sin(angle + 0.3f));
            canvas.DrawLine(end, p1);
            canvas.DrawLine(end, p2);
        }
    }
}