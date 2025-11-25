using Microsoft.Maui.Graphics;
using _2vdm_spec_generator.ViewModel;
using System.Collections.Generic;
using System.Linq;

namespace _2vdm_spec_generator.View
{
    /// <summary>
    /// ノードの描画処理（色・形・位置）。配置ロジックの一部（初期配置）もここに置いてあります。
    /// </summary>
    public class GuiDiagramDrawable : IDrawable
    {
        public List<GuiElement> Elements { get; set; } = new();

        // 他所から参照する定数
        public const float NodeWidth = 160f;
        public const float NodeHeight = 50f;

        // レイアウト定数（必要に応じて調整）
        private const float spacing = 80f;
        private const float leftColumnX = 40f;    // Screen / Button / Operation の固定 X
        private const float rightColumnX = 350f;  // Event の固定 X
        private const float timeoutStartX = 40f;  // Timeout 固定 X
        private const float timeoutStartY = 8f;   // Timeout の開始 Y

        /// <summary>
        /// 初期配置／既定位置が無いノードに対して初期位置を割り当てます。
        /// ※ 位置が 0,0 のノードを未配置とみなす仕様です（既に位置がある場合は上書きしません）。 
        /// </summary>
        public void ArrangeNodes()
        {
            float timeoutY = timeoutStartY;

            // 1) タイムアウトは上部に固定（上から縦に積む）
            foreach (var el in Elements.Where(e => e.Type == GuiElementType.Timeout))
            {
                el.IsFixed = true;
                el.X = timeoutStartX;
                el.Y = timeoutY;
                timeoutY += NodeHeight + 10f;
            }

            // 2) Screen は左側（左列に縦配置）
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

            // 3) Button は左列の下に続ける
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

            // 4) Operation も左側（必要ならボタンと同列）
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

            // 5) Event は右側
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
            // 背景
            canvas.FillColor = Colors.White;
            canvas.FillRectangle(dirtyRect);

            if (Elements == null || Elements.Count == 0) return;

            // 初回配置（全部が (0,0) なら ArrangeNodes を実行）
            if (!Elements.Any(e => e.X != 0 || e.Y != 0))
                ArrangeNodes();

            // 名前をキーに位置辞書（接続線描画に使う）
            var positions = Elements.Where(e => !string.IsNullOrWhiteSpace(e.Name))
                                    .ToDictionary(e => e.Name, e => new PointF(e.X, e.Y));

            // 接続線（矢印付き）
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

                // 選択中は枠を強調（アウトライン用に拡大した角丸を描く）
                if (el.IsSelected)
                {
                    canvas.StrokeColor = Colors.Orange;
                    canvas.StrokeSize = 3;
                    canvas.DrawRoundedRectangle(r.Expand(4), 8);
                    canvas.StrokeSize = 2;
                    canvas.StrokeColor = Colors.Black;
                }

                // テキスト
                canvas.FontColor = Colors.Black;
                canvas.FontSize = 14;
                canvas.DrawString(el.Name ?? "", r, HorizontalAlignment.Center, VerticalAlignment.Center);
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
