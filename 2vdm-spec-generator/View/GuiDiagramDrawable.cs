using Microsoft.Maui.Graphics;
using _2vdm_spec_generator.ViewModel;
using System.Collections.Generic;
using System.Linq;

namespace _2vdm_spec_generator.View
{
    /// <summary>
    /// GUI 図（ノード＋ノード間のエッジ）の描画を担う IDrawable 実装。
    /// 
    /// 対象読者：
    /// - C# の言語設計や UI 描画の基礎を知る「C# 諸学者」を想定し、アルゴリズム的観点や設計上の注意点を注記している。
    /// 
    /// 責務（責任分離）:
    /// - ViewModel 側が持つ `GuiElement` コレクションを受け取り、GraphicsView に必要な描画を実行する。
    /// - 自動レイアウト（単純ヒューリスティック）を必要に応じて行うが、高度なグラフレイアウトは行わない。
    /// - 線（エッジ）は element.Target を用いて単純に直線で結び、先端に矢羽を描く。
    /// 
    /// 設計上の重要点（学術的/工学的観点）:
    /// - 描画は UI スレッド（GraphicsView の描画コールバック）で実行される前提。Elements が別スレッドで更新される可能性がある場合、
    ///   呼び出し元で防護（ディスパッチ、コピー等）すること。ここでは同期処理は行っていない（副作用を避けるため）。
    /// - 自動レイアウトは寿命が短く、簡潔なルールベース（列配置）を用いる。大規模なノードや複雑な DAG の場合は別アルゴリズム（Sugiyama 法や力学法）を推奨。
    /// - 名前(Name)をキーにして線を結ぶため、Name の一意性が保証されないと期待する線が引けない（仕様上の仮定）。
    /// - 計算量: ArrangeNodes / Draw の主処理は各要素を 1 回走査するため O(N)。線描画のための辞書構築も O(N)（ただしハッシュ操作は平均 O(1)）。
    /// </summary>
    public class GuiDiagramDrawable : IDrawable
    {
        /// <summary>
        /// 描画対象ノード群（ViewModel から設定されることを想定する可変リスト）。
        /// 注意:
        /// - Draw 呼び出し中にこのリストが変更されると不定動作の原因となるため、スレッド競合を避けるには呼び出し側でコピーを渡すか Dispatcher 経由で同期すること。
        /// </summary>
        public List<GuiElement> Elements { get; set; } = new();

        // ノード矩形の共通定義（他クラスと数値を合わせるため public const）
        public const float NodeWidth = 160f;
        public const float NodeHeight = 50f;

        // ==== レイアウトパラメータ ====
        // これらは現在の単純レイアウトを決める定数。将来的にユーザー設定やレスポンシブ対応に外出し可能。
        private const float spacing = 80f;         // ノード間の垂直スペース（ピクセル）
        private const float leftColumnX = 40f;     // 左列 X 座標（Screen/Button/Operation 用）
        private const float rightColumnX = 200f;   // 右列 X 座標（Event 用）
        private const float timeoutStartX = 40f;   // Timeout ノードの X（上固定）
        private const float timeoutStartY = 8f;    // Timeout の開始 Y

