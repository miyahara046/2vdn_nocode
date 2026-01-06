using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using _2vdm_spec_generator.ViewModel;

namespace _2vdm_spec_generator.Services
{
    /// <summary>
    /// .positions.json の読み書きを担当する。
    /// ViewModel から JSON I/O を剥がし、位置情報永続化の責務を集約する。
    /// </summary>
    internal sealed class GuiPositionStore
    {
        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true
        };

        public void AddOrUpdatePositionEntry(string markdownPath, string name, float x, float y)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(markdownPath) || string.IsNullOrWhiteSpace(name)) return;

                var posPath = Path.ChangeExtension(markdownPath, ".positions.json");
                var list = Read(posPath);

                var found = list.FirstOrDefault(p => string.Equals((p.Name ?? string.Empty).Trim(), name.Trim(), StringComparison.Ordinal));
                if (found == null)
                {
                    list.Add(new GuiElementPosition { Name = name.Trim(), X = x, Y = y });
                }
                else
                {
                    found.X = x;
                    found.Y = y;
                }

                Write(posPath, list);
            }
            catch
            {
                // 保存失敗は黙殺（UI操作の妨げにしない）
            }
        }

        public void RenamePositionEntry(string markdownPath, string oldName, string newName)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(markdownPath)) return;
                if (string.IsNullOrWhiteSpace(oldName) || string.IsNullOrWhiteSpace(newName)) return;

                var posPath = Path.ChangeExtension(markdownPath, ".positions.json");
                if (!File.Exists(posPath)) return;

                var list = Read(posPath);
                var found = list.FirstOrDefault(p => string.Equals((p.Name ?? string.Empty).Trim(), oldName.Trim(), StringComparison.Ordinal));
                if (found == null) return;

                found.Name = newName.Trim();
                Write(posPath, list);
            }
            catch
            {
            }
        }

        private static List<GuiElementPosition> Read(string posPath)
        {
            if (!File.Exists(posPath)) return new List<GuiElementPosition>();

            var json = File.ReadAllText(posPath);
            return JsonSerializer.Deserialize<List<GuiElementPosition>>(json) ?? new List<GuiElementPosition>();
        }

        private static void Write(string posPath, List<GuiElementPosition> list)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(posPath) ?? string.Empty);
            File.WriteAllText(posPath, JsonSerializer.Serialize(list, JsonOptions), Encoding.UTF8);
        }
    }
}
