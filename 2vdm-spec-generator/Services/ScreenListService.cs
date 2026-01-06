using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;

namespace _2vdm_spec_generator.Services
{
    /// <summary>
    /// "# 画面一覧" Markdown の探索・更新・読み取りを担当する。
    /// </summary>
    internal sealed class ScreenListService
    {
        private readonly UiToMarkdownConverter _uiToMd = new();

        public string FindScreenListFilePath(string selectedFolderPath, string selectedItemFullPath)
        {
            string basePath = selectedFolderPath;
            if (string.IsNullOrWhiteSpace(basePath) && !string.IsNullOrWhiteSpace(selectedItemFullPath))
                basePath = Path.GetDirectoryName(selectedItemFullPath);

            if (string.IsNullOrWhiteSpace(basePath) || !Directory.Exists(basePath))
                return null;

            try
            {
                var mdFiles = Directory.GetFiles(basePath, "*.md", SearchOption.AllDirectories);
                foreach (var f in mdFiles)
                {
                    try
                    {
                        var first = File.ReadLines(f).FirstOrDefault() ?? string.Empty;
                        if (first.TrimStart().StartsWith("# 画面一覧", StringComparison.OrdinalIgnoreCase))
                            return f;
                    }
                    catch
                    {
                    }
                }
            }
            catch
            {
            }

            return null;
        }

        public IEnumerable<string> GetScreenNames(string selectedFolderPath)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(selectedFolderPath) || !Directory.Exists(selectedFolderPath))
                    return Enumerable.Empty<string>();

                var path = FindScreenListFilePath(selectedFolderPath, selectedItemFullPath: null);
                if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                    return Enumerable.Empty<string>();

                var lines = File.ReadAllLines(path);
                int idx = Array.FindIndex(lines, l => !string.IsNullOrWhiteSpace(l) && l.TrimStart().StartsWith("# 画面一覧", StringComparison.OrdinalIgnoreCase));
                if (idx < 0) return Enumerable.Empty<string>();

                var results = new List<string>();
                for (int i = idx + 1; i < lines.Length; i++)
                {
                    var t = lines[i].Trim();
                    if (string.IsNullOrEmpty(t)) continue;
                    if (t.StartsWith("#")) break;
                    if (t.StartsWith("- "))
                    {
                        var name = t.Substring(2).Trim();
                        if (name.EndsWith("へ")) name = name.Substring(0, name.Length - 1).Trim();
                        if (!string.IsNullOrWhiteSpace(name)) results.Add(name);
                    }
                }

                return results.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            }
            catch
            {
                return Enumerable.Empty<string>();
            }
        }

        public async Task EnsureScreenListHasClassAsync(string selectedFolderPath, string selectedItemFullPath, string className)
        {
            if (string.IsNullOrWhiteSpace(className)) return;

            try
            {
                string basePath = selectedFolderPath;
                if (string.IsNullOrWhiteSpace(basePath) && !string.IsNullOrWhiteSpace(selectedItemFullPath))
                    basePath = Path.GetDirectoryName(selectedItemFullPath);

                if (string.IsNullOrWhiteSpace(basePath) || !Directory.Exists(basePath))
                    return;

                var screenListFile = FindScreenListFilePath(basePath, selectedItemFullPath: null);

                if (screenListFile == null)
                {
                    screenListFile = Path.Combine(basePath, "ScreenList.md");
                    var init = "# 画面一覧" + Environment.NewLine + Environment.NewLine;
                    File.WriteAllText(screenListFile, init, Encoding.UTF8);
                }

                var content = File.ReadAllText(screenListFile);
                var lines = content.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None).ToList();
                var targetItem = $"- {className}";

                bool exists = lines.Any(l => string.Equals((l ?? string.Empty).Trim(), targetItem, StringComparison.OrdinalIgnoreCase));
                if (exists) return;

                var newContent = _uiToMd.AddScreenList(content, className);
                File.WriteAllText(screenListFile, newContent, Encoding.UTF8);

                // 実用性優先：ここで通知（ViewModelに戻したいなら戻してOK）
                if (Application.Current?.MainPage != null)
                {
                    await Application.Current.MainPage.DisplayAlert("更新", $"画面一覧に '{className}' を追加しました。", "OK");
                }
            }
            catch (Exception ex)
            {
                if (Application.Current?.MainPage != null)
                {
                    await Application.Current.MainPage.DisplayAlert("エラー", $"画面一覧更新中にエラーが発生しました: {ex.Message}", "OK");
                }
            }
        }
    }
}
