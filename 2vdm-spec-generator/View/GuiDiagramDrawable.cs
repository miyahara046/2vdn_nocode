using Microsoft.Maui.Graphics;
using _2vdm_spec_generator.ViewModel;
using System.Collections.Generic;
using System.Linq;

namespace _2vdm_spec_generator.View
{
    /// <summary>
    /// GUI 図（ノード＋ノード間のエッジ）の描画を担う IDrawable 実装。
    /// </summary>
    public class GuiDiagramDrawable : IDrawable
    {
        public List<GuiElement> Elements { get; set; } = new();

        public const float NodeWidth = 160f;
        public const float NodeHeight = 50f;

        // ==== レイアウトパラメータ ===
        private const float spacing = 80f;
        private const float leftColumnX = 40f;
        private const float rightColumnX = 200f;
        private const float timeoutStartX = 40f;
        private const float timeoutStartY = 8f;

        // タイムアウト隣接イベント用の X オフセット（タイムアウトの右側に表示）
        private const float timeoutEventOffset = NodeWidth + 10f;

        public void ArrangeNodes()
        {
            float timeoutY = timeoutStartY;

            // 1) Timeout 固定配置
            foreach (var el in Elements.Where(e => e.Type == GuiElementType.Timeout))
            {
                el.IsFixed = true;
                el.X = timeoutStartX;
                el.Y = timeoutY;
                timeoutY += NodeHeight + 10f;
            }

            // 2) Screen を左列に配置（未配置のみ）
            int screenIndex = 0;
            foreach (var el in Elements.Where(e => e.Type == GuiElementType.Screen))
            {
                if (IsUnpositioned(el))
                {
                    el.X = leftColumnX;
                    el.Y = timeoutY + screenIndex * spacing;
                }
                screenIndex++;
            }

            // 3) Button を配置
            int buttonIndex = 0;
            foreach (var el in Elements.Where(e => e.Type == GuiElementType.Button))
            {
                if (IsUnpositioned(el))
                {
                    el.X = leftColumnX;
                    el.Y = timeoutY + screenIndex * spacing + buttonIndex * spacing;
                }
                buttonIndex++;
            }

            // 4) Operation を配置
            int opIndex = 0;
            foreach (var el in Elements.Where(e => e.Type == GuiElementType.Operation))
            {
                if (IsUnpositioned(el))
                {
                    el.X = leftColumnX;
                    el.Y = timeoutY + screenIndex * spacing + (buttonIndex + opIndex) * spacing;
                }
                opIndex++;
            }

            // 5) Event のうち、Timeout に紐づくものはタイムアウトの横に並べる
            var timeoutsByName = Elements.Where(e => e.Type == GuiElementType.Timeout && !string.IsNullOrWhiteSpace(e.Name))
                                         .ToDictionary(e => e.Name, e => e);
            foreach (var evt in Elements.Where(e => e.Type == GuiElementType.Event))
            {
                if (!string.IsNullOrWhiteSpace(evt.Target) && timeoutsByName.TryGetValue(evt.Target, out var timeoutEl))
                {
                    // タイムアウトの横に表示し、Y を同期、X はタイムアウトの右側
                    evt.X = timeoutEl.X + timeoutEventOffset;
                    evt.Y = timeoutEl.Y;
                    evt.IsFixed = true; // 移動不可（必要なら true にする）
                }
            }

            // 6) それ以外の Event は右列に縦並び（未配置のみ）
            int eventIndex = 0;
            foreach (var el in Elements.Where(e => e.Type == GuiElementType.Event))
            {
                if (IsUnpositioned(el))
                {
                    el.X = rightColumnX;
                    el.Y = timeoutY + eventIndex * spacing;
                }
                eventIndex++;
            }
        }

        private static bool IsUnpositioned(GuiElement e) => e.X == 0 && e.Y == 0;

