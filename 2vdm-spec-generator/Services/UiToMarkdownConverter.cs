using Markdig;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using _2vdm_spec_generator.ViewModel;

namespace _2vdm_spec_generator.Services
{
    /// <summary>
    /// UI 操作（画面上の要素操作）を Markdown テキストに反映するためのユーティリティ。
    /// </summary>
    internal class UiToMarkdownConverter
    {
        /// <summary>
        /// 先頭行をクラス見出しにする（または上書きする）。
        /// - 入力: 既存 markdown とクラス名（例: "# MyClass" の先頭部分）
        /// - ロジック: 行分割して先頭行を置換。空ファイルなら行を追加。
        /// 
        /// 計算量: O(n) 行結合・分割（n は行数）
        /// 注意点: 行終端は Environment.NewLine を用いて結合するため、呼び出し元の改行ポリシーに依存する。
        /// </summary>
        public string AddClassHeading(string markdown, string className)
        {
            var lines = string.IsNullOrWhiteSpace(markdown)
                ? new List<string>()
                : markdown.Split(Environment.NewLine).ToList();

            if (lines.Count > 0)
                lines[0] = $"#{className}";
            else
                lines.Add($"#{className}");

            lines = NormalizeEmptyLines(lines);
            return string.Join(Environment.NewLine, lines);
        }

        /// <summary>
        /// 画面一覧に画面名を追加する。
        /// - ファイル上の 2 行目を必ず空行にする（視覚的区切りのため）。
        /// - 3 行目以降に "- {screenName}" を追加する（既存に同名がなければ）。
        /// </summary>
        public string AddScreenList(string markdown, string screenName)
        {
            var lines = markdown.Split(Environment.NewLine).ToList();

            // 既に同名の項目が存在するかチェック（大文字小文字を無視）
            var targetItem = $"- {screenName}".Trim();
            if (lines.Any(l => string.Equals(l?.Trim(), targetItem, StringComparison.OrdinalIgnoreCase)))
            {
                // 重複があれば元のマークダウンを正規化して返す（何も追加しない）
                lines = NormalizeEmptyLines(lines);
                return string.Join(Environment.NewLine, lines);
            }

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

            lines = NormalizeEmptyLines(lines);
            return string.Join(Environment.NewLine, lines);
        }

        /// <summary>
        /// 「### 有効ボタン一覧」セクションにボタンを追加する。
        /// アルゴリズムのポイント:
        /// - まず先頭 5 行（小さいファイルで見つかることが多いため）から見出し探索を行う。
        /// - 見出しが見つかれば、その見出し直後の適切な位置（空行、または次の見出し直前）を挿入箇所とする。
        /// - 見つからなければ、ファイル先頭付近（最初の 4 行の最後の非空行の次）に見出しとリストを挿入する。
        /// 
        /// 計算量: O(n)（行走査）だが探索領域は通常小さいため定数に近い。
        /// 注意点:
        /// - 改行コードの混在を考慮して複数種の改行区切りで Split している。
        /// - Name 重複や既に同名リスト項目が存在するかのチェックは呼び出し元で行う想定。
        /// </summary>
        public string AddButton(string markdown, string buttonName)
        {
            // 改行コードの種類に関わらず正しく分割できるように、複数の改行コードを区切り文字として指定
            string[] newLineSeparators = { Environment.NewLine, "\r\n", "\n", "\r" };
            var lines = new List<string>(markdown.Split(newLineSeparators, StringSplitOptions.None));

            const string targetHeading = "### 有効ボタン一覧";
            const string eventHeading = "### イベント一覧";

            // ファイル全体から見出しを探索（先頭5行に限定しない）
            int buttonListHeadingIndex = lines.FindIndex(l => l.Trim() == targetHeading);

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
            {
                // ### 有効ボタン一覧 が見つからなかった場合、
                // まず ### イベント一覧 があるか調べ、あればその直前にセクションを追加する（ボタン一覧はイベントの前に置く）
                int eventHeadingIndex = lines.FindIndex(l => l.Trim() == eventHeading);
                var newSection = new List<string> { targetHeading, $"- {buttonName}", string.Empty };

                if (eventHeadingIndex != -1)
                {
                    // イベント見出しの直前に挿入
                    lines.InsertRange(eventHeadingIndex, newSection);
                }
                else
                {
                    // イベント見出しも無ければ、従来のフォールバック位置（先頭付近）に挿入する
                    // 最初の4行の中で、テキストが入っている最後の行のインデックスを探す
                    int lastNonEmptyIndex = -1;
                    for (int i = 0; i < Math.Min(lines.Count, 4); i++)
                    {
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

                    // 挿入
                    if (insertionIndex > 0 && insertionIndex - 1 < lines.Count && string.IsNullOrWhiteSpace(lines[insertionIndex - 1]))
                    {
                        lines.Insert(insertionIndex, targetHeading);
                        lines.Insert(insertionIndex + 1, $"- {buttonName}");
                    }
                    else
                    {
                        lines.Insert(insertionIndex, targetHeading);
                        lines.Insert(insertionIndex + 1, $"- {buttonName}");
                    }
                }
            }

            lines = NormalizeEmptyLines(lines);
            return string.Join(Environment.NewLine, lines);
        }

        /// <summary>
        /// 非条件（単純）イベントを追加する。
        /// - セクション「### イベント一覧」が存在すればそのセクション末尾に追加、
        ///   なければ適切な位置に見出しとともに追加する。
        /// - 挿入される行のフォーマットは: "- {ボタン名}押下 → {イベント先}"
        /// 
        /// 注意点:
        /// - 既存の同一行の重複チェックは行っていない（呼び出し元で検査されている想定）。
        /// - Markdown の見出しレベル（#, ##, ###）を単純に文字列比較で扱っているため、
        ///   より厳密な構文解析が必要な場合は Markdown パーサを導入すること。
        /// </summary>
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

                if (insertionIndex > lines.Count) insertionIndex = lines.Count;
                // 挿入前が空行でなければ一つ入れるが Normalize が整理する
                if (insertionIndex == lines.Count || !string.IsNullOrWhiteSpace(lines[Math.Max(0, insertionIndex - 1)]))
                    lines.Insert(insertionIndex, string.Empty);
                insertionIndex++;
                lines.Insert(insertionIndex, eventHeading);
                insertionIndex++;
            }

