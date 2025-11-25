using Markdig;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using _2vdm_spec_generator.ViewModel;

namespace _2vdm_spec_generator.Services
{
    internal class UiToMarkdownConverter
    {
        public string AddClassHeading(string markdown, string className)
        {
            var lines = string.IsNullOrWhiteSpace(markdown)
                ? new List<string>()
                : markdown.Split(Environment.NewLine).ToList();

            if (lines.Count > 0)
                lines[0] = $"#{className}";
            else
                lines.Add($"#{className}");

            return string.Join(Environment.NewLine, lines);
        }

        public string AddScreenList(string markdown, string screenName)
        {
            var lines = markdown.Split(Environment.NewLine).ToList();

            // 2行目を必ず空行にする
            if (lines.Count < 2)
            {
                lines.Add(string.Empty);
            }
            else if (!string.IsNullOrEmpty(lines[1]))
            {
                lines[1] = string.Empty;
            }

            // 3行目以降に追加
            lines.Add($"- {screenName}");

            return string.Join(Environment.NewLine, lines);
        }

        public string AddButton(string markdown, string buttonName)
        {
            // 改行コードの種類に関わらず正しく分割できるように、複数の改行コードを区切り文字として指定
            string[] newLineSeparators = { Environment.NewLine, "\r\n", "\n", "\r" };
            var lines = new List<string>(markdown.Split(newLineSeparators, StringSplitOptions.None));

            const string targetHeading = "### 有効ボタン一覧";
            int buttonListHeadingIndex = -1;
            int searchLimit = Math.Min(5, lines.Count);

            // 1行目から最大5行目まで (インデックス0から4まで) を探索
            for (int i = 0; i < searchLimit; i++)
            {
                if (lines[i].Trim() == targetHeading)
                {
                    buttonListHeadingIndex = i;
                    break;
                }
            }

            if (buttonListHeadingIndex != -1)
            {
                // ### 有効ボタン一覧 が見つかった場合
                int insertionIndex = -1;

                // 見出しの次の行から探索を開始
                for (int i = buttonListHeadingIndex + 1; i < lines.Count; i++)
                {
                    // 最初に見つかった空行を挿入位置とする
                    if (string.IsNullOrWhiteSpace(lines[i]))
                    {
                        insertionIndex = i;
                        break;
                    }
                    // 次の見出しが見つかった場合は、その直前の行に挿入
                    else if (lines[i].Trim().StartsWith("#") && !lines[i].Trim().StartsWith("-") && !lines[i].Trim().StartsWith("*"))
                    {
                        // 見出しの直前の行に挿入
                        insertionIndex = i;
                        break;
                    }
                }

                // 空行または次の見出しが見つからなかった場合は、リストの末尾に追加
                if (insertionIndex == -1)
                {
                    insertionIndex = lines.Count;
                }

                // 既存のテキストを上書きしないように、新しい行を挿入
                lines.Insert(insertionIndex, $"- {buttonName}");
            }
            else
            // ### 有効ボタン一覧 が見つからなかった場合
            {
                // 最初の4行（インデックス0から3）の中で、テキストが入っている最後の行のインデックスを探す
                int lastNonEmptyIndex = -1;
                for (int i = 0; i < Math.Min(lines.Count, 4); i++)
                {
                    // Trim()して空文字でなければ、その行がテキストが入っている行
                    if (!string.IsNullOrWhiteSpace(lines[i]))
                    {
                        lastNonEmptyIndex = i;
                    }
                }

                // テキストが入っている最後の行の次の次の行に挿入
                int insertionIndex = lastNonEmptyIndex + 2;

                // 挿入位置が存在しない場合は、行を追加する
                while (lines.Count < insertionIndex)
                {
                    lines.Add(string.Empty);
                }

                // 挿入位置（insertionIndex）に ### 有効ボタン一覧 を挿入
                lines.Insert(insertionIndex, targetHeading);

                // その次の行（insertionIndex + 1）に - buttonName を挿入
                lines.Insert(insertionIndex + 1, $"- {buttonName}");
            }

            // 最後に、行を結合してMarkdown文字列を再構築
            return string.Join(Environment.NewLine, lines);
        }

