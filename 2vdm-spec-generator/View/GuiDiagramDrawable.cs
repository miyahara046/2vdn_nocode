using Microsoft.Maui.Graphics;
using _2vdm_spec_generator.ViewModel;
using System.Collections.Generic;

namespace _2vdm_spec_generator.View
{
    public class GuiDiagramDrawable : IDrawable
    {
        public List<GuiElement> Elements { get; set; } = new();

        private const float nodeW = 160;
        private const float nodeH = 50;
        private const float spacing = 80;
        private const float startX = 50;
        private const float startY = 5;

        public void ArrangeNodes()
        {
            float timeoutY = startY;

            // タイムアウトは上部固定
            foreach (var el in Elements.Where(e => e.Type == GuiElementType.Timeout))
            {
                el.X = startX;
                el.Y = timeoutY;
                timeoutY += nodeH + 10;
            }

            // Screen ノードは左側
            int screenIndex = 0;
            foreach (var el in Elements.Where(e => e.Type == GuiElementType.Screen))
            {
                el.X = startX;
                el.Y = timeoutY + screenIndex * spacing;
                screenIndex++;
            }

            // Button ノードは左下
            int buttonIndex = 0;
            foreach (var el in Elements.Where(e => e.Type == GuiElementType.Button))
            {
                el.X = startX;
                el.Y = timeoutY + screenIndex * spacing + buttonIndex * spacing;
                buttonIndex++;
            }

            // Event ノードは右側
            int eventIndex = 0;
            foreach (var el in Elements.Where(e => e.Type == GuiElementType.Event || e.Type == GuiElementType.Operation))
            {
                el.X = startX + 300; // 右側
                el.Y = timeoutY + eventIndex * spacing;
                eventIndex++;
            }
        }

        public void Draw(ICanvas canvas, RectF dirtyRect)
        {
            canvas.FillColor = Colors.White;
            canvas.FillRectangle(dirtyRect);

            if (Elements == null || Elements.Count == 0) return;

            // ノードの位置が未設定なら初期化
            if (!Elements.Any(e => e.X != 0 || e.Y != 0))
                ArrangeNodes();

            var positions = Elements.ToDictionary(e => e.Name, e => new PointF(e.X, e.Y));

            // 接続線
            canvas.StrokeColor = Colors.Gray;
            canvas.StrokeSize = 2;
            foreach (var el in Elements)
            {
                if (!string.IsNullOrEmpty(el.Target) && positions.ContainsKey(el.Target))
                {
                    var f = positions[el.Name];
                    var t = positions[el.Target];

                    var s = new PointF(f.X + nodeW, f.Y + nodeH / 2);
                    var eP = new PointF(t.X, t.Y + nodeH / 2);

                    canvas.DrawLine(s, eP);
                    DrawArrow(canvas, s, eP);
                }
            }

            // ノード描画
            foreach (var el in Elements)
            {
                var p = positions[el.Name];
                // ノード描画
                var r = new RectF(el.X, el.Y, nodeW, nodeH);


                canvas.FillColor = el.Type switch
                {
                    GuiElementType.Screen => Colors.Lavender,
                    GuiElementType.Button => Colors.LightBlue,
                    GuiElementType.Event => Colors.LightGreen,
                    GuiElementType.Timeout => Colors.Pink,
                    GuiElementType.Operation => Colors.LightGreen,
                    _ => Colors.LightGray
                };

                canvas.StrokeColor = Colors.Black;

                switch (el.Type)
                {
                    case GuiElementType.Screen:
                        canvas.FillRoundedRectangle(r, 8);
                        canvas.DrawRoundedRectangle(r, 8);
                        break;
                    case GuiElementType.Button:
                        canvas.FillEllipse(r);
                        canvas.DrawEllipse(r);
                        break;
                    case GuiElementType.Event:
                    case GuiElementType.Operation:
                        using (var path = new PathF())
                        {
                            path.MoveTo(r.X + r.Width / 2, r.Y);
                            path.LineTo(r.Right, r.Y + r.Height / 2);
                            path.LineTo(r.X + r.Width / 2, r.Bottom);
                            path.LineTo(r.X, r.Y + r.Height / 2);
                            path.Close();
                            canvas.FillPath(path);
                            canvas.DrawPath(path);
                        }
                        break;
                    case GuiElementType.Timeout:
                        canvas.FillRectangle(r);
                        canvas.DrawRectangle(r);
                        break;
                    default:
                        canvas.FillRoundedRectangle(r, 8);
                        canvas.DrawRoundedRectangle(r, 8);
                        break;
                }

                canvas.FontColor = Colors.Black;
                canvas.FontSize = 14;
                canvas.DrawString(el.Name, r, HorizontalAlignment.Center, VerticalAlignment.Center);
            }
        }

        private void DrawArrow(ICanvas canvas, PointF start, PointF end)
        {
            float size = 6f;
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