            // フォーマット: "- {ボタン名}押下 → {イベント先}"
            var newLine = $"- {buttonName}押下 → {eventTarget}";
            lines.Insert(insertionIndex, newLine);

            lines = NormalizeEmptyLines(lines);
            return string.Join(Environment.NewLine, lines);
        }

        /// <summary>
        /// 条件分岐イベント（複数ブランチ）を追加する。
        /// - 基本的な出力形式:
        ///   - {button}押下 →
        ///       - {condition1} → {target1}
        ///       - {condition2} → {target2}
        /// - branches 引数は (Condition, Target) のタプルリストで、null は ArgumentNullException とする。
        /// 
        /// 実装メモ:
        /// - まず "### イベント一覧" セクションの有無を確認し、存在しなければ新規追加する。
        /// - メイン行を追加した後、各分岐を 2 スペースインデントの "- " 行として追加する。
        /// - 入力の正規化（Trim）を行っているが、複雑な記法や入れ子構造には対応しない。
        /// </summary>
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
                // 見出しがある場合は、そのセクションの末尾を探して挿入位置仮決定
                insertionIndex = eventHeadingIndex + 1;
                while (insertionIndex < lines.Count)
                {
                    var t = lines[insertionIndex].Trim();
                    // セクション終端条件：空行または次の見出し
                    if (string.IsNullOrEmpty(t) || t.StartsWith("### ") || t.StartsWith("## ") || t.StartsWith("# "))
                        break;
                    insertionIndex++;
                }

                // --- 既存の親イベント行をセクション内で探す（存在すればそのブロック末尾に追加する） ---
                // 探索範囲は eventHeadingIndex+1 .. insertionIndex-1
                int sectionStart = eventHeadingIndex + 1;
                int sectionEnd = Math.Max(sectionStart, insertionIndex); // sectionEnd は挿入候補

                // 正規化した検索キーワード例: "{buttonName}押下"
                var buttonKey = (buttonName ?? string.Empty).Trim();
                bool parentFound = false;
                int parentLineIndex = -1;

                for (int i = sectionStart; i < sectionEnd; i++)
                {
                    var line = lines[i];
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    // トップレベル行のみ対象（先頭にスペースがない）
                    if (!line.StartsWith("  ") && !line.StartsWith("\t") && line.TrimStart().StartsWith("- "))
                    {
                        var top = line.Trim();
                        // match pattern "- {button}押下" または "- {button}押下 →" を含むか、"{button}押下" を部分一致で探す
                        if (top.Contains($"{buttonKey}押下") || top.StartsWith($"- {buttonKey}押下", StringComparison.Ordinal))
                        {
                            parentFound = true;
                            parentLineIndex = i;
                            break;
                        }
                    }
                }