        //イベント追加（ボタン選択時の処理を想定）
        public string AddEvent(string markdown, string buttonName, string eventTarget)
        {
            string[] newLineSeparators = { Environment.NewLine, "\r\n", "\n", "\r" };
            var lines = new List<string>(markdown.Split(newLineSeparators, StringSplitOptions.None));

            const string eventHeading = "### イベント一覧";
            int eventHeadingIndex = -1;

            // 先頭付近にイベント見出しがあるか探す
            for (int i = 0; i < lines.Count; i++)
            {
                if (lines[i].Trim() == eventHeading)
                {
                    eventHeadingIndex = i;
                    break;
                }
            }

            int insertionIndex;
            if (eventHeadingIndex != -1)
            {
                // 見出しがある場合は、そのセクションの末尾を探して挿入
                insertionIndex = eventHeadingIndex + 1;
                while (insertionIndex < lines.Count)
                {
                    var t = lines[insertionIndex].Trim();
                    if (string.IsNullOrEmpty(t) || t.StartsWith("### ") || t.StartsWith("## ") || t.StartsWith("# ")) break;
                    insertionIndex++;
                }
            }
            else
            {
                // 見出しがない場合は適切な位置に新規挿入（既存のヘッダーの後など）
                int insertAfter = -1;
                for (int i = 0; i < Math.Min(lines.Count, 6); i++)
                {
                    if (lines[i].Trim().StartsWith("## ") || lines[i].Trim().StartsWith("# "))
                    {
                        insertAfter = i;
                    }
                }

                insertionIndex = (insertAfter >= 0) ? insertAfter + 1 : lines.Count;

                // 空行と見出しを追加
                if (insertionIndex > lines.Count) insertionIndex = lines.Count;
                lines.Insert(insertionIndex, string.Empty);
                insertionIndex++;
                lines.Insert(insertionIndex, eventHeading);
                insertionIndex++;
            }

            // フォーマット: "- {ボタン名}押下 → {イベント先}"
            var newLine = $"- {buttonName}押下 → {eventTarget}";
            lines.Insert(insertionIndex, newLine);

            return string.Join(Environment.NewLine, lines);
        }

        public string AddConditionalEvent(string markdown, string buttonName, List<(string Condition, string Target)> branches)
        {
            if (branches == null) throw new ArgumentNullException(nameof(branches));

            string[] newLineSeparators = { Environment.NewLine, "\r\n", "\n", "\r" };
            var lines = new List<string>(markdown.Split(newLineSeparators, StringSplitOptions.None));

            const string eventHeading = "### イベント一覧";
            int eventHeadingIndex = -1;

            // 先頭付近にイベント見出しがあるか探す
            for (int i = 0; i < lines.Count; i++)
            {
                if (lines[i].Trim() == eventHeading)
                {
                    eventHeadingIndex = i;
                    break;
                }
            }

            int insertionIndex;
            if (eventHeadingIndex != -1)
            {
                // 見出しがある場合は、そのセクションの末尾を探して挿入
                insertionIndex = eventHeadingIndex + 1;
                while (insertionIndex < lines.Count)
                {
                    var t = lines[insertionIndex].Trim();
                    // セクション終端条件：空行または次の見出し
                    if (string.IsNullOrEmpty(t) || t.StartsWith("### ") || t.StartsWith("## ") || t.StartsWith("# "))
                        break;
                    insertionIndex++;
                }
            }
            else
            {
                // 見出しがない場合は適切な位置に新規挿入（既存のヘッダーの後など）
                int insertAfter = -1;
                for (int i = 0; i < Math.Min(lines.Count, 6); i++)
                {
                    var trimmed = lines[i].Trim();
                    if (trimmed.StartsWith("## ") || trimmed.StartsWith("# "))
                    {
                        insertAfter = i;
                    }
                }

                insertionIndex = (insertAfter >= 0) ? insertAfter + 1 : lines.Count;

                if (insertionIndex > lines.Count) insertionIndex = lines.Count;

                // 空行と見出しを追加
                lines.Insert(insertionIndex, string.Empty);
                insertionIndex++;
                lines.Insert(insertionIndex, eventHeading);
                insertionIndex++;
            }
            // メイン行（" - {button}押下 →"）
            lines.Insert(insertionIndex, $"- {buttonName}押下 →");
            insertionIndex++;

            // 各分岐はスペース + "- " で追加
            foreach (var (condition, target) in branches)
            {
                // 空白や矢印が混ざっている入力に対する簡単な正規化
                var cond = (condition ?? string.Empty).Trim();
                var tgt = (target ?? string.Empty).Trim();

                // 例: "表示部に１が入力されている" と "画面Cへ" のように保存
                lines.Insert(insertionIndex, $"  - {cond} → {tgt}");
                insertionIndex++;
            }

            return string.Join(Environment.NewLine, lines);
        }


        public string AddTimeoutEvent(string markdown, int timeoutSeconds, string target)
        {
            var lines = markdown.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None).ToList();

            const string eventHeading = "### イベント一覧";

            // -----------------------------
            // ❶ ファイル2行目に「～でタイムアウト」を追加 or 上書き
            // -----------------------------
            string timeoutLine = $"- {timeoutSeconds}秒でタイムアウト";