        public void Draw(ICanvas canvas, RectF dirtyRect)
        {
            canvas.FillColor = Colors.White;
            canvas.FillRectangle(dirtyRect);

            if (Elements == null || Elements.Count == 0) return;

            if (!Elements.Any(e => e.X != 0 || e.Y != 0))
                ArrangeNodes();

            var positions = Elements.Where(e => !string.IsNullOrWhiteSpace(e.Name))
                                    .ToDictionary(e => e.Name, e => new PointF(e.X, e.Y));

            // 線（矢印）描画
            canvas.StrokeColor = Colors.Gray;
            canvas.StrokeSize = 2;

            foreach (var el in Elements)
            {
                if (!string.IsNullOrEmpty(el.Target) &&
                    positions.ContainsKey(el.Target) &&
                    positions.ContainsKey(el.Name))
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

                // タイムアウトに紐づくイベント判定
                var attachedToTimeout = !string.IsNullOrWhiteSpace(el.Target) &&
                                         Elements.Any(t => t.Type == GuiElementType.Timeout && t.Name == el.Target);

                // 背景色決定（タイムアウトに紐づくイベントはタイムアウト色）
                if (el.Type == GuiElementType.Timeout)
                {
                    canvas.FillColor = Colors.Pink;
                }
                else if (el.Type == GuiElementType.Button)
                {
                    canvas.FillColor = Colors.LightBlue;
                }
                else if (el.Type == GuiElementType.Event && attachedToTimeout)
                {
                    canvas.FillColor = Colors.Pink; // タイムアウトと同色
                }
                else if (el.Type == GuiElementType.Event || el.Type == GuiElementType.Operation)
                {
                    canvas.FillColor = Colors.LightGreen;
                }
                else if (el.Type == GuiElementType.Screen)
                {
                    canvas.FillColor = Colors.Lavender;
                }
                else
                {
                    canvas.FillColor = Colors.LightGray;
                }

                canvas.StrokeColor = Colors.Black;

                // 形状描画
                if (el.Type == GuiElementType.Screen)
                {
                    canvas.FillRoundedRectangle(r, 8);
                    canvas.DrawRoundedRectangle(r, 8);
                }
                else if (el.Type == GuiElementType.Button)
                {
                    canvas.FillEllipse(r);
                    canvas.DrawEllipse(r);
                }
                else if (el.Type == GuiElementType.Event && attachedToTimeout)
                {
                    // タイムアウト紐づきイベントは楕円で描画（タイムアウトと同色）
                    canvas.FillRectangle(r);
                    canvas.DrawRectangle(r);
                }
                else if (el.Type == GuiElementType.Event || el.Type == GuiElementType.Operation)
                {
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
                }
                else if (el.Type == GuiElementType.Timeout)
                {
                    canvas.FillRectangle(r);
                    canvas.DrawRectangle(r);
                }
                else
                {
                    canvas.FillRoundedRectangle(r, 8);
                    canvas.DrawRoundedRectangle(r, 8);
                }

                // 選択表示（形状に応じて枠を描く）
                if (el.IsSelected)
                {
                    canvas.StrokeColor = Colors.Orange;
                    canvas.StrokeSize = 3;

                    if (el.Type == GuiElementType.Button || (el.Type == GuiElementType.Event && attachedToTimeout))
                    {
                        // 楕円系の選択枠
                        canvas.DrawEllipse(r.Expand(4));
                    }
                    else if (el.Type == GuiElementType.Screen)
                    {
                        canvas.DrawRoundedRectangle(r.Expand(4), 8);
                    }
                    else
                    {
                        // デフォルトは角丸矩形枠
                        canvas.DrawRoundedRectangle(r.Expand(4), 8);
                    }

                    canvas.StrokeSize = 2;
                    canvas.StrokeColor = Colors.Black;
                }

                // ラベル描画
                canvas.FontColor = Colors.Black;
                canvas.FontSize = 14;
                canvas.DrawString(el.Name ?? "", r,
                    HorizontalAlignment.Center, VerticalAlignment.Center);
            }
        }

        private void DrawArrow(ICanvas canvas, PointF start, PointF end)
        {
            float size = 6f;
            canvas.StrokeColor = Colors.Gray;
            canvas.StrokeSize = 2;

            float angle = MathF.Atan2(end.Y - start.Y, end.X - start.X);

            var p1 = new PointF(end.X - size * MathF.Cos(angle - 0.3f),
                                end.Y - size * MathF.Sin(angle - 0.3f));
            var p2 = new PointF(end.X - size * MathF.Cos(angle + 0.3f),
                                end.Y - size * MathF.Sin(angle + 0.3f));

            canvas.DrawLine(end, p1);
            canvas.DrawLine(end, p2);
        }
    }

    public static class RectFExtensions
    {
        public static RectF Expand(this RectF rect, float margin)
        {
            return new RectF(
                rect.Left - margin,
                rect.Top - margin,
                rect.Width + margin * 2,
                rect.Height + margin * 2
            );
        }
    }
}