                if (parentFound && parentLineIndex >= 0)
                {
                    // 親ブロックの終端を見つける（親行の次から、インデント行・空行を含む）
                    int next = parentLineIndex + 1;
                    while (next < lines.Count)
                    {
                        var l = lines[next];
                        // 終端条件: 次のトップレベル "- "（先頭にスペースが無い）または次の見出し
                        if (!string.IsNullOrWhiteSpace(l) && l.TrimStart().StartsWith("- ") && !l.StartsWith("  ") && !l.StartsWith("\t"))
                            break;
                        if (l.TrimStart().StartsWith("### ") || l.TrimStart().StartsWith("## ") || l.TrimStart().StartsWith("# "))
                            break;
                        next++;
                    }

                    // next は親ブロック直後のインデックス（挿入位置）
                    insertionIndex = next;

                    // 各分岐をインサート（2スペースインデント付き）
                    foreach (var (condition, target) in branches)
                    {
                        var cond = (condition ?? string.Empty).Trim();
                        var tgt = (target ?? string.Empty).Trim();
                        lines.Insert(insertionIndex, $"  - {cond} → {tgt}");
                        insertionIndex++;
                    }

                    lines = NormalizeEmptyLines(lines);
                    return string.Join(Environment.NewLine, lines);
                }
                // 親が見つからなければ以下で新規追加（既存の動作）
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

