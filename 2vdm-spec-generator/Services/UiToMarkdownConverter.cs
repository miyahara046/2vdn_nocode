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
            var lines = string.IsNullOrWhiteSpace(markdown)
              ? new List<string>()
              : markdown.Split(Environment.NewLine).ToList();

            if (lines.Count < 2)
                lines[2] = $"- {screenName}";
            else
                lines.Add($"- {screenName}");

            return string.Join(Environment.NewLine, lines);
        }
    }
}
