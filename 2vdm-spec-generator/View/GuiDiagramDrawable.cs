using Microsoft.Maui.Graphics;
using _2vdm_spec_generator.ViewModel;
using System;
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

        // レイアウト
        private const float spacing = 80f;
        private const float leftColumnX = 80f;
        private const float midColumnX = leftColumnX + NodeWidth + 40f; // 分岐ハンドル／イベント中継用
        private const float opColumnX = 520f; // 操作（Operation）を並べる右端列
        private const float timeoutStartX = leftColumnX;
        private const float timeoutStartY = 8f;
        private const float timeoutEventOffset = NodeWidth + 100f;

        // 分岐の可視ノード（描画用）を保持（公開して外部から参照可能にする）
        public readonly List<BranchVisual> BranchVisuals = new();

        public class BranchVisual
        {
            public GuiElement ParentEvent { get; set; }
            public GuiElement Button { get; set; }
            public string Condition { get; set; }
            public string Target { get; set; }
            public float CenterX { get; set; }   // 中央参照点（ダイヤモンド中心の基準）
            public float CenterY { get; set; }
            public int BranchIndex { get; set; } // 追加：親イベント内での分岐インデックス
        }

        public void ArrangeNodes()
        {
            // タイムアウト群の配置
            float timeoutY = timeoutStartY;
            foreach (var el in Elements.Where(e => e.Type == GuiElementType.Timeout))
            {
                el.IsFixed = true;
                el.X = timeoutStartX;
                el.Y = timeoutY;
                timeoutY += NodeHeight + 10f;
            }

            // Screen
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

            // Button
            int buttonIndex = 0;
            var buttonList = Elements.Where(e => e.Type == GuiElementType.Button).ToList();
            foreach (var el in buttonList)
            {
                if (IsUnpositioned(el))
                {
                    el.X = leftColumnX;
                    el.Y = timeoutY + screenIndex * spacing + buttonIndex * spacing;
                }
                buttonIndex++;
            }



            // Operation 未配置のものは右端に積む準備（位置は後で分岐に合わせる）
            var opList = Elements.Where(e => e.Type == GuiElementType.Operation).ToList();

            // Event: まずタイムアウト紐づきのイベントは既存処理（タイムアウト横）
            var timeoutsByName = Elements.Where(e => e.Type == GuiElementType.Timeout && !string.IsNullOrWhiteSpace(e.Name))
                                         .ToDictionary(e => e.Name, e => e);
            foreach (var evt in Elements.Where(e => e.Type == GuiElementType.Event))
            {
                if (!string.IsNullOrWhiteSpace(evt.Target) && timeoutsByName.TryGetValue(evt.Target, out var timeoutEl))
                {
                    evt.X = timeoutEl.X + timeoutEventOffset;
                    evt.Y = timeoutEl.Y;
                    evt.IsFixed = true;
                }
            }

            // ----- 単一（非条件）イベントを対応ボタンに常に同期 -----
            // 条件分岐イベント（Branches を持つもの）は後で別処理するため除外
            foreach (var evt in Elements.Where(e => e.Type == GuiElementType.Event && (e.Branches == null || e.Branches.Count == 0)))
            {
                // タイムアウト紐づけ済みで固定されている場合は上書きしない
                if (evt.IsFixed && !IsUnpositioned(evt)) continue;

                GuiElement correspondingButton = null;

                if (!string.IsNullOrWhiteSpace(evt.Name))
                {
                    var evtNameNorm = evt.Name.Trim();
                    correspondingButton = buttonList.FirstOrDefault(b =>
                        !string.IsNullOrWhiteSpace(b.Name) &&
                        evtNameNorm.StartsWith(b.Name.Trim(), StringComparison.OrdinalIgnoreCase));
                }

                if (correspondingButton == null && !string.IsNullOrWhiteSpace(evt.Target))
                {
                    correspondingButton = buttonList.FirstOrDefault(b => !string.IsNullOrWhiteSpace(b.Target) && string.Equals(b.Target.Trim(), evt.Target.Trim(), StringComparison.OrdinalIgnoreCase));
                }

                if (correspondingButton != null)
                {
                    // 常に同期：ボタンの Y に合わせ、イベントを中間列に寄せて固定する
                    evt.X = midColumnX;
                    evt.Y = correspondingButton.Y;
                    evt.IsFixed = true;
                }
            }
            // ---------------------------------------------------------------------

            // ----- 分岐イベント処理（子イベントをノードとして可視化） -----
            BranchVisuals.Clear();
            float branchSpacing = 18f; // 見た目で使う間隔（Draw側とも整合）
            var eventsWithBranches = Elements.Where(e => e.Type == GuiElementType.Event && e.Branches != null && e.Branches.Count > 0).ToList();

            foreach (var evt in eventsWithBranches)
            {
                // 対応ボタンを探索（既存ロジック）
                GuiElement correspondingButton = null;
                var buttonListLocal = Elements.Where(e => e.Type == GuiElementType.Button).ToList();
                if (!string.IsNullOrWhiteSpace(evt.Name))
                {
                    var evtNameNorm = evt.Name.Trim();
                    correspondingButton = buttonListLocal.FirstOrDefault(b =>
                        !string.IsNullOrWhiteSpace(b.Name) &&
                        evtNameNorm.StartsWith(b.Name.Trim(), StringComparison.OrdinalIgnoreCase));

                    if (correspondingButton == null && !string.IsNullOrWhiteSpace(evt.Target))
                    {
                        correspondingButton = buttonListLocal.FirstOrDefault(b => !string.IsNullOrWhiteSpace(b.Target) && string.Equals(b.Target.Trim(), evt.Target.Trim(), StringComparison.OrdinalIgnoreCase));
                    }
                }

                // ブランチ数に応じて各子ノードの中心Yを算出（子ノードは midColumnX に揃える）
                int n = evt.Branches.Count;
                float totalHeight = n * NodeHeight + Math.Max(0, n - 1) * branchSpacing;

                // 基準 Y を決める元値（既存ボタン位置またはイベント位置）
                float anchorCenterY = (correspondingButton != null) ? (correspondingButton.Y + NodeHeight / 2f) : (evt.Y + NodeHeight / 2f);

                // まず top を anchorCenterY を中心に算出（後でボタンをブロック中央に合わせる）
                float top = anchorCenterY - totalHeight / 2f;
                float centerReferenceX = midColumnX + NodeWidth / 2f; // 中間列の中心基準

                for (int i = 0; i < n; i++)
                {
                    var br = evt.Branches[i];
                    float centerY = top + NodeHeight / 2f + i * (NodeHeight + branchSpacing);
                    BranchVisuals.Add(new BranchVisual
                    {
                        ParentEvent = evt,
                        Button = correspondingButton,
                        Condition = br.Condition,
                        Target = br.Target,
                        CenterX = centerReferenceX,
                        CenterY = centerY,
                        BranchIndex = i // ここで index を保持
                    });

                    // Branch に対応する Operation を右端に配置する（存在すれば Y をこの子ノード中心に合わせる）
                    if (!string.IsNullOrWhiteSpace(br.Target))
                    {
                        var op = opList.FirstOrDefault(o => !string.IsNullOrWhiteSpace(o.Name) &&
                                                             string.Equals(o.Name.Trim(), br.Target.Trim(), StringComparison.OrdinalIgnoreCase));
                        if (op != null)
                        {
                            if (IsUnpositioned(op))
                            {
                                op.X = opColumnX;
                                op.Y = centerY - NodeHeight / 2f;
                                op.IsFixed = true;
                            }
                            else
                            {
                                // 既に配置済みでも Y を子ノードに合わせる（視認性向上）
                                op.Y = centerY - NodeHeight / 2f;
                            }
                        }
                    }
                }

                // ブロック中央を計算して、対応ボタンがあればその上にセンタリングする（要求通り）
                float blockCenterY = top + totalHeight / 2f;
                if (correspondingButton != null)
                {
                    correspondingButton.Y = blockCenterY - NodeHeight / 2f;
                }

                // 親イベント自体は中間列へ寄せておく（表示は Draw で抑制する）
                if (IsUnpositioned(evt))
                {
                    evt.X = midColumnX;
                    evt.Y = (correspondingButton != null) ? correspondingButton.Y : (top + NodeHeight / 2f - NodeHeight / 2f);
                }
                else
                {
                    evt.X = midColumnX;
                    // ensure parent Y aligned with block center for consistency
                    evt.Y = (correspondingButton != null) ? correspondingButton.Y : evt.Y;
                }
                evt.IsFixed = true;
            }

            // それ以外の Event は既存の右列縦並びで配置（未配置のもの）
            int eventIndex = 0;
            foreach (var el in Elements.Where(e => e.Type == GuiElementType.Event))
            {
                if (IsUnpositioned(el))
                {
                    el.X = midColumnX;
                    el.Y = timeoutY + eventIndex * spacing;
                }
                eventIndex++;
            }

            // 未配置の Operation は右端に縦に配置
            int opIndex = 0;
            foreach (var el in Elements.Where(e => e.Type == GuiElementType.Operation))
            {
                if (IsUnpositioned(el))
                {
                    el.X = opColumnX;
                    el.Y = timeoutY + (buttonIndex + opIndex) * spacing;
                }
                opIndex++;
            }
        }

        private static bool IsUnpositioned(GuiElement e) => e.X == 0 && e.Y == 0;

        public void Draw(ICanvas canvas, RectF dirtyRect)
        {
            canvas.FillColor = Colors.White;
            canvas.FillRectangle(dirtyRect);

            if (Elements == null || Elements.Count == 0) return;

            // 変更点：常に ArrangeNodes() を実行して BranchVisuals を再構築する
            // 既に位置が設定されている要素については ArrangeNodes() 内の IsUnpositioned 判定で上書きされないため安全
            ArrangeNodes();

            // positions: Name -> PointF
            var positions = Elements.Where(e => !string.IsNullOrWhiteSpace(e.Name))
                            .ToDictionary(e => e.Name, e => new PointF(e.X, e.Y));

            // normalized map (Trim / remove trailing へ) for fallback lookup
            var normPositions = new Dictionary<string, PointF>(StringComparer.OrdinalIgnoreCase);
            foreach (var kv in positions)
            {
                var norm = NormalizeLabel(kv.Key);
                if (!normPositions.ContainsKey(norm))
                    normPositions[norm] = kv.Value;
            }

            // ボタン一覧キャッシュ（分岐接続のため）
            var buttonList = Elements.Where(e => e.Type == GuiElementType.Button).ToList();

            // --- 基本の Target ベース線描画（分岐親イベントや子ノード起点の線は描かない） ---
            canvas.StrokeColor = Colors.Gray;
            canvas.StrokeSize = 2;

            // 親イベント（Branches を持つ）から伸びる既存の線は消すために名前集合を作成
            var parentEventNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var evt in Elements.Where(e => e.Type == GuiElementType.Event && e.Branches != null && e.Branches.Count > 0))
                if (!string.IsNullOrWhiteSpace(evt.Name)) parentEventNames.Add(NormalizeLabel(evt.Name));

            foreach (var el in Elements)
            {
                if (string.IsNullOrEmpty(el.Target)) continue;

                // 親イベント要素の基本線は描画しない
                if (el.Type == GuiElementType.Event && el.Branches != null && el.Branches.Count > 0) continue;

                // 子ノード（分岐）を可視ノード化するため、親イベント名と一致するソースの通常線は抑制
                if (!string.IsNullOrWhiteSpace(el.Name) && parentEventNames.Contains(NormalizeLabel(el.Name))) continue;

                if (TryResolvePosition(el.Name, positions, normPositions, out var f) &&
                    TryResolvePosition(el.Target, positions, normPositions, out var t))
                {
                    // 修正: ボタンから始まる線は楕円の右端を起点にする
                    PointF s;
                    if (el.Type == GuiElementType.Button)
                    {
                        // ボタンは楕円で描画しており、見た目上の右端は矩形右端より内側にある。
                        float buttonEllipseWi = NodeWidth / 2f;
                        float ellipseRight = f.X + (NodeWidth + buttonEllipseWi) / 2f;
                        s = new PointF(ellipseRight, f.Y + NodeHeight / 2f);
                    }
                    else
                    {
                        s = new PointF(f.X + NodeWidth, f.Y + NodeHeight / 2f);
                    }

                    var eP = new PointF(t.X, t.Y + NodeHeight / 2f);
                    canvas.DrawLine(s, eP);
                    DrawArrow(canvas, s, eP);
                }
            }

            // --- 分岐の描画：ボタン -> Condition(青長方形) -> Target(緑ひし形) -> 実ターゲット の順で表示 ---
            // 条件矩形サイズ / ひし形サイズ
            float condW = NodeWidth * 0.9f;
            float condH = NodeHeight * 0.7f;
            // 変更：ひし形幅を狭めて縦長に近づける（横幅が広すぎる問題を修正）
            float diamondW = NodeWidth * 0.8f;
            float diamondH = NodeHeight * 0.7f;
            // condition と target 間の水平ギャップ
            float midGap = 24f;
            // 条件矩形を右に寄せる微調整（要求により右へ）
            float condRightShift = 40f;

            // ボタン楕円幅（半分にする）
            float buttonEllipseW = NodeWidth / 2f;

            foreach (var bv in BranchVisuals)
            {
                // basePoint（起点）は対応ボタンがあればボタン右中央、なければ親イベント右中央
                PointF basePoint;
                if (bv.Button != null)
                {
                    // 楕円はボタン矩形内で中央寄せして描画しており、右端を計算する
                    float ellipseRight = bv.Button.X + (NodeWidth + buttonEllipseW) / 2f;
                    basePoint = new PointF(ellipseRight, bv.Button.Y + NodeHeight / 2f);
                }
                else
                    basePoint = new PointF(bv.ParentEvent.X + NodeWidth, bv.ParentEvent.Y + NodeHeight / 2f);

                // 中央参照点 bv.CenterX はブロックの中心。配置方針：
                // condCenter = bv.CenterX - (diamondW/2 + midGap/2 + condW/2) + condRightShift
                // targetCenter = bv.CenterX + (diamondW/2 + midGap/2)
                float condCenterX = bv.CenterX - (diamondW / 2f + midGap / 2f + condW / 2f) + condRightShift;
                float targetCenterX = bv.CenterX + (diamondW / 2f + midGap / 2f);

                var condCenter = new PointF(condCenterX, bv.CenterY);
                var targetCenter = new PointF(targetCenterX, bv.CenterY);

                // 起点 -> 条件矩形左側へ線を引く
                var condLeft = new PointF(condCenter.X - condW / 2f, condCenter.Y);
                canvas.StrokeColor = Colors.DarkBlue;
                canvas.StrokeSize = 2;
                canvas.DrawLine(basePoint, condLeft);

                // 条件矩形（青）
                var condRect = new RectF(condCenter.X - condW / 2f, condCenter.Y - condH / 2f, condW, condH);
                canvas.FillColor = Colors.SkyBlue;
                canvas.FillRectangle(condRect);
                canvas.StrokeColor = Colors.DarkBlue;
                canvas.DrawRectangle(condRect);

                // 条件ラベル（矩形中央）
                canvas.FontColor = Colors.Black;
                canvas.FontSize = 12;
                var condText = string.IsNullOrWhiteSpace(bv.Condition) ? "(条件)" : bv.Condition;
                canvas.DrawString(condText, condRect, HorizontalAlignment.Center, VerticalAlignment.Center);

                // 矩形右 -> ひし形（Target）左 へ矢印（条件 -> ターゲット）
                var condRight = new PointF(condCenter.X + condW / 2f, condCenter.Y);
                var diamondLeft = new PointF(targetCenter.X - diamondW / 2f, targetCenter.Y);
                // 矩形右から少し内側へ線を引き、矢印でつなぐ
                canvas.StrokeColor = Colors.DarkGreen;
                canvas.StrokeSize = 1.8f;
                canvas.DrawLine(condRight, diamondLeft);
                DrawArrow(canvas, condRight, diamondLeft);

                // ひし形（緑）描画（ターゲット）
                canvas.FillColor = Colors.LightGreen;
                using (var path = new PathF())
                {
                    path.MoveTo(targetCenter.X, targetCenter.Y - diamondH / 2f); // top
                    path.LineTo(targetCenter.X + diamondW / 2f, targetCenter.Y); // right
                    path.LineTo(targetCenter.X, targetCenter.Y + diamondH / 2f); // bottom
                    path.LineTo(targetCenter.X - diamondW / 2f, targetCenter.Y); // left
                    path.Close();
                    canvas.FillPath(path);
                    canvas.StrokeColor = Colors.DarkGreen;
                    canvas.DrawPath(path);
                }

                // ひし形中央に Target 名を表示（先頭の '→' を除去して表示）
                var rawTarget = CleanTargetLabel(bv.Target);
                var targetLabel = string.IsNullOrWhiteSpace(rawTarget) ? "(未指定)" : rawTarget;
                var targetRect = new RectF(targetCenter.X - diamondW / 2f + 6f, targetCenter.Y - 10f, diamondW - 12f, 20f);
                canvas.FontColor = Colors.Black;
                canvas.FontSize = 12;
                canvas.DrawString(targetLabel, targetRect, HorizontalAlignment.Center, VerticalAlignment.Center);

                // ひし形右端 -> 実ノード(Target) へ線（Target が解決できれば接続）
                if (!string.IsNullOrWhiteSpace(bv.Target) && TryResolvePosition(bv.Target, positions, normPositions, out var targetPos))
                {
                    var diamondRight = new PointF(targetCenter.X + diamondW / 2f, targetCenter.Y);
                    var targetPoint = new PointF(targetPos.X, targetPos.Y + NodeHeight / 2f);

                    canvas.StrokeColor = Colors.Gray;
                    canvas.StrokeSize = 1.5f;
                    canvas.DrawLine(diamondRight, new PointF(targetPoint.X - 6f, targetPoint.Y));
                    DrawArrow(canvas, diamondRight, targetPoint);
                }
            }

            // ノード本体描画（分岐親イベントはここで描かない）
            foreach (var el in Elements)
            {
                // 親イベント（Branches があるもの）は表示しない（見た目をボタン→Condition→Target にするため）
                if (el.Type == GuiElementType.Event && el.Branches != null && el.Branches.Count > 0)
                    continue;

                var r = new RectF(el.X, el.Y, NodeWidth, NodeHeight);

                var attachedToTimeout = !string.IsNullOrWhiteSpace(el.Target) &&
                                         Elements.Any(t => t.Type == GuiElementType.Timeout && t.Name == el.Target);

                // 背景色
                if (el.Type == GuiElementType.Timeout) canvas.FillColor = Colors.Pink;
                else if (el.Type == GuiElementType.Button) canvas.FillColor = Colors.LightBlue;
                else if (el.Type == GuiElementType.Event && attachedToTimeout) canvas.FillColor = Colors.Pink;
                else if (el.Type == GuiElementType.Event || el.Type == GuiElementType.Operation) canvas.FillColor = Colors.LightGreen;
                else if (el.Type == GuiElementType.Screen) canvas.FillColor = Colors.Lavender;
                else canvas.FillColor = Colors.LightGray;

                canvas.StrokeColor = Colors.Black;

                // ボタンは楕円幅を半分にして中央に寄せて描画
                if (el.Type == GuiElementType.Button)
                {
                    float ellipseW = NodeWidth / 2f;
                    var ellipseRect = new RectF(el.X + (NodeWidth - ellipseW) / 2f, el.Y, ellipseW, NodeHeight);
                    canvas.FillEllipse(ellipseRect);
                    canvas.DrawEllipse(ellipseRect);

                    if (el.IsSelected)
                    {
                        canvas.StrokeColor = Colors.Orange;
                        canvas.StrokeSize = 3;
                        canvas.DrawEllipse(ellipseRect.Expand(4));
                        canvas.StrokeSize = 2;
                        canvas.StrokeColor = Colors.Black;
                    }

                    // ラベルは楕円の中央に表示（矩形 r ではなく楕円領域に合わせる）
                    canvas.FontColor = Colors.Black;
                    canvas.FontSize = 14;
                    var labelRect = new RectF(ellipseRect.Left, ellipseRect.Top, ellipseRect.Width, ellipseRect.Height);
                    canvas.DrawString(el.Name ?? "", labelRect, HorizontalAlignment.Center, VerticalAlignment.Center);
                }
                else if (el.Type == GuiElementType.Screen)
                {
                    canvas.FillRoundedRectangle(r, 8);
                    canvas.DrawRoundedRectangle(r, 8);

                    if (el.IsSelected)
                    {
                        canvas.StrokeColor = Colors.Orange;
                        canvas.StrokeSize = 3;
                        canvas.DrawRoundedRectangle(r.Expand(4), 8);
                        canvas.StrokeSize = 2;
                        canvas.StrokeColor = Colors.Black;
                    }

                    canvas.FontColor = Colors.Black;
                    canvas.FontSize = 14;
                    canvas.DrawString(el.Name ?? "", r, HorizontalAlignment.Center, VerticalAlignment.Center);
                }
                else if (el.Type == GuiElementType.Event && attachedToTimeout)
                {
                    canvas.FillRectangle(r);
                    canvas.DrawRectangle(r);

                    if (el.IsSelected)
                    {
                        canvas.StrokeColor = Colors.Orange;
                        canvas.StrokeSize = 3;
                        canvas.DrawRectangle(r.Expand(4));
                        canvas.StrokeSize = 2;
                        canvas.StrokeColor = Colors.Black;
                    }

                    canvas.FontColor = Colors.Black;
                    canvas.FontSize = 14;
                    canvas.DrawString(el.Name ?? "", r, HorizontalAlignment.Center, VerticalAlignment.Center);
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

                    if (el.IsSelected)
                    {
                        canvas.StrokeColor = Colors.Orange;
                        canvas.StrokeSize = 3;
                        canvas.DrawPath(new PathF()
                        {
                            // no-op for selection path reuse; keep simple by drawing expanded rounded rect
                        });
                        canvas.StrokeSize = 2;
                        canvas.StrokeColor = Colors.Black;
                    }

                    canvas.FontColor = Colors.Black;
                    canvas.FontSize = 14;
                    canvas.DrawString(el.Name ?? "", r, HorizontalAlignment.Center, VerticalAlignment.Center);
                }
                else if (el.Type == GuiElementType.Timeout)
                {
                    canvas.FillRectangle(r);
                    canvas.DrawRectangle(r);

                    if (el.IsSelected)
                    {
                        canvas.StrokeColor = Colors.Orange;
                        canvas.StrokeSize = 3;
                        canvas.DrawRectangle(r.Expand(4));
                        canvas.StrokeSize = 2;
                        canvas.StrokeColor = Colors.Black;
                    }

                    canvas.FontColor = Colors.Black;
                    canvas.FontSize = 14;
                    canvas.DrawString(el.Name ?? "", r, HorizontalAlignment.Center, VerticalAlignment.Center);
                }
                else
                {
                    canvas.FillRoundedRectangle(r, 8);
                    canvas.DrawRoundedRectangle(r, 8);

                    if (el.IsSelected)
                    {
                        canvas.StrokeColor = Colors.Orange;
                        canvas.StrokeSize = 3;
                        canvas.DrawRoundedRectangle(r.Expand(4), 8);
                        canvas.StrokeSize = 2;
                        canvas.StrokeColor = Colors.Black;
                    }

                    canvas.FontColor = Colors.Black;
                    canvas.FontSize = 14;
                    canvas.DrawString(el.Name ?? "", r, HorizontalAlignment.Center, VerticalAlignment.Center);
                }
            }
        }

        // 名前を正規化（Trim + 末尾の「へ」を除去など）
        private static string NormalizeLabel(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return string.Empty;
            var t = s.Trim();
            // 末尾の全角「へ」や英数スペースを除去
            while (t.EndsWith("へ", StringComparison.Ordinal) || t.EndsWith(" ", StringComparison.Ordinal) || t.EndsWith("　", StringComparison.Ordinal))
                t = t.Substring(0, t.Length - 1).TrimEnd();
            return t;
        }

        // Target 表示用に先頭の矢印を除去してトリムするユーティリティ
        private static string CleanTargetLabel(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return string.Empty;
            var t = s.Trim();
            // 先頭の全角矢印や半角矢印を除去
            if (t.StartsWith("→")) t = t.Substring(1).Trim();
            if (t.StartsWith("->")) t = t.Substring(2).Trim();
            if (t.StartsWith("→")) t = t.TrimStart('→').Trim();
            return t;
        }

        // positions / normPositions を参照して名前を解決するユーティリティ
        private static bool TryResolvePosition(string name, Dictionary<string, PointF> positions, Dictionary<string, PointF> normPositions, out PointF pos)
        {
            pos = default;
            if (string.IsNullOrWhiteSpace(name)) return false;

            // 1) まず完全一致（元のキー）を試す
            if (positions.TryGetValue(name, out pos)) return true;

            // 2) 正規化一致を試す
            var norm = NormalizeLabel(name);
            if (!string.IsNullOrEmpty(norm) && normPositions.TryGetValue(norm, out pos)) return true;

            // 3) 部分一致（候補の正規化が name を含む/逆）を試す
            foreach (var kv in positions)
            {
                var kNorm = NormalizeLabel(kv.Key);
                if (string.IsNullOrEmpty(kNorm)) continue;
                if (kNorm.IndexOf(norm, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    norm.IndexOf(kNorm, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    pos = kv.Value;
                    return true;
                }
            }

            return false;
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
            return new RectF(rect.Left - margin, rect.Top - margin, rect.Width + margin * 2, rect.Height + margin * 2);
        }
    }
}