                if (insertionIndex == lines.Count || !string.IsNullOrWhiteSpace(lines[Math.Max(0, insertionIndex - 1)]))
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
                var cond = (condition ?? string.Empty).Trim();
                var tgt = (target ?? string.Empty).Trim();
                lines.Insert(insertionIndex, $"  - {cond} → {tgt}");
                insertionIndex++;
            }

            lines = NormalizeEmptyLines(lines);
            return string.Join(Environment.NewLine, lines);
        }

        /// <summary>
        /// タイムアウト（秒数）と、そのタイムアウト時の遷移先を Markdown に追加または更新する。
        /// 処理の流れ:
        /// 1) 2 行目に "- {N}秒でタイムアウト" を追加または上書き
        /// 2) "### イベント一覧" セクションを探して、"- タイムアウト → {target}へ" 行を上書きまたは追加
        /// 
        /// 注意点:
        /// - 既存の "でタイムアウト" 表記の検出は簡易な contains ベース。柔軟性より実用性を優先している。
        /// - イベント一覧がない場合は末尾にセクションを作る。
        /// </summary>
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
            // ❷ イベント一覧セクションの開始／終了位置を決定（再取得）
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

            int sectionStart = -1;
            int sectionEnd = -1;
            if (eventHeadingIndex != -1)
            {
                sectionStart = eventHeadingIndex + 1;
                sectionEnd = sectionStart;
                while (sectionEnd < lines.Count)
                {
                    var t = lines[sectionEnd].Trim();
                    if (string.IsNullOrEmpty(t) || t.StartsWith("### ") || t.StartsWith("## ") || t.StartsWith("# ")) break;
                    sectionEnd++;
                }
            }
            else
            {
                // イベント一覧がない場合は末尾にセクションを追加してその範囲を設定
                // 先に空行と見出しを追加
                lines.Add(string.Empty);
                lines.Add(eventHeading);
                sectionStart = lines.Count; // 新しいセクションの先頭（まだ要素はない）
                sectionEnd = sectionStart;
            }

            // -----------------------------
            // ❸ セクション内で既存の "- タイムアウト →" 行を探して上書き、なければセクション末尾へ挿入
            // -----------------------------
            int actionLineIndex = -1;
            if (sectionStart >= 0)
            {
                for (int i = sectionStart; i < sectionEnd; i++)
                {
                    if (lines[i].TrimStart().StartsWith("- タイムアウト →"))
                    {
                        actionLineIndex = i;
                        break;
                    }
                }

                string actionLine = $"- タイムアウト → {target}へ";

                if (actionLineIndex != -1)
                {
                    // 既存行を上書き（セクション内）
                    lines[actionLineIndex] = actionLine;
                }
                else
                {
                    // セクション末尾（sectionEnd）に挿入
                    lines.Insert(sectionEnd, actionLine);
                }
            }

            lines = NormalizeEmptyLines(lines);
            return string.Join(Environment.NewLine, lines);
        }


        /// <summary>
        /// Markdown ファイルの中で、GUI 上の要素位置（elements に基づく Y 順）に合わせて
        /// 「有効ボタン一覧」と「イベント一覧」の順序を再構築してファイルへ書き戻すユーティリティ。
        /// 
        /// 実装のポイント:
        /// - ファイルを丸ごと読み込み、行単位で操作した後、上書き保存する（File I/O を伴う）。
        /// - ReplaceListSection / ReplaceEventSection を使ってそれぞれのセクションを置換する。
        /// - 書き戻しは UTF-8 (System.Text.Encoding.UTF8) を用いる。
        /// 
        /// 注意:
        /// - ファイル操作で例外が発生する可能性がある（アクセス権、同時書き込み等）。呼び出し側で例外対処を行うか、
        ///   このメソッドを try/catch で囲むことを推奨する。
        /// - elements の内容（Name 非 null、Y 座標など）に依存して動作するため、事前検証を行っておくと安全。
        /// </summary>
        public static void UpdateMarkdownOrder(string markdownFilePath, IEnumerable<GuiElement> elements)
        {
            if (string.IsNullOrWhiteSpace(markdownFilePath) || !File.Exists(markdownFilePath)) return;
            if (elements == null) return;

            var lines = File.ReadAllLines(markdownFilePath).ToList();

            const string buttonHeading = "### 有効ボタン一覧";
            const string eventHeading = "### イベント一覧";

            int buttonIdx = lines.FindIndex(l => l.Trim() == buttonHeading);
            int eventIdx = lines.FindIndex(l => l.Trim() == eventHeading);

            var buttonList = BuildButtonList(elements);

            // ボタンリストが空なら見出しの挿入/置換をスキップ（空見出し禁止）
            if (buttonList.Count > 0)
            {
                if (buttonIdx != -1)
                {
                    lines = ReplaceListSection(lines, buttonHeading, buttonList);
                }
                else
                {
                    var newSection = new List<string> { buttonHeading };
                    foreach (var it in buttonList)
                        newSection.Add($"- {it}");
                    newSection.Add(string.Empty);

                    if (eventIdx != -1)
                    {
                        lines.InsertRange(eventIdx, newSection);
                    }
                    else
                    {
                        int insertAfter = -1;
                        for (int i = 0; i < Math.Min(lines.Count, 6); i++)
                        {
                            var t = lines[i].Trim();
                            if (t.StartsWith("## ") || t.StartsWith("# "))
                                insertAfter = i;
                        }

                        int insertionIndex = (insertAfter >= 0) ? insertAfter + 1 : lines.Count;
                        if (insertionIndex > lines.Count) insertionIndex = lines.Count;
                        lines.InsertRange(insertionIndex, newSection);
                    }
                }
            }
            // イベント一覧は常に（この時点でボタン節があれば前にある）置換する
            lines = ReplaceEventSection(lines, eventHeading, BuildEventBlockOrder(elements));

            // 正規化してから書き込み
            lines = NormalizeEmptyLines(lines);
            File.WriteAllLines(markdownFilePath, lines, Encoding.UTF8);
        }

        /// <summary>
        /// Button 要素を Y 座標順に並べ替えて名前リストを作成するヘルパー。
        /// - 戻り値はリストの各要素がリスト用の文字列（Name.Trim()）である。
        /// - 計算量: O(n log n) (OrderBy)
        /// </summary>
        private static List<string> BuildButtonList(IEnumerable<GuiElement> elements)
        {
            return elements
                .Where(e => e.Type == GuiElementType.Button && !string.IsNullOrWhiteSpace(e.Name))
                .OrderBy(e => e.Y)
                .Select(e => e.Name.Trim())
                .ToList();
        }

        /// <summary>
        /// Event 要素の名前を Y 座標順で抽出するヘルパー。
        /// - ReplaceEventSection の並べ替えキーとして使う。
        /// - 計算量: O(n log n)（OrderBy）
        /// </summary>
        private static List<string> BuildEventBlockOrder(IEnumerable<GuiElement> elements)
        {
            return elements
                .Where(e => e.Type == GuiElementType.Event && !string.IsNullOrWhiteSpace(e.Name))
                .OrderBy(e => e.Y)
                .Select(e => e.Name.Trim())
                .ToList();
        }

        /// <summary>
        /// 指定見出しセクションを単純なリストセクション（- item の順次行）として置き換える。
        /// アルゴリズム:
        /// - 見出し位置を探し、その直後から次の空行または次の見出しまでを既存セクションと見なす。
        /// - 新しい heading + items + 空行 を作成し、既存の該当範囲を置換する。
        /// 
        /// 注意:
        /// - セクション内に複雑なサブ構造がある場合（ネストやコードブロック等）は破壊的になる可能性がある。
        /// </summary>
        private static List<string> ReplaceListSection(List<string> lines, string heading, List<string> newItems)
        {
            if (newItems == null || newItems.Count == 0) return lines;

            int idx = lines.FindIndex(l => l.Trim() == heading);
            if (idx == -1) return lines;

            int insertion = idx + 1;
            int end = insertion;
            while (end < lines.Count)
            {
                var t = lines[end];
                if (string.IsNullOrWhiteSpace(t)) { end++; break; }
                if (t.TrimStart().StartsWith("### ") || t.TrimStart().StartsWith("## ") || t.TrimStart().StartsWith("# "))
                    break;
                end++;
            }

            var newSection = new List<string> { heading };
            foreach (var it in newItems)
                newSection.Add($"- {it}");
            newSection.Add(string.Empty);

            var result = new List<string>();
            result.AddRange(lines.Take(idx));
            result.AddRange(newSection);
            result.AddRange(lines.Skip(end));
            return result;
        }

        /// <summary>
        /// イベントセクションを「ブロック単位」で扱い、ブロックの順序を再構築する。
        /// ブロック定義:
        /// - トップレベルの "- " で始まる行をブロック先頭とし、
        /// - それに続くインデント（先頭に２つ以上のスペース、あるいはタブ）または空行をブロックの一部とする。
        /// 
        /// 並べ替えアルゴリズム:
        /// - blocksOrder (GuiElement.Name のリスト) に従い、各 desired 名称を含むブロックを前から順に採用する（部分一致）。
        /// - マッチしなかったブロックは後から元の順序で追加する（安定性のため）。
        /// 
        /// 設計上の注意:
        /// - マッチングは単純な部分文字列一致 (Contains) に依存するため、
        ///   異なるブロックに同じキーワードが含まれると意図しないマッチングが起きることがある。
        /// - ブロックの検出は行頭 "- " に依存しているため、Markdown の複雑な構造（ネストされたリスト、コードブロック中の行等）には脆弱。
        /// - より堅牢にする場合は完全な Markdown パーサを用いて AST を操作することを検討する。
        /// 
        /// 計算量: ブロック収集 O(n), 並べ替えマッチングは O(b * m)（b=ブロック数, m=blocksOrder 長）だが実用上小さい。
        /// </summary>
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
                    blocks.Add((keyCandidate, block));
                    cursor = next;
                }
                else
                {
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

            // 区切りの単一空行を追加（Normalize で整理される）
            result.Add(string.Empty);

            // Append the rest after endOfSectionCursor
            if (endOfSectionCursor < lines.Count)
                result.AddRange(lines.Skip(endOfSectionCursor));

            return result;
        }

        // 共通: 空行正規化ヘルパー（連続空行を1つに、先頭/末尾の不要な空行を削除）
        private static List<string> NormalizeEmptyLines(List<string> lines)
        {
            if (lines == null) return new List<string>();

            // find first/last non-blank to trim leading/trailing blanks
            int first = 0;
            while (first < lines.Count && string.IsNullOrWhiteSpace(lines[first])) first++;
            if (first >= lines.Count) return new List<string>();

            int last = lines.Count - 1;
            while (last >= 0 && string.IsNullOrWhiteSpace(lines[last])) last--;
            if (last < first) return new List<string>();

            var result = new List<string>();
            int i = first;
            while (i <= last)
            {
                var line = lines[i];
                if (!string.IsNullOrWhiteSpace(line))
                {
                    result.Add(line);
                    i++;
                    continue;
                }

                // 現在の行は空行。次の非空行を探す
                int j = i + 1;
                while (j <= last && string.IsNullOrWhiteSpace(lines[j])) j++;

                if (j > last)
                {
                    // 後続の非空行がない（末尾） → 終了
                    break;
                }

                // 前後の非空行を取得
                string prev = result.Count > 0 ? result[^1] : null;
                string next = lines[j];

                // 判定: 前後がリスト項目（トップレベル/ネストどちらでも）であれば空行を入れない
                bool prevIsList = prev != null && prev.TrimStart().StartsWith("- ");
                bool nextIsList = next.TrimStart().StartsWith("- ");

                if (prevIsList && nextIsList)
                {
                    // リスト内の項目間なので空行を挿入せずスキップ
                    i = j;
                    continue;
                }

                // それ以外はセクション区切りとして単一の空行を一つだけ入れる
                result.Add(string.Empty);
                i = j;
            }

            // 最後に念のため末尾の空行が残っていたら除去
            while (result.Count > 0 && string.IsNullOrWhiteSpace(result[^1]))
                result.RemoveAt(result.Count - 1);

            return result;
        }
    }


}