        /// <summary>
        /// 初期配置ルーチン:
        /// - X=0,Y=0 を未配置と見なして自動配置を試みる。
        /// - Timeout を上部に固定し、その下から Screen→Button→Operation を左列に、Event を右列に配置する。
        /// 
        /// アルゴリズム的特徴:
        /// - 単純で決定性あり。計算量は O(N)（各要素を一巡する）。
        /// - 衝突回避や重なり解消は行わない（重なりが生じた場合、別段の reflow/packing アルゴリズムが必要）。
        /// - 「既に配置済みの要素は上書きしない」ポリシーを取っている（ユーザー操作で位置が決まったものを尊重）。
        /// </summary>
        public void ArrangeNodes()
        {
            float timeoutY = timeoutStartY;

            // ==== 1) Timeout ノードは上から順に固定配置 ====
            // Timeout は特別扱い：常に上部に集めて固定ノード（IsFixed=true とする）
            foreach (var el in Elements.Where(e => e.Type == GuiElementType.Timeout))
            {
                el.IsFixed = true; // 論理フラグとして移動不可を示す
                el.X = timeoutStartX;
                el.Y = timeoutY;

                // タイムアウト同士は NodeHeight + 10 の間隔で積む（固定間隔）
                timeoutY += NodeHeight + 10f;
            }

            // ==== 2) Screen を左列に縦並び ====
            // timeoutY の下から配置を始めることで、タイムアウトが常に視界上部にあるようにする
            int screenIndex = 0;
            foreach (var el in Elements.Where(e => e.Type == GuiElementType.Screen))
            {
                if (IsUnpositioned(el)) // 未配置のものだけ初期位置を与える
                {
                    el.X = leftColumnX;
                    el.Y = timeoutY + screenIndex * spacing;
                }
                screenIndex++;
            }

            // ==== 3) Button を Screen の下に続けて配置 ====
            int buttonIndex = 0;
            foreach (var el in Elements.Where(e => e.Type == GuiElementType.Button))
            {
                if (IsUnpositioned(el))
                {
                    // Screen の末尾から継続して配置する設計
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
        /// 未配置判定（簡易定義: X==0 && Y==0 を未配置とする）
        /// - 実務的には NaN や負値、特別フラグを使う方が堅牢になる場合がある。
        /// </summary>
        private static bool IsUnpositioned(GuiElement e) => e.X == 0 && e.Y == 0;

        /// <summary>
        /// 描画エントリポイント（GraphicsView が呼ぶ）。
        /// 
        /// 手順:
        /// 1. 背景塗りつぶし
        /// 2. 初期配置（全要素が未配置のときのみ）
        /// 3. 名前ベースの位置辞書作成（線描画用）
        /// 4. 線（直線）と矢印描画
        /// 5. 各ノードの描画（形状・色・ラベル・選択枠）
        /// 
        /// 注意点:
        /// - Name をキーに位置辞書を作るため Name の一意性を前提としている（重複があると線が欠ける）。
        /// - 描画中に Elements が変わると Dictionary の構築や Draw のループで例外や不整合が起きうる。
        ///   必要なら描画開始時に要素の shallow copy を作る実装が望ましい（ここでは行っていない）。
        /// </summary>
        public void Draw(ICanvas canvas, RectF dirtyRect)
        {
            // 背景を白で塗りつぶす（キャンバスクリアの意味合い）
            canvas.FillColor = Colors.White;
            canvas.FillRectangle(dirtyRect);

            // 対象がなければ早期終了（無駄な処理を避ける）
            if (Elements == null || Elements.Count == 0) return;

            // 初回描画時の自動配置判定:
            // - 全要素が (0,0) の場合だけ ArrangeNodes を呼び出す（部分的に未配置のものは上書きしない）
            if (!Elements.Any(e => e.X != 0 || e.Y != 0))
                ArrangeNodes();

            // 名前をキーにした位置辞書を作成（線を引くときのソース/ターゲット検出に使う）
            // Dictionary 作成は O(N) だが、キーが重複すると ArgumentException になるため
            // 事前に Name の一意性を保証するか、TryAdd ベースの処理に変更することができる。
            var positions = Elements.Where(e => !string.IsNullOrWhiteSpace(e.Name))
                                    .ToDictionary(e => e.Name, e => new PointF(e.X, e.Y));

            // === ノード間の線（矢印）描画 ===
            canvas.StrokeColor = Colors.Gray;
            canvas.StrokeSize = 2;

            foreach (var el in Elements)
            {
                // Target が設定されており、かつ positions 辞書に両側が存在する場合にのみ線を引く
                if (!string.IsNullOrEmpty(el.Target) &&
                    positions.ContainsKey(el.Target) &&
                    positions.ContainsKey(el.Name))
                {
                    var f = positions[el.Name];
                    var t = positions[el.Target];

                    // 直線の始点はソースの右中央 (f.X + NodeWidth, f.Y + NodeHeight/2)
                    var s = new PointF(f.X + NodeWidth, f.Y + NodeHeight / 2f);
                    // 終点はターゲットの左中央 (t.X, t.Y + NodeHeight/2)
                    var eP = new PointF(t.X, t.Y + NodeHeight / 2f);

                    // 直線を描く。将来、曲線（ベジェ）やオフセットを入れる拡張も可能。
                    canvas.DrawLine(s, eP);

                    // 終端に矢印（羽根）を描画する（軽量表現）
                    DrawArrow(canvas, s, eP);
                }
            }

            // === ノード描画 ===
            foreach (var el in Elements)
            {
                var r = new RectF(el.X, el.Y, NodeWidth, NodeHeight);

                // ノードの背景色を種類ごとに分ける（視覚的カテゴリ化）
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

                // 形状描画: 種類に応じて角丸矩形／楕円／菱形／矩形などを選択する
                // 学術的には「形状はセマンティクスを伝える符号化」であり、意味の差を表現する。
                switch (el.Type)
                {
                    case GuiElementType.Screen:
                        // 角丸長方形
                        canvas.FillRoundedRectangle(r, 8);
                        canvas.DrawRoundedRectangle(r, 8);
                        break;

                    case GuiElementType.Button:
                        // 楕円（ボタンらしさを視覚的に表現）
                        canvas.FillEllipse(r);
                        canvas.DrawEllipse(r);
                        break;

                    case GuiElementType.Event:
                    case GuiElementType.Operation:
                        // 菱形（ダイヤ）: Path を構築して塗りつぶし・輪郭描画
                        using (var path = new PathF())
                        {
                            path.MoveTo(r.X + r.Width / 2f, r.Y);               // 上頂点
                            path.LineTo(r.Right, r.Y + r.Height / 2f);         // 右頂点
                            path.LineTo(r.X + r.Width / 2f, r.Bottom);        // 下頂点
                            path.LineTo(r.X, r.Y + r.Height / 2f);            // 左頂点
                            path.Close();
                            canvas.FillPath(path);
                            canvas.DrawPath(path);
                        }
                        break;

                    case GuiElementType.Timeout:
                        // 矩形（固定ノードとして目立たせる）
                        canvas.FillRectangle(r);
                        canvas.DrawRectangle(r);
                        break;

                    default:
                        // フォールバックは角丸長方形
                        canvas.FillRoundedRectangle(r, 8);
                        canvas.DrawRoundedRectangle(r, 8);
                        break;
                }

                // 選択強調表示:
                // - el.IsSelected が true の場合、オレンジ色の外枠を通常枠より太く描く（視認性を高める）
                if (el.IsSelected)
                {
                    canvas.StrokeColor = Colors.Orange;
                    canvas.StrokeSize = 3;

                    // 少し余白を持たせた枠を描く（Expand 拡張を利用）
                    canvas.DrawRoundedRectangle(r.Expand(4), 8);

                    // 元のペン設定に戻す
                    canvas.StrokeSize = 2;
                    canvas.StrokeColor = Colors.Black;
                }

                // ラベル描画（中央揃え）
                canvas.FontColor = Colors.Black;
                canvas.FontSize = 14;
                canvas.DrawString(el.Name ?? "", r,
                    HorizontalAlignment.Center, VerticalAlignment.Center);
            }
        }

        /// <summary>
        /// 線の終端に矢羽（左右 2 本の線）を描画する。
        /// 
        /// 幾何的説明:
        /// - 線分の角度 angle = atan2(dy, dx)
        /// - 終端点 end を基準に角度 ± offset の方向に長さ size の線を引くことで羽根を表現する。
        /// 
        /// 実装上の選択:
        /// - 三角形を塗りつぶす代わりに 2 本の線を描いているため描画は軽量。
        /// - 視覚的な好みやスケーラビリティに応じて、塗りつぶし三角形やベジェ曲線の矢印に改良可能。
        /// </summary>
        private void DrawArrow(ICanvas canvas, PointF start, PointF end)
        {
            float size = 6f; // 羽根の長さ（ピクセル）
            canvas.StrokeColor = Colors.Gray;
            canvas.StrokeSize = 2;

            // 線分の角度（ラジアン）
            float angle = MathF.Atan2(end.Y - start.Y, end.X - start.X);

            // 羽根の左右方向の角度オフセット（0.3 ラジアン ≒ 17.2 度）
            var p1 = new PointF(end.X - size * MathF.Cos(angle - 0.3f),
                                end.Y - size * MathF.Sin(angle - 0.3f));
            var p2 = new PointF(end.X - size * MathF.Cos(angle + 0.3f),
                                end.Y - size * MathF.Sin(angle + 0.3f));

            // 羽根を描く（線 2 本）
            canvas.DrawLine(end, p1);
            canvas.DrawLine(end, p2);
        }
    }

    /// <summary>
    /// RectF の拡張メソッド群（現状は Expand のみ）。
    /// - Expand は矩形を margin 分だけ外側に拡張した RectF を返すユーティリティ。
    /// - 表示系で選択枠などを元の矩形より少し外側に描きたい場合に用いる。
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
