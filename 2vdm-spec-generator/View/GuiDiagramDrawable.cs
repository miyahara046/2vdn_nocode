using Microsoft.Maui.Graphics;
using _2vdm_spec_generator.ViewModel;
using System.Collections.Generic;
using System.Linq;

namespace _2vdm_spec_generator.View
{
    public class GuiDiagramDrawable : IDrawable
    {
        public List<GuiElement> Elements { get; set; } = new();
        public Action<GuiElement>? NotifyPositionChanged { get; set; }


        // 外部参照用に public にする
        public const float NodeWidth = 160f;
        public const float NodeHeight = 50f;
        private const float spacing = 80f;
        private const float startX = 50f;
        private const float startY = 5f;

        public void ArrangeNodes()
        {
            float timeoutY = startY;

            // タイムアウトは上部固定
            foreach (var el in Elements.Where(e => e.Type == GuiElementType.Timeout))
            {
                el.X = startX;
                el.Y = timeoutY;
                timeoutY += NodeHeight + 10f;
            }

            // Screen ノードは左側
            int screenIndex = 0;
            foreach (var el in Elements.Where(e => e.Type == GuiElementType.Screen))
            {
                el.X = startX;
                el.Y = timeoutY + screenIndex * spacing;
                screenIndex++;
            }

            // Button ノードは左下（screen の下に続ける）
            int buttonIndex = 0;
            foreach (var el in Elements.Where(e => e.Type == GuiElementType.Button))
            {
                el.X = startX;
                el.Y = timeoutY + screenIndex * spacing + buttonIndex * spacing;
                buttonIndex++;
            }

            // Event / Operation は右側
            int eventIndex = 0;
            foreach (var el in Elements.Where(e => e.Type == GuiElementType.Event || e.Type == GuiElementType.Operation))
            {
                el.X = startX + 300f; // 右側オフセット
                el.Y = timeoutY + eventIndex * spacing;
                eventIndex++;
            }
        }

        public void Draw(ICanvas canvas, RectF dirtyRect)
        {
            canvas.FillColor = Colors.White;
            canvas.FillRectangle(dirtyRect);

            if (Elements == null || Elements.Count == 0) return;

            // 初期配置が未設定なら配置
            if (!Elements.Any(e => e.X != 0 || e.Y != 0))
                ArrangeNodes();

            var positions = Elements.ToDictionary(e => e.Name, e => new PointF(e.X, e.Y));

            // 接続線
            canvas.StrokeColor = Colors.Gray;
            canvas.StrokeSize = 2;
            foreach (var el in Elements)
            {
                if (!string.IsNullOrEmpty(el.Target) && positions.ContainsKey(el.Target) && positions.ContainsKey(el.Name))
                {
                    var f = positions[el.Name];
                    var t = positions[el.Target];

                    var s = new PointF(f.X + NodeWidth, f.Y + NodeHeight / 2f);
                    var eP = new PointF(t.X, t.Y + NodeHeight / 2f);

                    canvas.DrawLine(s, eP);
                    DrawArrow(canvas, s, eP);
                }
            }

            // ノード描画
            foreach (var el in Elements)
            {
                var r = new RectF(el.X, el.Y, NodeWidth, NodeHeight);

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
                            path.MoveTo(r.X + r.Width / 2f, r.Y);
                            path.LineTo(r.Right, r.Y + r.Height / 2f);
                            path.LineTo(r.X + r.Width / 2f, r.Bottom);
                            path.LineTo(r.X, r.Y + r.Height / 2f);
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
