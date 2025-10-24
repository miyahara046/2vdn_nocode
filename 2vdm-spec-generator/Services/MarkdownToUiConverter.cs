using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using _2vdm_spec_generator.ViewModel;

namespace _2vdm_spec_generator.Services
{
    internal class MarkdownToUiConverter
    {
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
                            Description = ""
                        });
                    }
                }
                return elements;
            }
            else if (lines[0].Trim().StartsWith("## ")) // '## 'で始まる行をすべて許容する
            {
                // 2行目（index=1）だけを見て Timeout 名を抽出（"- " の後ろから最初の 'で' まで）
                if (lines.Count > 1)
                {
                    var second = lines[1].Trim();
                    if (second.StartsWith("- "))
                    {
                        var content = second.Length > 2 ? second.Substring(2).Trim() : string.Empty;
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
                            // Timeout が見つかっても、ボタン一覧などの検出を行いたければ return を外して続行できます。
                            // 今回は2行目で Timeout を取ったらその段階で返す挙動を維持します。
                          
                        }
                    }
                }

                // 3行目以降（index=2 から）で "### 有効ボタン一覧" を探し、その下の "- " 行を Button として追加
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
                                if (sub.StartsWith("- "))
                                {
                                    var name = sub.Substring(2).Trim();
                                    elements.Add(new GuiElement
                                    {
                                        Type = GuiElementType.Button,
                                        Name = name,
                                        Description = ""
                                    });
                                }
                                else if (sub.StartsWith("### ") || sub.StartsWith("## "))
                                {
                                    // 次のセクションで終了
                                    break;
                                }
                            }
                            return elements;
                        }
                    }
                }
                return elements;
            }
           
            return elements;
        }
    }
}
