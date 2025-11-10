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
                                var sub = lines[k].Trim();

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

                                        var correspondingButton = buttonElements.FirstOrDefault(b => b.Name == opName);

                                        // 対応するボタンがあれば、TargetとDescriptionを設定
                                        if (correspondingButton != null)
                                        {
                                            correspondingButton.Target = targetName;
                                            correspondingButton.Description = targetName; 
                                        }
                                        
                                        // targetNameが空の場合はネストされたリストを解析
                                        if (string.IsNullOrEmpty(targetName))
                                        {
                                            // ネストされたリストの解析 (kをインクリメントし、次の行から処理を開始)
                                            int startK = k + 1;
                                            while (startK < lines.Count && lines[startK].TrimStart().StartsWith("-") && !lines[startK].TrimStart().StartsWith("--"))
                                            {
                                                var nestedSub = lines[startK].Trim();
                                                if (nestedSub.StartsWith("- "))
                                                {
                                                    var nestedContent = nestedSub.Substring(2).Trim();
                                                    var nestedMatch = EventPattern.Match(nestedContent);
                                                    if (nestedMatch.Success)
                                                    {
                                                        var nestedTarget = nestedMatch.Groups["Target"].Value.Trim();
                                                        elements.Add(new GuiElement
                                                        {
                                                            Type = GuiElementType.Event, 
                                                            Name = nestedTarget,             
                                                            Target = nestedTarget,
                                                            Description = ""
                                                        });
                                                    }
                                                }
                                                startK++;
                                            }
                                            k = startK - 1; // kを読み進めた位置に戻す
                                        }
                                        else
                                        {
                                            // 押下イベントをEventとして追加 (Targetが空でない場合のみ)
                                            elements.Add(new GuiElement
                                            {
                                                Type = GuiElementType.Event, 
                                                Name = targetName,             
                                                Target = targetName,
                                                Description = ""
                                            });
                                        }
                                    }
                                    else
                                    {
                                        // 通常のイベント解析 (EventPatternを使用)
                                        var eventMatch = EventPattern.Match(content);
                                        if (eventMatch.Success)
                                        {
                                            var target = eventMatch.Groups["Target"].Value.Trim(); 
                                            elements.Add(new GuiElement
                                            {
                                                Type = GuiElementType.Event,
                                                Name = target, 
                                                Target = target,
                                                Description = ""
                                            });
                                        }
                                        else
                                        {
                                            // '→' がない、またはその他の通常のイベント
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
                        EndOfEventList:;
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