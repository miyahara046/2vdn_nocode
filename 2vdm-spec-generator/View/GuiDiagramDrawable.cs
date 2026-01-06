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

        // 外部から渡される「画面一覧（画面管理クラス）」の名前集合（任意）
        // null の場合は Elements 内の Screen 要素から自動構築する（後方互換）
        public IEnumerable<string> ScreenNameSet { get; set; } = null;

        public const float NodeWidth = 160f;
        public const float NodeHeight = 45f;
        private const float TimeoutEllipseWidth = NodeWidth * 0.7f;

        private const float spacing = 80f;
        private const float leftColumnX = 40f;
        private const float midColumnX = leftColumnX + NodeWidth + 40f;
        private const float opColumnX = 520f;
        private const float timeoutStartX = leftColumnX;
        private const float timeoutStartY = 8f;
        private const float timeoutEventOffset = NodeWidth + 120f;

        private const float branchCenterOffset = 120f;

        public readonly List<BranchVisual> BranchVisuals = new();

        public class BranchVisual
        {
            public GuiElement ParentEvent { get; set; }
            public GuiElement Button { get; set; }
            public string Condition { get; set; }
            public string Target { get; set; }
            public float CenterX { get; set; }
            public float CenterY { get; set; }
            public int BranchIndex { get; set; }
        }

        public void ArrangeNodes()
        {
            float timeoutY = timeoutStartY;
            foreach (var el in Elements.Where(e => e.Type == GuiElementType.Timeout))
            {
                el.IsFixed = true;
                el.X = timeoutStartX;
                el.Y = timeoutY;
                timeoutY += NodeHeight + 10f;
            }

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

            var opList = Elements.Where(e => e.Type == GuiElementType.Operation).ToList();

            var timeoutsByName = Elements
    .Where(e => e.Type == GuiElementType.Timeout && !string.IsNullOrWhiteSpace(e.Name))
    .GroupBy(e => e.Name.Trim(), StringComparer.OrdinalIgnoreCase)
    .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
            foreach (var evt in Elements.Where(e => e.Type == GuiElementType.Event))
            {
                if (!string.IsNullOrWhiteSpace(evt.Target) && timeoutsByName.TryGetValue(evt.Target, out var timeoutEl))
                {
                    evt.X = timeoutEl.X + timeoutEventOffset;
                    evt.Y = timeoutEl.Y;
                    evt.IsFixed = true;
                }
            }

            foreach (var evt in Elements.Where(e => e.Type == GuiElementType.Event && (e.Branches == null || e.Branches.Count == 0)))
            {
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
                    evt.X = midColumnX;
                    evt.Y = correspondingButton.Y;
                    evt.IsFixed = true;
                }
            }

            BranchVisuals.Clear();
            float branchSpacing = 18f;
            var eventsWithBranches = Elements.Where(e => e.Type == GuiElementType.Event && e.Branches != null && e.Branches.Count > 0).ToList();

            foreach (var evt in eventsWithBranches)
            {
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

                int n = evt.Branches.Count;
                float totalHeight = n * NodeHeight + Math.Max(0, n - 1) * branchSpacing;
                float anchorCenterY = (correspondingButton != null) ? (correspondingButton.Y + NodeHeight / 2f) : (evt.Y + NodeHeight / 2f);
                float top = anchorCenterY - totalHeight / 2f;
                float centerReferenceX = midColumnX + branchCenterOffset;

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
                        BranchIndex = i
                    });

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
                                op.Y = centerY - NodeHeight / 2f;
                            }
                        }
                    }
                }

                float blockCenterY = top + totalHeight / 2f;
                if (correspondingButton != null)
                {
                    correspondingButton.Y = blockCenterY - NodeHeight / 2f;
                }

                if (IsUnpositioned(evt))
                {
                    evt.X = midColumnX;
                    evt.Y = (correspondingButton != null) ? correspondingButton.Y : (top + NodeHeight / 2f - NodeHeight / 2f);
                }
                else
                {
                    evt.X = midColumnX;
                    evt.Y = (correspondingButton != null) ? correspondingButton.Y : evt.Y;
                }
                evt.IsFixed = true;
            }

            // --- 重要: ブランチ配置でボタンの Y が変更される可能性があるため、
            //           ブランチ後に「分岐を持たないイベント」を再同期して
            //           対象ボタンと Y を揃える ---
            foreach (var evt in Elements.Where(e => e.Type == GuiElementType.Event && (e.Branches == null || e.Branches.Count == 0)))
            {
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
                    // 常にボタンの現在の Y に合わせる（初回読み込みやノード追加時のズレを防ぐ）
                    evt.X = midColumnX;
                    evt.Y = correspondingButton.Y;
                    evt.IsFixed = true;
                }
            }

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
            foreach (var btn in buttonList)
            {
                if (string.IsNullOrWhiteSpace(btn.Target)) continue;

                var op = opList.FirstOrDefault(o =>
                !string.IsNullOrWhiteSpace(o.Name) &&
                string.Equals(o.Name.Trim(), btn.Target.Trim(), StringComparison.OrdinalIgnoreCase));

                if (op == null) continue;

                // ユーザー配置済みは尊重。未配置だけ揃える
                if (!op.IsFixed && IsUnpositioned(op))
                {
                    op.X = opColumnX;
                    op.Y = btn.Y;    
                    op.IsFixed = true;
                }
            }
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

            ArrangeNodes();

            var positions = Elements
    .Where(e => !string.IsNullOrWhiteSpace(e.Name))
    .GroupBy(e => e.Name.Trim(), StringComparer.OrdinalIgnoreCase)
    .ToDictionary(g => g.Key, g => new PointF(g.First().X, g.First().Y), StringComparer.OrdinalIgnoreCase);


            var normPositions = new Dictionary<string, PointF>(StringComparer.OrdinalIgnoreCase);
            foreach (var kv in positions)
            {
                var norm = NormalizeLabel(kv.Key);
                if (!normPositions.ContainsKey(norm))
                    normPositions[norm] = kv.Value;
            }

            // 画面一覧名集合：外部から渡された ScreenNameSet を優先して使い、なければ Elements 内から構築
            // 修正: ScreenNameSet が渡されていても Elements 内の Screen 名を併せて登録する（初回描画で Elements 側の画面を見落とし赤表示される問題対応）
            var screenNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (ScreenNameSet != null)
            {
                foreach (var s in ScreenNameSet)
                {
                    var n = NormalizeLabel(s);
                    if (!string.IsNullOrEmpty(n)) screenNames.Add(n);
                }
            }

            // Elements 側の Screen 名も必ず追加する（ScreenNameSet が不完全なケースへの保険）
            foreach (var s in Elements.Where(e => e.Type == GuiElementType.Screen && !string.IsNullOrWhiteSpace(e.Name)))
            {
                var n = NormalizeLabel(s.Name);
                if (!string.IsNullOrEmpty(n)) screenNames.Add(n);
            }

            bool IsTargetInScreenListByCandidate(string candidate)
            {
                if (string.IsNullOrWhiteSpace(candidate)) return false;
                var norm = NormalizeLabel(candidate);
                if (string.IsNullOrEmpty(norm)) return false;
                return screenNames.Contains(norm);
            }

            bool EndsWithHe(string s) => !string.IsNullOrWhiteSpace(s) && s.Trim().EndsWith("へ");

            string ExtractCandidateFromText(string s)
            {
                if (string.IsNullOrWhiteSpace(s)) return string.Empty;
                var t = s.Trim();
                int idx = t.LastIndexOf('→');
                if (idx >= 0 && idx + 1 < t.Length) return NormalizeLabel(t.Substring(idx + 1).Trim().TrimEnd('へ'));
                int idx2 = t.LastIndexOf("->", StringComparison.Ordinal);
                if (idx2 >= 0 && idx2 + 2 < t.Length) return NormalizeLabel(t.Substring(idx2 + 2).Trim().TrimEnd('へ'));
                if (t.EndsWith("へ")) return NormalizeLabel(t.Substring(0, t.Length - 1));
                return NormalizeLabel(t);
            }

            var buttonList = Elements.Where(e => e.Type == GuiElementType.Button).ToList();

            canvas.StrokeSize = 2;

            var parentEventNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var evt in Elements.Where(e => e.Type == GuiElementType.Event && e.Branches != null && e.Branches.Count > 0))
                if (!string.IsNullOrWhiteSpace(evt.Name)) parentEventNames.Add(NormalizeLabel(evt.Name));

            // --- 通常の遷移線描画（イベント Name が 'へ' で終わる遷移イベントのみ色判定） ---
            foreach (var el in Elements)
            {
                if (string.IsNullOrEmpty(el.Target)) continue;
                if (el.Type == GuiElementType.Event && el.Branches != null && el.Branches.Count > 0) continue;
                if (!string.IsNullOrWhiteSpace(el.Name) && parentEventNames.Contains(NormalizeLabel(el.Name))) continue;

                if (TryResolvePosition(el.Name, positions, normPositions, out var f) &&
                    TryResolvePosition(el.Target, positions, normPositions, out var t))
                {
                    PointF s;
                    if (el.Type == GuiElementType.Button || el.Type == GuiElementType.Timeout)
                    {
                        float ellipseW = (el.Type == GuiElementType.Button) ? NodeWidth / 2f : TimeoutEllipseWidth;
                        float ellipseRight = f.X + (NodeWidth + ellipseW) / 2f;
                        s = new PointF(ellipseRight, f.Y + NodeHeight / 2f);
                    }
                    else
                    {
                        s = new PointF(f.X + NodeWidth, f.Y + NodeHeight / 2f);
                    }

                    var eP = new PointF(t.X, t.Y + NodeHeight / 2f);

                    if (TryGetElementByPosition(t, out var targetEl) && targetEl.Type == GuiElementType.Timeout && el.Type == GuiElementType.Event)
                    {
                        continue;
                    }

                    var isTransitionEvent = el.Type == GuiElementType.Event && EndsWithHe(el.Name);
                    Color lineColor = Colors.Gray;
                    if (isTransitionEvent)
                    {
                        var candidate = ExtractCandidateFromText(el.Name);
                        lineColor = IsTargetInScreenListByCandidate(candidate) ? Colors.Gray : Colors.Red;
                    }

                    canvas.StrokeColor = lineColor;
                    canvas.DrawLine(s, eP);
                    DrawArrow(canvas, s, eP, lineColor);
                }
            }

            // --- タイムアウト -> イベント 矢印 ---
            foreach (var timeout in Elements.Where(e => e.Type == GuiElementType.Timeout && !string.IsNullOrWhiteSpace(e.Name)))
            {
                var linkedEvents = Elements.Where(ev =>
                    ev.Type == GuiElementType.Event &&
                    !string.IsNullOrWhiteSpace(ev.Target) &&
                    string.Equals(NormalizeLabel(ev.Target), NormalizeLabel(timeout.Name), StringComparison.OrdinalIgnoreCase))
                    .ToList();

                if (linkedEvents.Count == 0) continue;

                float ellipseW = TimeoutEllipseWidth;
                var timeoutRight = new PointF(timeout.X + (NodeWidth + ellipseW) / 2f, timeout.Y + NodeHeight / 2f);

                foreach (var ev in linkedEvents)
                {
                    var eventLeft = new PointF(ev.X, ev.Y + NodeHeight / 2f);

                    var isTransitionEvent = ev.Type == GuiElementType.Event && EndsWithHe(ev.Name);
                    var arrowColor = Colors.Gray;
                    if (isTransitionEvent)
                    {
                        var candidate = ExtractCandidateFromText(ev.Name);
                        arrowColor = IsTargetInScreenListByCandidate(candidate) ? Colors.Gray : Colors.Red;
                    }

                    canvas.StrokeColor = arrowColor;
                    canvas.StrokeSize = 2;
                    canvas.DrawLine(timeoutRight, eventLeft);
                    DrawArrow(canvas, timeoutRight, eventLeft, arrowColor);
                }
            }

            // --- 分岐の描画（ブランチ Target の末尾が 'へ' の場合だけ screen list と照合） ---
            float condDiamondW = NodeWidth * 1.1f;
            float condDiamondH = NodeHeight * 1.1f;
            float tW = NodeWidth * 0.95f;
            float tH = NodeHeight * 0.8f;
            float midGap = 20f;
            float condRightShift = 80f;
            float buttonEllipseW = NodeWidth / 2f;

            foreach (var bv in BranchVisuals)
            {
                PointF basePoint;
                if (bv.Button != null)
                {
                    float ellipseRight = bv.Button.X + (NodeWidth + buttonEllipseW) / 2f;
                    basePoint = new PointF(ellipseRight, bv.Button.Y + NodeHeight / 2f);
                }
                else
                    basePoint = new PointF(bv.ParentEvent.X + NodeWidth, bv.ParentEvent.Y + NodeHeight / 2f);

                float condCenterX = bv.CenterX - (condDiamondW / 2f + midGap / 2f + tW / 2f) + condRightShift;
                float targetCenterX = bv.CenterX + (condDiamondW / 2f + midGap / 2f);

                var condCenter = new PointF(condCenterX, bv.CenterY);
                var targetCenter = new PointF(targetCenterX, bv.CenterY);

                var diamondLeft = new PointF(condCenter.X - condDiamondW / 2f, condCenter.Y);
                canvas.StrokeColor = Colors.DarkBlue;
                canvas.StrokeSize = 2;
                canvas.DrawLine(basePoint, diamondLeft);
                DrawArrow(canvas, basePoint, diamondLeft, Colors.DarkBlue);

                canvas.FillColor = Colors.SkyBlue;
                using (var path = new PathF())
                {
                    path.MoveTo(condCenter.X, condCenter.Y - condDiamondH / 2f);
                    path.LineTo(condCenter.X + condDiamondW / 2f, condCenter.Y);
                    path.LineTo(condCenter.X, condCenter.Y + condDiamondH / 2f);
                    path.LineTo(condCenter.X - condDiamondW / 2f, condCenter.Y);
                    path.Close();
                    canvas.FillPath(path);
                    canvas.StrokeColor = Colors.DarkBlue;
                    canvas.DrawPath(path);
                }

                canvas.FontColor = Colors.Black;
                canvas.FontSize = 12;
                var condText = string.IsNullOrWhiteSpace(bv.Condition) ? "(条件)" : bv.Condition;
                var condTextRect = new RectF(condCenter.X - condDiamondW / 2f + 6f, condCenter.Y - 10f, condDiamondW - 12f, 20f);
                canvas.DrawString(condText, condTextRect, HorizontalAlignment.Center, VerticalAlignment.Center);

                var condRight = new PointF(condCenter.X + condDiamondW / 2f, condCenter.Y);
                var targetLeft = new PointF(targetCenter.X - tW / 2f, targetCenter.Y);
                canvas.StrokeColor = Colors.DarkGreen;
                canvas.StrokeSize = 1.8f;
                canvas.DrawLine(condRight, targetLeft);
                DrawArrow(canvas, condRight, targetLeft);

                var targetRect = new RectF(targetCenter.X - tW / 2f, targetCenter.Y - tH / 2f, tW, tH);

                bool branchIsTransition = !string.IsNullOrWhiteSpace(bv.Target) && EndsWithHe(bv.Target);
                bool targetInScreen = true;
                if (branchIsTransition)
                {
                    var candidate = ExtractCandidateFromText(bv.Target);
                    targetInScreen = IsTargetInScreenListByCandidate(candidate);
                }

                if (targetInScreen)
                {
                    canvas.FillColor = Colors.LightGreen;
                    canvas.StrokeColor = Colors.DarkGreen;
                }
                else
                {
                    canvas.FillColor = Colors.LightCoral;
                    canvas.StrokeColor = Colors.Red;
                }

                canvas.FillRectangle(targetRect);
                canvas.DrawRectangle(targetRect);

                var rawTarget = CleanTargetLabel(bv.Target);
                var targetLabel = string.IsNullOrWhiteSpace(rawTarget) ? "(未指定)" : rawTarget;
                canvas.FontColor = Colors.Black;
                canvas.FontSize = 12;
                var targetLabelRect = new RectF(targetCenter.X - tW / 2f + 6f, targetCenter.Y - 10f, tW - 12f, 20f);
                canvas.DrawString(targetLabel, targetLabelRect, HorizontalAlignment.Center, VerticalAlignment.Center);

                if (!string.IsNullOrWhiteSpace(bv.Target) && TryResolvePosition(bv.Target, positions, normPositions, out var targetPos))
                {
                    var targetRectRight = new PointF(targetCenter.X + tW / 2f, targetCenter.Y);
                    var targetPoint = new PointF(targetPos.X, targetPos.Y + NodeHeight / 2f);
                    var linkColor = targetInScreen ? Colors.Gray : Colors.Red;
                    canvas.StrokeColor = linkColor;
                    canvas.StrokeSize = 1.5f;
                    canvas.DrawLine(targetRectRight, new PointF(targetPoint.X - 6f, targetPoint.Y));
                    DrawArrow(canvas, targetRectRight, targetPoint, linkColor);
                }
            }

            // ノード本体描画
            foreach (var el in Elements)
            {
                if (el.Type == GuiElementType.Event && el.Branches != null && el.Branches.Count > 0)
                    continue;

                var r = new RectF(el.X, el.Y, NodeWidth, NodeHeight);

                var attachedToTimeout = !string.IsNullOrWhiteSpace(el.Target) &&
                                         Elements.Any(t => t.Type == GuiElementType.Timeout && t.Name == el.Target);

                bool isTransitionEvent = el.Type == GuiElementType.Event && EndsWithHe(el.Name);
                bool transitionTargetExistsInScreenList = true;
                if (isTransitionEvent)
                {
                    var candidate = ExtractCandidateFromText(el.Name);
                    transitionTargetExistsInScreenList = IsTargetInScreenListByCandidate(candidate);
                }

                // 色決定
                if (el.Type == GuiElementType.Timeout)
                {
                    canvas.FillColor = Colors.Pink;
                    canvas.StrokeColor = Colors.Black;
                }
                else if (el.Type == GuiElementType.Button)
                {
                    canvas.FillColor = Colors.LightBlue;
                    canvas.StrokeColor = Colors.Black;
                }
                else if (el.Type == GuiElementType.Event)
                {
                    if (isTransitionEvent && !transitionTargetExistsInScreenList)
                    {
                        canvas.FillColor = Colors.LightCoral;
                        canvas.StrokeColor = Colors.Red;
                    }
                    else if (attachedToTimeout)
                    {
                        canvas.FillColor = Colors.Pink;
                        canvas.StrokeColor = Colors.Black;
                    }
                    else
                    {
                        canvas.FillColor = Colors.LightGreen;
                        canvas.StrokeColor = Colors.Black;
                    }
                }
                else if (el.Type == GuiElementType.Operation)
                {
                    canvas.FillColor = Colors.LightGreen;
                    canvas.StrokeColor = Colors.Black;
                }
                else if (el.Type == GuiElementType.Screen)
                {
                    canvas.FillColor = Colors.Lavender;
                    canvas.StrokeColor = Colors.Black;
                }
                else
                {
                    canvas.FillColor = Colors.LightGray;
                    canvas.StrokeColor = Colors.Black;
                }

                // 描画（タイプ別）
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
                else if (el.Type == GuiElementType.Event)
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
                else if (el.Type == GuiElementType.Operation)
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
                        canvas.StrokeSize = 2;
                        canvas.StrokeColor = Colors.Black;
                    }

                    canvas.FontColor = Colors.Black;
                    canvas.FontSize = 14;
                    canvas.DrawString(el.Name ?? "", r, HorizontalAlignment.Center, VerticalAlignment.Center);
                }
                else if (el.Type == GuiElementType.Timeout)
                {
                    float ellipseW = TimeoutEllipseWidth;
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

                    canvas.FontColor = Colors.Black;
                    canvas.FontSize = 14;
                    canvas.DrawString(el.Name ?? "", ellipseRect, HorizontalAlignment.Center, VerticalAlignment.Center);
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

        private static string NormalizeLabel(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return string.Empty;
            var t = s.Trim();
            while (t.EndsWith("へ", StringComparison.Ordinal) || t.EndsWith(" ", StringComparison.Ordinal) || t.EndsWith("　", StringComparison.Ordinal))
                t = t.Substring(0, t.Length - 1).TrimEnd();
            return t;
        }

        private static string CleanTargetLabel(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return string.Empty;
            var t = s.Trim();
            if (t.StartsWith("→")) t = t.Substring(1).Trim();
            if (t.StartsWith("->")) t = t.Substring(2).Trim();
            if (t.StartsWith("→")) t = t.TrimStart('→').Trim();
            return t;
        }

        private static bool TryResolvePosition(string name, Dictionary<string, PointF> positions, Dictionary<string, PointF> normPositions, out PointF pos)
        {
            pos = default;
            if (string.IsNullOrWhiteSpace(name)) return false;

            if (positions.TryGetValue(name, out pos)) return true;

            var norm = NormalizeLabel(name);
            if (!string.IsNullOrEmpty(norm) && normPositions.TryGetValue(norm, out pos)) return true;

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

        private bool TryGetElementByPosition(PointF pos, out GuiElement element)
        {
            element = Elements.FirstOrDefault(e => MathF.Abs(e.X - pos.X) < 0.1f && MathF.Abs(e.Y - pos.Y) < 0.1f);
            return element != null;
        }

        private void DrawArrow(ICanvas canvas, PointF start, PointF end, Color? color = null)
        {
            float size = 6f;
            var arrowColor = color ?? Colors.Gray;
            canvas.StrokeColor = arrowColor;
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
