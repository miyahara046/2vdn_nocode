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
        private const float leftColumnX = 40f;
        private const float midColumnX = leftColumnX + NodeWidth + 40f; // 分岐ハンドル／イベント中継用
        private const float opColumnX = 520f; // 操作（Operation）を並べる右端列
        private const float timeoutStartX = leftColumnX;
        private const float timeoutStartY = 8f;
        private const float timeoutEventOffset = NodeWidth + 100f;

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

            // 分岐を持つイベント（Branchesが存在するもの）をボタンに紐づけて中間列に配置し、
            // 各 branch.Target に対応する Operation ノードを右端に配置して Y を分岐毎に揃える
            var eventsWithBranches = Elements.Where(e => e.Type == GuiElementType.Event && e.Branches != null && e.Branches.Count > 0).ToList();

            foreach (var evt in eventsWithBranches)
            {
                // 対応ボタンを探索（ボタン名がイベント名の先頭にある等を考慮）
                GuiElement correspondingButton = null;
                if (!string.IsNullOrWhiteSpace(evt.Name))
                {
                    var evtNameNorm = evt.Name.Trim();
                    // try exact prefix match against button name
                    correspondingButton = buttonList.FirstOrDefault(b =>
                        !string.IsNullOrWhiteSpace(b.Name) &&
                        evtNameNorm.StartsWith(b.Name.Trim(), StringComparison.OrdinalIgnoreCase));

                    // fallback: match by Target (イベント.Target がボタン.Target を参照している場合)
                    if (correspondingButton == null && !string.IsNullOrWhiteSpace(evt.Target))
                    {
                        correspondingButton = buttonList.FirstOrDefault(b => !string.IsNullOrWhiteSpace(b.Target) && string.Equals(b.Target.Trim(), evt.Target.Trim(), StringComparison.OrdinalIgnoreCase));
                    }
                }

                // 見つかったボタンに合わせてイベントを中間列に配置、もしくは既定の位置
                if (correspondingButton != null)
                {
                    evt.X = midColumnX;
                    evt.Y = correspondingButton.Y;
                    evt.IsFixed = true;
                }
                else
                {
                    // 見つからなければ右列に配置（既存の挙動を尊重）
                    if (IsUnpositioned(evt))
                    {
                        evt.X = midColumnX;
                        evt.Y = timeoutY + (buttonIndex++) * spacing;
                    }
                }

                // 分岐ごとに対応する Operation を右端に配置
                float branchSpacing = 18f;
                float startOffset = -((evt.Branches.Count - 1) * branchSpacing) / 2f;
                float offset = startOffset;
                for (int i = 0; i < evt.Branches.Count; i++)
                {
                    var br = evt.Branches[i];
                    if (string.IsNullOrWhiteSpace(br.Target)) { offset += branchSpacing; continue; }

                    // Target 名と一致する Operation を探す（正規化して比較）
                    var op = opList.FirstOrDefault(o => !string.IsNullOrWhiteSpace(o.Name) &&
                                                         string.Equals(o.Name.Trim(), br.Target.Trim(), StringComparison.OrdinalIgnoreCase));
                    if (op != null)
                    {
                        // まだ未配置なら右端列に配置し、Yを分岐に合わせる
                        if (IsUnpositioned(op))
                        {
                            op.X = opColumnX;
                            op.Y = evt.Y + offset;
                            op.IsFixed = true;
                        }
                        else
                        {
                            // 既に配置済みでもYをイベントに近づける（視認性のため）
                            op.Y = evt.Y + offset;
                        }
                    }
                    offset += branchSpacing;
                }
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

            if (!Elements.Any(e => e.X != 0 || e.Y != 0))
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

            // --- 変更: 分岐親イベントから伸びる線を抑制 ---
            // 分岐を持つ親イベントのソース名（正規化）を集合化しておく（後でスキップ判定に使う）
            var parentEventSourceNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var eventsWithBranches = Elements.Where(e => e.Type == GuiElementType.Event && e.Branches != null && e.Branches.Count > 0);
            foreach (var p in eventsWithBranches)
            {
                if (!string.IsNullOrWhiteSpace(p.Name))
                    parentEventSourceNames.Add(NormalizeLabel(p.Name));
            }

            // 基本の線描画（Target ベース）
            canvas.StrokeColor = Colors.Gray;
            canvas.StrokeSize = 2;
            foreach (var el in Elements)
            {
                if (string.IsNullOrEmpty(el.Target)) continue;

                // 分岐親イベントから伸びる線は描画しない（要求対応）
                if (el.Type == GuiElementType.Event && el.Branches != null && el.Branches.Count > 0)
                    continue;

                // また、別アプローチ：親イベントの名前に対応するソース名の線も抑制
                if (!string.IsNullOrWhiteSpace(el.Name) && parentEventSourceNames.Contains(NormalizeLabel(el.Name)))
                    continue;

                // Try resolve both source and target positions with normalization fallback
                if (TryResolvePosition(el.Name, positions, normPositions, out var f) &&
                    TryResolvePosition(el.Target, positions, normPositions, out var t))
                {
                    var s = new PointF(f.X + NodeWidth, f.Y + NodeHeight / 2f);
                    var eP = new PointF(t.X, t.Y + NodeHeight / 2f);
                    canvas.DrawLine(s, eP);
                    DrawArrow(canvas, s, eP);
                }
            }

            // --- 分岐イベント（親イベントは非表示にして、ボタンから直接分岐を描く） ---
            foreach (var evt in eventsWithBranches)
            {
                // 親イベントに紐づくボタンを探索（ArrangeNodes と同じロジック）
                GuiElement correspondingButton = null;
                if (!string.IsNullOrWhiteSpace(evt.Name))
                {
                    var evtNameNorm = evt.Name.Trim();
                    correspondingButton = buttonList.FirstOrDefault(b =>
                        !string.IsNullOrWhiteSpace(b.Name) &&
                        evtNameNorm.StartsWith(b.Name.Trim(), StringComparison.OrdinalIgnoreCase));
                    if (correspondingButton == null && !string.IsNullOrWhiteSpace(evt.Target))
                    {
                        correspondingButton = buttonList.FirstOrDefault(b => !string.IsNullOrWhiteSpace(b.Target) && string.Equals(b.Target.Trim(), evt.Target.Trim(), StringComparison.OrdinalIgnoreCase));
                    }
                }

                // basePoint を決定（ボタンが見つかればボタン右端、無ければ親イベント右端）
                PointF basePoint;
                if (correspondingButton != null)
                    basePoint = new PointF(correspondingButton.X + NodeWidth, correspondingButton.Y + NodeHeight / 2f);
                else
                    basePoint = new PointF(evt.X + NodeWidth, evt.Y + NodeHeight / 2f);

                float branchSpacing = 18f;
                float startOffset = -((evt.Branches.Count - 1) * branchSpacing) / 2f;
                float offset = startOffset;

                foreach (var br in evt.Branches)
                {
                    var branchPoint = new PointF(basePoint.X + 10f, basePoint.Y + offset);

                    // 線：起点 -> branchPoint
                    canvas.StrokeColor = Colors.DarkBlue;
                    canvas.StrokeSize = 2;
                    canvas.DrawLine(basePoint, branchPoint);

                    // ハンドル
                    canvas.FillColor = Colors.SkyBlue;
                    canvas.FillEllipse(new RectF(branchPoint.X - 4f, branchPoint.Y - 4f, 8f, 8f));
                    canvas.StrokeColor = Colors.DarkBlue;
                    canvas.DrawEllipse(new RectF(branchPoint.X - 4f, branchPoint.Y - 4f, 8f, 8f));

                    // 条件ラベル
                    canvas.FontColor = Colors.Black;
                    canvas.FontSize = 12;
                    var condText = string.IsNullOrWhiteSpace(br.Condition) ? "(条件)" : br.Condition;
                    canvas.DrawString(condText, new RectF(branchPoint.X + 8f, branchPoint.Y - 8f, 200f, 16f), HorizontalAlignment.Left, VerticalAlignment.Center);

                    // 対応操作が存在するかを positions で調べ接続（解決ヘルパーを使用）
                    if (!string.IsNullOrWhiteSpace(br.Target) && TryResolvePosition(br.Target, positions, normPositions, out var targetPos))
                    {
                        var targetPoint = new PointF(targetPos.X, targetPos.Y + NodeHeight / 2f);
                        // branchPoint -> targetPoint
                        canvas.StrokeColor = Colors.Gray;
                        canvas.StrokeSize = 1.5f;
                        canvas.DrawLine(new PointF(branchPoint.X, branchPoint.Y), new PointF(targetPoint.X - 6f, targetPoint.Y));
                        DrawArrow(canvas, branchPoint, targetPoint);
                    }
                    else
                    {
                        // 未指定または未配置のターゲット表示
                        if (string.IsNullOrWhiteSpace(br.Target))
                            canvas.DrawString("(未指定)", new RectF(branchPoint.X + 8f, branchPoint.Y + 8f, 120f, 14f), HorizontalAlignment.Left, VerticalAlignment.Top);
                        else
                            canvas.DrawString($"→ {br.Target}", new RectF(branchPoint.X + 8f, branchPoint.Y + 8f, 180f, 14f), HorizontalAlignment.Left, VerticalAlignment.Top);
                    }

                    offset += branchSpacing;
                }
                // 戻す
                canvas.StrokeSize = 2;
                canvas.StrokeColor = Colors.Black;
            }

            // ノード本体描画（分岐親イベントはここで描かない）
            foreach (var el in Elements)
            {
                // 親イベント（Branches があるもの）は表示しない（見た目をボタン→子へ直接にするため）
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

                if (el.IsSelected)
                {
                    canvas.StrokeColor = Colors.Orange;
                    canvas.StrokeSize = 3;
                    if (el.Type == GuiElementType.Button) canvas.DrawEllipse(r.Expand(4));
                    else canvas.DrawRoundedRectangle(r.Expand(4), 8);
                    canvas.StrokeSize = 2;
                    canvas.StrokeColor = Colors.Black;
                }

                // ラベル
                canvas.FontColor = Colors.Black;
                canvas.FontSize = 14;
                canvas.DrawString(el.Name ?? "", r, HorizontalAlignment.Center, VerticalAlignment.Center);
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
