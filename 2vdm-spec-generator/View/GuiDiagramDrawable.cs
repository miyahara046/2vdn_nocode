using Microsoft.Maui.Graphics;
using _2vdm_spec_generator.ViewModel;
using System.Collections.Generic;
using System.Linq;

namespace _2vdm_spec_generator.View
{
    /// <summary>
    /// GUI 図（ノードと線）の描画処理を担当するクラス。
    /// 
    /// - ノード（Screen/Button/Event/Timeout/Operation）をキャンバスに描画する
    /// - ノードの初期配置（位置が 0,0 の場合に自動レイアウト）
    /// - ノードとノードをつなぐ線（矢印）の描画
    /// 
    /// IDrawable を実装しており、GraphicsView で使用される。
    /// </summary>
    public class GuiDiagramDrawable : IDrawable
    {
        // 描画対象のノード（外部の ViewModel から渡される）
        public List<GuiElement> Elements { get; set; } = new();

        // 他の場所でも使いたいので public const として幅・高さを定義
        public const float NodeWidth = 160f;
        public const float NodeHeight = 50f;

        // ==== レイアウトに使う値 ====
        private const float spacing = 80f;         // ノード間の縦方向スペース
        private const float leftColumnX = 40f;     // 左列の X 座標（Screen / Button / Operation）
        private const float rightColumnX = 200f;   // 右列の X（Event の位置）
        private const float timeoutStartX = 40f;   // Timeout の X 座標（上固定）
        private const float timeoutStartY = 8f;    // Timeout の最初の Y 座標

        /// <summary>
        /// 初期位置が未設定（X=0, Y=0）の要素に自動的に位置を割り当てる。
        /// 
        /// 初回描画時、すべてのノードの位置が 0,0 の場合に実行される。
        /// ※ ユーザーが移動したノードは上書きしない。
        /// </summary>
        public void ArrangeNodes()
        {
            float timeoutY = timeoutStartY;

            // ==== 1) Timeout ノードは上から順に固定配置 ====
            foreach (var el in Elements.Where(e => e.Type == GuiElementType.Timeout))
            {
                el.IsFixed = true;         // 移動不可にする（論理的なフラグ）
                el.X = timeoutStartX;
                el.Y = timeoutY;

                // 縦に積む
                timeoutY += NodeHeight + 10f;
            }

            // ==== 2) Screen は左列に縦並び ====
            int screenIndex = 0;
            foreach (var el in Elements.Where(e => e.Type == GuiElementType.Screen))
            {
                if (IsUnpositioned(el)) // X=0, Y=0 を未配置とみなす
                {
                    el.X = leftColumnX;
                    el.Y = timeoutY + screenIndex * spacing;
                }
                screenIndex++;
            }

            // ==== 3) Button は Screen の下に続けて縦並び ====
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

            // ==== 4) Operation も Button の下に配置 ====
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

            // ==== 5) Event は右列に縦並び ====
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

        /// <summary>
        /// ノードが未配置（X=0 かつ Y=0）の場合に true を返す。
        /// </summary>
        private static bool IsUnpositioned(GuiElement e) => e.X == 0 && e.Y == 0;

        /// <summary>
        /// GraphicsView により呼び出される描画メソッド。
        /// 
        /// - 背景描画
        /// - ノードの自動配置チェック
        /// - ノード間の線（矢印）描画
        /// - ノード（形状と色）描画
        /// </summary>
        public void Draw(ICanvas canvas, RectF dirtyRect)
        {
            // === 背景を白で塗りつぶす ===
            canvas.FillColor = Colors.White;
            canvas.FillRectangle(dirtyRect);

            // 描画対象が何もなければ終了
            if (Elements == null || Elements.Count == 0) return;

            // === 初回描画時：全ノードが 0,0 のときだけ初期配置を行う ===
            if (!Elements.Any(e => e.X != 0 || e.Y != 0))
                ArrangeNodes();

            // === 名前をキーに位置辞書を作成（線を引くときに使う） ===
            var positions = Elements.Where(e => !string.IsNullOrWhiteSpace(e.Name))
                                    .ToDictionary(e => e.Name, e => new PointF(e.X, e.Y));

            // === ノード間の線（矢印）描画 ===
            canvas.StrokeColor = Colors.Gray;
            canvas.StrokeSize = 2;

            foreach (var el in Elements)
            {
                // Target が設定されていて、辞書に位置情報がある場合
                if (!string.IsNullOrEmpty(el.Target) &&
                    positions.ContainsKey(el.Target) &&
                    positions.ContainsKey(el.Name))
                {
                    var f = positions[el.Name];
                    var t = positions[el.Target];

                    // 線の start は右側中央、end は左側中央
                    var s = new PointF(f.X + NodeWidth, f.Y + NodeHeight / 2f);
                    var eP = new PointF(t.X, t.Y + NodeHeight / 2f);

                    canvas.DrawLine(s, eP);
                    DrawArrow(canvas, s, eP); // 矢印先端を描く
                }
            }

            // === ノード描画 ===
            foreach (var el in Elements)
            {
                var r = new RectF(el.X, el.Y, NodeWidth, NodeHeight);

                // 種類ごとの背景色
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

                // === ノードの形状を種類ごとに描画 ===
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
                        // ダイヤ型（Event / Operation）
                        using (var path = new PathF())
                        {
                            path.MoveTo(r.X + r.Width / 2f, r.Y);               // 上
                            path.LineTo(r.Right, r.Y + r.Height / 2f);         // 右
                            path.LineTo(r.X + r.Width / 2f, r.Bottom);        // 下
                            path.LineTo(r.X, r.Y + r.Height / 2f);            // 左
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

                // === 選択されているノードはオレンジ枠で強調表示 ===
                if (el.IsSelected)
                {
                    canvas.StrokeColor = Colors.Orange;
                    canvas.StrokeSize = 3;

                    // 少し大きめの枠を描くために Expand を使用
                    canvas.DrawRoundedRectangle(r.Expand(4), 8);

                    // 元の設定に戻す
                    canvas.StrokeSize = 2;
                    canvas.StrokeColor = Colors.Black;
                }

                // === ノード中央に名前を描画 ===
                canvas.FontColor = Colors.Black;
                canvas.FontSize = 14;
                canvas.DrawString(el.Name ?? "", r,
                    HorizontalAlignment.Center, VerticalAlignment.Center);
            }
        }

        /// <summary>
        /// ノード間の線の先端に矢印を描画する。
        /// 三角形ではなく線 2 本で表現している。
        /// </summary>
        private void DrawArrow(ICanvas canvas, PointF start, PointF end)
        {
            float size = 6f; // 矢印の大きさ
            canvas.StrokeColor = Colors.Gray;
            canvas.StrokeSize = 2;

            // 線分の角度を計算
            float angle = MathF.Atan2(end.Y - start.Y, end.X - start.X);

            // 左右の羽根の位置を計算
            var p1 = new PointF(end.X - size * MathF.Cos(angle - 0.3f),
                                end.Y - size * MathF.Sin(angle - 0.3f));
            var p2 = new PointF(end.X - size * MathF.Cos(angle + 0.3f),
                                end.Y - size * MathF.Sin(angle + 0.3f));

            canvas.DrawLine(end, p1);
            canvas.DrawLine(end, p2);
        }
    }

    /// <summary>
    /// RectF を拡張するメソッド。
    /// ノードの選択枠（少し大きめの四角）を描くために使用される。
    /// </summary>
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