            if (lines.Count >= 2)
            {
                if (lines[1].Contains("でタイムアウト"))
                {
                    // 既存のタイムアウト行を上書き
                    lines[1] = timeoutLine;
                }
                else
                {
                    // 2行目に挿入
                    lines.Insert(1, timeoutLine);
                }
            }
            else if (lines.Count == 1)
            {
                // 1行しかない場合は末尾に追加
                lines.Add(timeoutLine);
            }
            else if (lines.Count == 0)
            {
                // 空ファイルなら作成
                lines.Add(string.Empty);
                lines.Add(timeoutLine);
            }

            // -----------------------------
            // ❷ イベント一覧セクションの処理
            // -----------------------------
            int eventHeadingIndex = -1;
            for (int i = 0; i < lines.Count; i++)
            {
                if (lines[i].Trim() == eventHeading)
                {
                    eventHeadingIndex = i;
                    break;
                }
            }

            int insertionIndex;
            if (eventHeadingIndex != -1)
            {
                // イベント一覧が存在
                insertionIndex = eventHeadingIndex + 1;
                while (insertionIndex < lines.Count)
                {
                    var t = lines[insertionIndex].Trim();
                    if (string.IsNullOrEmpty(t) || t.StartsWith("### ") || t.StartsWith("## ") || t.StartsWith("# ")) break;
                    insertionIndex++;
                }
            }
            else
            {
                // イベント一覧がない場合 → 新規追加
                insertionIndex = lines.Count;
                lines.Add(string.Empty);
                lines.Add(eventHeading);
                insertionIndex = lines.Count;
            }

            // -----------------------------
            // ❸ 「タイムアウト → ～」行を上書きまたは追加
            // -----------------------------
            int actionLineIndex = -1;
            for (int i = 0; i < lines.Count; i++)
            {
                if (lines[i].Trim().StartsWith("- タイムアウト →"))
                {
                    actionLineIndex = i;
                    break;
                }
            }

            string actionLine = $"- タイムアウト → {target}へ";

            if (actionLineIndex != -1)
            {
                // 既存行を上書き
                lines[actionLineIndex] = actionLine;
            }
            else
            {
                // イベント一覧末尾に追加
                lines.Insert(insertionIndex, actionLine);
            }

            return string.Join(Environment.NewLine, lines);
        }


        public static void UpdateMarkdownOrder(string markdownFilePath, IEnumerable<GuiElement> elements)
        {
            if (string.IsNullOrWhiteSpace(markdownFilePath) || !File.Exists(markdownFilePath)) return;
            if (elements == null) return;

            var lines = File.ReadAllLines(markdownFilePath).ToList();

            // 1) 有効ボタン一覧を抽出して書き換える
            lines = ReplaceListSection(lines, "### 有効ボタン一覧", BuildButtonList(elements));

            // 2) イベント一覧を抽出して書き換える（イベントブロック単位で並べ替え）
            lines = ReplaceEventSection(lines, "### イベント一覧", BuildEventBlockOrder(elements));

            // 上書き保存
            File.WriteAllLines(markdownFilePath, lines, Encoding.UTF8);
        }

        // 有効ボタン一覧用：elements から Button の名前を Y 順で取り出す（Name が null でなければ）
        private static List<string> BuildButtonList(IEnumerable<GuiElement> elements)
        {
            return elements
                .Where(e => e.Type == GuiElementType.Button && !string.IsNullOrWhiteSpace(e.Name))
                .OrderBy(e => e.Y)
                .Select(e => e.Name.Trim())
                .ToList();
        }

        // イベント一覧用：elements から Event の“キー”（比較に使うテキスト）を取得
        // ここでは GuiElement.Name を使用してマッチを試みる。
        private static List<string> BuildEventBlockOrder(IEnumerable<GuiElement> elements)
        {
            return elements
                .Where(e => e.Type == GuiElementType.Event && !string.IsNullOrWhiteSpace(e.Name))
                .OrderBy(e => e.Y)
                .Select(e => e.Name.Trim())
                .ToList();
        }

        // 指定見出しセクション（単純なトップレベルの - item リスト）を置き換える
        private static List<string> ReplaceListSection(List<string> lines, string heading, List<string> newItems)
        {
            if (newItems == null || newItems.Count == 0) return lines;

            int idx = lines.FindIndex(l => l.Trim() == heading);
            if (idx == -1) return lines;

            int insertion = idx + 1;
            // セクション終端を探す：空行または次の見出し（#）が来るまで
            int end = insertion;
            while (end < lines.Count)
            {
                var t = lines[end];
                if (string.IsNullOrWhiteSpace(t)) { end++; break; }
                if (t.TrimStart().StartsWith("### ") || t.TrimStart().StartsWith("## ") || t.TrimStart().StartsWith("# ")) break;
                end++;
            }

            // 新しいセクションに置換：heading と newItems と 1つ空行を残す
            var newSection = new List<string> { heading };
            foreach (var it in newItems)
                newSection.Add($"- {it}");
            newSection.Add(string.Empty);

            // 実際の置換
            var result = new List<string>();
            result.AddRange(lines.Take(idx));
            result.AddRange(newSection);
            result.AddRange(lines.Skip(end));
            return result;
        }

