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

            // それ以外は従来通り
            foreach (var line in lines)
            {
                var match = Regex.Match(line, @"^##\s*Button:\s*(.+?)\s*-\s*(.+)$");
                if (match.Success)
                {
                    elements.Add(new GuiElement
                    {
                        Type = GuiElementType.Button,
                        Name = match.Groups[1].Value.Trim(),
                        Description = match.Groups[2].Value.Trim()
                    });
                    continue;
                }
                match = Regex.Match(line, @"^##\s*Event:\s*(.+?)\s*-\s*(.+)$");
                if (match.Success)
                {
                    elements.Add(new GuiElement
                    {
                        Type = GuiElementType.Event,
                        Name = match.Groups[1].Value.Trim(),
                        Description = match.Groups[2].Value.Trim()
                    });
                    continue;
                }
                match = Regex.Match(line, @"^##\s*Timeout:\s*(.+?)\s*-\s*(.+)$");
                if (match.Success)
                {
                    elements.Add(new GuiElement
                    {
                        Type = GuiElementType.Timeout,
                        Name = match.Groups[1].Value.Trim(),
                        Description = match.Groups[2].Value.Trim()
                    });
                    continue;
                }
            }
            return elements;
        }
    }
}
