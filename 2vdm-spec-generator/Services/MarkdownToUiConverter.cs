using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using _2vdm_spec_generator.ViewModel;

namespace _2vdm_spec_generator.Services
{
    internal class MarkdownToUiConverter
    {

        private static readonly Regex EventPattern = new Regex(@"^(?<Name>.*?)\s*→\s*(?<Target>.*)$", RegexOptions.Compiled);

        private static readonly Regex OperationPattern = new Regex(@"^(?<Operation>.*?)(?<Trigger>押下|で)\s*→\s*(?<Target>.*)$", RegexOptions.Compiled);

        private void ArrangeElements(List<GuiElement> elements)
        {
            float startX = 40;
            float startY = 40;
            float paddingY = 80;
            float paddingX = 0;

            // ==== Screen ====
            float currentY = startY;
            foreach (var screen in elements.Where(e => e.Type == GuiElementType.Screen))
            {
                screen.X = startX;
                screen.Y = currentY;
                currentY += screen.Height + paddingY;
            }

            // ==== Timeout ====
            float timeoutX = startX + paddingX;
            currentY = startY;
            foreach (var timeout in elements.Where(e => e.Type == GuiElementType.Timeout))
            {
                timeout.X = timeoutX;
                timeout.Y = currentY;
                currentY += timeout.Height + paddingY;
            }

            // ==== Button + Event Group ====
            float buttonX = timeoutX + paddingX;
            float eventX = buttonX + 150;

            currentY = startY;
            var buttons = elements.Where(e => e.Type == GuiElementType.Button).ToList();

            foreach (var button in buttons)
            {
                // ボタン配置
                button.X = buttonX;
                button.Y = currentY;

                // 同じ Target を持つ Event 全て取得
                var relatedEvents = elements
                    .Where(e => e.Type == GuiElementType.Event &&
                        (
                            // 1) Target が存在して button.Target と一致
                            (!string.IsNullOrWhiteSpace(e.Target) && !string.IsNullOrWhiteSpace(button.Target) &&
                             string.Equals(e.Target.Trim(), button.Target.Trim(), StringComparison.OrdinalIgnoreCase))
                            ||
                            // 2) Target が存在して button.Name と一致（イベントがボタン名をターゲットにしている場合）
                            (!string.IsNullOrWhiteSpace(e.Target) && !string.IsNullOrWhiteSpace(button.Name) &&
                             string.Equals(e.Target.Trim(), button.Name.Trim(), StringComparison.OrdinalIgnoreCase))
                            ||
                            // 3) Name が存在して button.Target と一致（稀な逆方向マッピング）
                            (!string.IsNullOrWhiteSpace(e.Name) && !string.IsNullOrWhiteSpace(button.Target) &&
                             string.Equals(e.Name.Trim(), button.Target.Trim(), StringComparison.OrdinalIgnoreCase))
                        ))
                    .ToList();

                float eventY = currentY;

                foreach (var evt in relatedEvents)
                {
                    evt.X = eventX;
                    evt.Y = eventY;

                    eventY += evt.Height + 10;  // イベント間の間隔
                }

                // Eventが複数ある場合、縦に並んだ高さに合わせて次のボタンのYを調整
                float blockHeight = Math.Max(button.Height, (eventY - currentY));
                currentY += blockHeight + paddingY;
            }

            // ==== Targetを持たない孤立イベント ====
            currentY += paddingY;
            foreach (var evt in elements.Where(e => e.Type == GuiElementType.Event && e.X == 0 && e.Y == 0))
            {
                evt.X = eventX;
                evt.Y = currentY;
                currentY += evt.Height + paddingY;
            }
        }