        // イベントセクションは「ブロック」単位（トップレベルの "- " とそれに続くインデント付き行群）で扱う。
        // blocksOrder は GuiElement.Name のリスト（Y順）で、それをベースにブロックを並べ替える。
        private static List<string> ReplaceEventSection(List<string> lines, string heading, List<string> blocksOrder)
        {
            int idx = lines.FindIndex(l => l.Trim() == heading);
            if (idx == -1) return lines;

            int cursor = idx + 1;
            // Collect blocks
            var blocks = new List<(string key, List<string> textLines)>();
            while (cursor < lines.Count)
            {
                var line = lines[cursor];
                if (string.IsNullOrWhiteSpace(line))
                {
                    cursor++;
                    // stop at first blank followed by heading or end? We'll treat blank as separator but continue
                    // We'll break if next non-empty is another heading
                    int look = cursor;
                    while (look < lines.Count && string.IsNullOrWhiteSpace(lines[look])) look++;
                    if (look < lines.Count && (lines[look].TrimStart().StartsWith("### ") || lines[look].TrimStart().StartsWith("## ") || lines[look].TrimStart().StartsWith("# ")))
                        break;
                    continue;
                }
                if (line.TrimStart().StartsWith("### ") || line.TrimStart().StartsWith("## ") || line.TrimStart().StartsWith("# "))
                {
                    break;
                }

                // Expect top-level block starts with "- "
                if (line.TrimStart().StartsWith("- "))
                {
                    var block = new List<string> { line };
                    int next = cursor + 1;
                    // collect indented or nested lines (starting with two or more spaces) as part of the block
                    while (next < lines.Count && (lines[next].StartsWith("  ") || lines[next].StartsWith("\t") || string.IsNullOrWhiteSpace(lines[next])))
                    {
                        // stop if next is a top-level new "- " with same indent (no leading spaces)
                        if (!string.IsNullOrWhiteSpace(lines[next]) && lines[next].TrimStart().StartsWith("- ") && !lines[next].StartsWith("  "))
                            break;

                        block.Add(lines[next]);
                        next++;
                    }

                    // Determine a key for this block for matching: try to extract meaningful token from first line
                    var first = block.First().Trim();
                    // Remove leading "- " and trim
                    var keyCandidate = first.StartsWith("- ") ? first.Substring(2).Trim() : first;
                    // For matching, use whole first line text as keyCandidate (matching is fuzzy)
                    blocks.Add((keyCandidate, block));
                    cursor = next;
                }
                else
                {
                    // Unexpected line inside events section — treat as consumed
                    cursor++;
                }
            }

            // If no blocks found, nothing to do
            if (!blocks.Any()) return lines;

            // Build reorder mapping: blocksOrder contains element names in desired order.
            // We'll try to match each desired name to a block whose first line contains that name.
            var orderedBlocks = new List<List<string>>();
            var used = new bool[blocks.Count];

            // First, match by contains (exact substring) in order of blocksOrder
            foreach (var desired in blocksOrder)
            {
                bool matched = false;
                for (int i = 0; i < blocks.Count; i++)
                {
                    if (used[i]) continue;
                    var key = blocks[i].key;
                    if (!string.IsNullOrEmpty(key) && key.Contains(desired, StringComparison.Ordinal))
                    {
                        orderedBlocks.Add(blocks[i].textLines);
                        used[i] = true;
                        matched = true;
                        break;
                    }
                }
                // if not matched, continue — we will append unmatched blocks later
            }

            // Append any remaining unmatched blocks in original order
            for (int i = 0; i < blocks.Count; i++)
            {
                if (!used[i])
                    orderedBlocks.Add(blocks[i].textLines);
            }

            // Now rebuild the final lines:
            // Keep everything before heading, then heading, then flattened ordered blocks, then remainder after the original event section
            // Determine where the original event section ended (cursor where we stopped collecting)
            int endOfSectionCursor = cursor;
            // If we broke because of heading, cursor currently points at that heading or beyond.

            var result = new List<string>();
            result.AddRange(lines.Take(idx)); // before heading
            result.Add(lines[idx]); // the heading itself

            // Add ordered blocks
            foreach (var blockLines in orderedBlocks)
            {
                foreach (var bl in blockLines)
                    result.Add(bl);
            }

            // Add a blank line after section (to separate from next heading), only if original had one
            result.Add(string.Empty);

            // Append the rest after endOfSectionCursor
            if (endOfSectionCursor < lines.Count)
                result.AddRange(lines.Skip(endOfSectionCursor));

            return result;
        }
    }


}
