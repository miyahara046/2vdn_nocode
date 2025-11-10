using Markdig;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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




    }
}