        public IEnumerable<GuiElement> Convert(string markdown)
        {
            var elements = new List<GuiElement>();
            if (string.IsNullOrWhiteSpace(markdown)) return elements;

            var lines = markdown.Split('\n').Select(l => l.TrimEnd('\r')).ToList();
            if (lines.Count == 0) return elements;

            // 1行目が "# 画面一覧" の場合
            if (lines[0].Trim() == "# 画面一覧")
            {
                for (int i = 1; i < lines.Count; i++)
                {
                    var line = lines[i].Trim();
                    if (line.StartsWith("- "))
                    {
                        var name = line.Substring(2).Trim();
                        elements.Add(new GuiElement
                        {
                            Type = GuiElementType.Screen,
                            Name = name,
                            Description = "",
                            X = 200,
                            Y = 80 + elements.Count * 90
                        });
                    }
                }
                ArrangeElements(elements);
                return elements;
            }
            else if (lines[0].Trim().StartsWith("## "))
            {
                // 1. Timeout 抽出
                if (lines.Count > 1)
                {
                    var second = lines[1].Trim();
                    if (second.StartsWith("- "))
                    {
                        var content = second.Length > 2 ? second.Substring(2).Trim() : string.Empty;

                        // タイムアウト → 遷移先 の形式に対応
                        var timeoutMatch = EventPattern.Match(content);
                        if (timeoutMatch.Success)
                        {
                            var name = timeoutMatch.Groups["Name"].Value.Trim();
                            var target = timeoutMatch.Groups["Target"].Value.Trim();

                            // "〇〇で" の部分をTimeoutのNameとして切り出す
                            var idx = name.IndexOf('で');
                            var timeoutName = idx > 0 ? name.Substring(0, idx).Trim() : name;

                            elements.Add(new GuiElement
                            {
                                Type = GuiElementType.Timeout,
                                Name = timeoutName,
                                Target = target,
                                Description = ""
                            });
                        }
                        else
                        {
                            // 従来の "〇〇で" の形式に対応
                            var idx = content.IndexOf('で');
                            if (idx > 0)
                            {
                                var name = content.Substring(0, idx).Trim();
                                elements.Add(new GuiElement
                                {
                                    Type = GuiElementType.Timeout,
                                    Name = name,
                                    Description = ""
                                });
                            }
                        }
                    }
                }

                // 2. Button 抽出 (変更なし)
                var buttonElements = new List<GuiElement>();
                if (lines.Count > 2)
                {
                    for (int i = 2; i < lines.Count; i++)
                    {
                        var line = lines[i].Trim();
                        if (line.StartsWith("### 有効ボタン一覧"))
                        {
                            for (int k = i + 1; k < lines.Count; k++)
                            {
                                var sub = lines[k].Trim();

                                if (string.IsNullOrEmpty(sub) || sub.StartsWith("### イベント一覧") || sub.StartsWith("### ") || sub.StartsWith("## "))
                                {
                                    goto EndOfButtonList;
                                }

                                if (sub.StartsWith("- "))
                                {
                                    var name = sub.Substring(2).Trim();
                                    var newButton = new GuiElement
                                    {
                                        Type = GuiElementType.Button,
                                        Name = name,
                                        Description = ""
                                    };
                                    buttonElements.Add(newButton);
                                    elements.Add(newButton);
                                }
                            }
                        EndOfButtonList:;
                            break;
                        }
                    }
                }

                // 3. Event / Operation 抽出
                if (lines.Count > 2)
                {
                    for (int i = 2; i < lines.Count; i++)
                    {
                        var line = lines[i].Trim();
                        if (line.StartsWith("### イベント一覧"))
                        {
                            for (int k = i + 1; k < lines.Count; k++)
                            {
                                var subRaw = lines[k];

                                var sub = subRaw.Trim();

                                // 次のヘッダーが出現したら終了
                                if (sub.StartsWith("### ") || sub.StartsWith("## "))
                                {
                                    goto EndOfEventList;
                                }

                                if (sub.StartsWith("- "))
                                {
                                    var content = sub.Substring(2).Trim();

                                    // 押下/で イベントの解析 (OperationPatternを使用)
                                    var opMatch = OperationPattern.Match(content);

                                    if (opMatch.Success)
                                    {
                                        var opName = opMatch.Groups["Operation"].Value.Trim();
                                        var targetName = opMatch.Groups["Target"].Value.Trim(); // →の右側

                                        // --- 対応ボタン検索（正規化して buttonElements -> elements を探索） ---
                                        GuiElement correspondingButton = null;
                                        if (!string.IsNullOrWhiteSpace(opName))
                                        {
                                            string norm = opName.Trim();
                                            var candidates = new List<string> { norm, norm.Replace("押下", "").Trim() };

                                            foreach (var cand in candidates)
                                            {
                                                correspondingButton = buttonElements
                                                    .FirstOrDefault(b => !string.IsNullOrWhiteSpace(b.Name) && string.Equals(b.Name.Trim(), cand, StringComparison.OrdinalIgnoreCase));
                                                if (correspondingButton != null) break;

                                                correspondingButton = elements
                                                    .FirstOrDefault(e => e.Type == GuiElementType.Button && !string.IsNullOrWhiteSpace(e.Name) && string.Equals(e.Name.Trim(), cand, StringComparison.OrdinalIgnoreCase));
                                                if (correspondingButton != null) break;
                                            }
                                        }

                                        // targetNameが空の場合はネストされたリストを解析して Branches を作る
                                        if (string.IsNullOrEmpty(targetName))
                                        {
                                            int startK = k + 1;
                                            var branches = new List<GuiElement.EventBranch>();

                                            // ネストされたリストは UiToMarkdownConverter では "  - {cond} → {target}" の形式で出力されるため
                                            // 行頭に2つ以上のスペースまたはタブがある行をネスト行とみなす
                                            while (startK < lines.Count)
                                            {
                                                var nestedRaw = lines[startK];
                                                if (string.IsNullOrWhiteSpace(nestedRaw)) break;

                                                // ネスト判定: 先頭に2つ以上のスペースまたはタブがあるかどうか
                                                if (!(nestedRaw.StartsWith("  ") || nestedRaw.StartsWith("\t")))
                                                    break;

                                                var nestedTrim = nestedRaw.Trim();
                                                if (nestedTrim.StartsWith("- "))
                                                {
                                                    var nestedContent = nestedTrim.Substring(2).Trim();
                                                    var nestedMatch = EventPattern.Match(nestedContent);
                                                    if (nestedMatch.Success)
                                                    {
                                                        var cond = nestedMatch.Groups["Name"].Value.Trim();
                                                        var nestedTarget = nestedMatch.Groups["Target"].Value.Trim();

                                                        branches.Add(new GuiElement.EventBranch
                                                        {
                                                            Condition = cond,
                                                            Target = nestedTarget
                                                        });
                                                    }
                                                    else
                                                    {
                                                        // '→' が無い場合は全体を条件として格納
                                                        branches.Add(new GuiElement.EventBranch
                                                        {
                                                            Condition = nestedTrim,
                                                            Target = string.Empty
                                                        });
                                                    }
                                                }

                                                startK++;
                                            }
                                            // k を読み進めた位置に戻す
                                            k = Math.Max(k, startK - 1);

                                            if (branches.Count > 0)
                                            {
                                                // 親イベント要素を作成して Branches を格納する
                                                var eventEl = new GuiElement
                                                {
                                                    Type = GuiElementType.Event,
                                                    Name = string.IsNullOrWhiteSpace(opName) ? "押下" : $"{opName}押下",
                                                    Target = correspondingButton?.Target ?? opName,
                                                    Description = "",
                                                    Branches = branches
                                                };
                                                elements.Add(eventEl);
                                            }
                                            // ブランチがない場合は何もしない
                                        }
                                        else
                                        {
                                            // 押下イベントをEventとして追加 (Targetが空でない場合のみ)
                                            var evt = new GuiElement
                                            {
                                                Type = GuiElementType.Event,
                                                Name = targetName,
                                                Target = targetName,
                                                Description = ""
                                            };
                                            elements.Add(evt);

                                            // --- 追加: 単一イベントを見つけた場合、対応ボタンに Target を設定 ---
                                            if (correspondingButton != null && string.IsNullOrWhiteSpace(correspondingButton.Target))
                                            {
                                                // 同じ Target を持つボタンが他にないか簡易チェック
                                                var duplicate = elements.Any(e => e != correspondingButton && e.Type == GuiElementType.Button && !string.IsNullOrWhiteSpace(e.Target)
                                                                                 && string.Equals(e.Target.Trim(), targetName.Trim(), StringComparison.OrdinalIgnoreCase));
                                                if (!duplicate)
                                                {
                                                    correspondingButton.Target = targetName;
                                                }
                                            }
                                        }
                                    }
                                    else
                                    {
                                        // 通常のイベント解析 (EventPatternを使用)
                                        var eventMatch = EventPattern.Match(content);
                                        if (eventMatch.Success)
                                        {
                                            // 左辺と右辺を取得
                                            var leftName = eventMatch.Groups["Name"].Value.Trim();
                                            var rightTarget = eventMatch.Groups["Target"].Value.Trim();

                                            if (string.IsNullOrEmpty(rightTarget))
                                            {
                                                // 例: "- タイムアウト →" のように右辺が空 -> 左辺を Name/Target として扱う
                                                elements.Add(new GuiElement
                                                {
                                                    Type = GuiElementType.Event,
                                                    Name = leftName,
                                                    Target = leftName,
                                                    Description = ""
                                                });
                                            }
                                            else
                                            {
                                                // 改良: 左辺がタイムアウト（またはタイムアウトを含む表現）の場合は既存の Timeout 要素に紐づける
                                                GuiElement timeoutEl = null;

                                                // 1) 左辺が Timeout 名と完全一致するケース
                                                timeoutEl = elements.FirstOrDefault(e => e.Type == GuiElementType.Timeout && e.Name == leftName);

                                                // 2) 左辺が Timeout 名を含む、または Timeout 名が左辺に含まれるケース
                                                if (timeoutEl == null)
                                                {
                                                    timeoutEl = elements.FirstOrDefault(e => e.Type == GuiElementType.Timeout && !string.IsNullOrWhiteSpace(e.Name) && leftName.Contains(e.Name));
                                                }
                                                if (timeoutEl == null)
                                                {
                                                    timeoutEl = elements.FirstOrDefault(e => e.Type == GuiElementType.Timeout && !string.IsNullOrWhiteSpace(e.Name) && e.Name.Contains(leftName));
                                                }

                                                // 3) 左辺が 'タイムアウト' を直接含む場合、タイムアウト要素が一つだけならそれに紐づける
                                                if (timeoutEl == null && leftName.Contains("タイムアウト"))
                                                {
                                                    timeoutEl = elements.FirstOrDefault(e => e.Type == GuiElementType.Timeout);
                                                }

                                                if (timeoutEl != null)
                                                {
                                                    // Event は右側を Name (表示対象)、Target を timeout の Name にする
                                                    var evt = new GuiElement
                                                    {
                                                        Type = GuiElementType.Event,
                                                        Name = rightTarget,
                                                        Target = timeoutEl.Name,
                                                        Description = ""
                                                    };
                                                    elements.Add(evt);

                                                    // ボタン探索：左辺や右辺を手がかりに対応ボタンを探す（leftName を優先）
                                                    GuiElement correspondingButton = null;
                                                    if (!string.IsNullOrWhiteSpace(leftName))
                                                    {
                                                        var cand = leftName.Trim();
                                                        // 候補: exact, without 押下
                                                        var candidates = new List<string> { cand, cand.Replace("押下", "").Trim() };
                                                        foreach (var c in candidates)
                                                        {
                                                            correspondingButton = buttonElements
                                                                .FirstOrDefault(b => !string.IsNullOrWhiteSpace(b.Name) && string.Equals(b.Name.Trim(), c, StringComparison.OrdinalIgnoreCase));
                                                            if (correspondingButton != null) break;

                                                            correspondingButton = elements
                                                                .FirstOrDefault(e => e.Type == GuiElementType.Button && !string.IsNullOrWhiteSpace(e.Name) && string.Equals(e.Name.Trim(), c, StringComparison.OrdinalIgnoreCase));
                                                            if (correspondingButton != null) break;
                                                        }
                                                    }

                                                    if (correspondingButton != null && string.IsNullOrWhiteSpace(correspondingButton.Target))
                                                    {
                                                        var dup = elements.Any(e => e != correspondingButton && e.Type == GuiElementType.Button && !string.IsNullOrWhiteSpace(e.Target)
                                                                                   && string.Equals(e.Target.Trim(), evt.Target?.Trim(), StringComparison.OrdinalIgnoreCase));
                                                        if (!dup)
                                                            correspondingButton.Target = evt.Target;
                                                    }
                                                }
                                                else
                                                {
                                                    // 通常の "A → B" は右辺を Name/Target にする（従来挙動）
                                                    var evt = new GuiElement
                                                    {
                                                        Type = GuiElementType.Event,
                                                        Name = rightTarget,
                                                        Target = rightTarget,
                                                        Description = ""
                                                    };
                                                    elements.Add(evt);

                                                    // ボタン探索：左辺や右辺を手がかりに対応ボタンを探す（leftName を優先）
                                                    GuiElement correspondingButton = null;
                                                    if (!string.IsNullOrWhiteSpace(leftName))
                                                    {
                                                        var cand = leftName.Trim();
                                                        var candidates = new List<string> { cand, cand.Replace("押下", "").Trim() };
                                                        foreach (var c in candidates)
                                                        {
                                                            correspondingButton = buttonElements
                                                                .FirstOrDefault(b => !string.IsNullOrWhiteSpace(b.Name) && string.Equals(b.Name.Trim(), c, StringComparison.OrdinalIgnoreCase));
                                                            if (correspondingButton != null) break;

                                                            correspondingButton = elements
                                                                .FirstOrDefault(e => e.Type == GuiElementType.Button && !string.IsNullOrWhiteSpace(e.Name) && string.Equals(e.Name.Trim(), c, StringComparison.OrdinalIgnoreCase));
                                                            if (correspondingButton != null) break;
                                                        }
                                                    }

                                                    if (correspondingButton != null && string.IsNullOrWhiteSpace(correspondingButton.Target))
                                                    {
                                                        var dup = elements.Any(e => e != correspondingButton && e.Type == GuiElementType.Button && !string.IsNullOrWhiteSpace(e.Target)
                                                                                   && string.Equals(e.Target.Trim(), evt.Target?.Trim(), StringComparison.OrdinalIgnoreCase));
                                                        if (!dup)
                                                            correspondingButton.Target = evt.Target;
                                                    }
                                                }
                                            }
                                        }
                                        else
                                        {
                                            // '→' がない、またはその他の通常のイベント
                                            // ここで、イベント名が既存の Timeout 名と一致するなら Target にセットしておくと親和性が上がる
                                            var possibleName = content;
                                            var timeout = elements.FirstOrDefault(e => e.Type == GuiElementType.Timeout && e.Name == possibleName);
                                            if (timeout != null)
                                            {
                                                elements.Add(new GuiElement
                                                {
                                                    Type = GuiElementType.Event,
                                                    Name = possibleName,
                                                    Target = possibleName,
                                                    Description = ""
                                                });
                                            }
                                            else
                                            {
                                                elements.Add(new GuiElement
                                                {
                                                    Type = GuiElementType.Event,
                                                    Name = content,
                                                    Description = ""
                                                });
                                            }
                                        }
                                    }
                                }
                            }
                        EndOfEventList:;
                            ArrangeElements(elements);
                            return elements;
                        }
                    }
                }
                ArrangeElements(elements);
                return elements;
            }
            ArrangeElements(elements);
            return elements;
        }

    }
}