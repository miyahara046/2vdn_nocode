using _2vdm_spec_generator.Services;
using _2vdm_spec_generator.View;
using CommunityToolkit.Maui.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.Controls;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Text; // 追加: Encoding 等のため

namespace _2vdm_spec_generator.ViewModel
{
    public partial class NoCodePageViewModel : ObservableObject
    {
        // ========== バインド可能プロパティ (ObservableProperty により自動でプロパティが生成される) ===========

        [ObservableProperty] private string selectedFolderPath;
        [ObservableProperty] private FolderItem selectedItem;
        [ObservableProperty] private string markdownContent;
        [ObservableProperty] private string vdmContent;
        [ObservableProperty] private bool isClassAddButtonVisible;
        [ObservableProperty] private bool isScreenListAddButtonVisible;
        [ObservableProperty] private bool isClassAllButtonVisible;
        [ObservableProperty] private bool isFolderSelected = true;
        [ObservableProperty]
        private string diagramTitle = "Condition Transition Map";
        [ObservableProperty]
        private ObservableCollection<GuiElement> guiElements = new();
        [ObservableProperty]
　　　　private GuiElement selectedGuiElement;

        public ObservableCollection<FolderItem> FolderItems { get; } = new();

        private readonly string mdFileName = "NewClass.md";

        // ===== フォルダ選択 =====
        [RelayCommand]
        private async Task SelectFolderAsync()
        {
#if WINDOWS
            var hwnd = ((MauiWinUIWindow)App.Current.Windows[0].Handler.PlatformView).WindowHandle;
            var picker = new Windows.Storage.Pickers.FolderPicker();
            picker.FileTypeFilter.Add("*");
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

            var folder = await picker.PickSingleFolderAsync();
            if (folder != null)
            {
                SelectedFolderPath = folder.Path;
                LoadFolderItems();

                // 選択したフォルダ自体を SelectedItem に設定（ツリーの root として表示されないケースに備える）
                var existing = FolderItems.FirstOrDefault(f => string.Equals(f.FullPath, SelectedFolderPath, StringComparison.OrdinalIgnoreCase));
                if (existing != null)
                {
                    SelectedItem = existing;
                }
                else
                {
                    SelectedItem = new FolderItem
                    {
                        Name = Path.GetFileName(SelectedFolderPath),
                        FullPath = SelectedFolderPath,
                        Level = 0,
                    };
                }
            }
            IsFolderSelected = false;
#else
            await Application.Current.MainPage.DisplayAlert("未対応", "このプラットフォームではフォルダ選択は未対応です。", "OK");
#endif
        }

        // ===== フォルダ読み込み =====
        private void LoadFolderItems()
        {
            FolderItems.Clear();
            if (string.IsNullOrWhiteSpace(SelectedFolderPath)) return;

            foreach (var dir in Directory.GetDirectories(SelectedFolderPath))
                AddFolderRecursive(dir, 0);

            foreach (var file in Directory.GetFiles(SelectedFolderPath).Where(f => Path.GetExtension(f).ToLower() == ".md"))
                FolderItems.Add(new FolderItem { Name = Path.GetFileName(file), FullPath = file, Level = 0 });
        }

        private string ExtractDiagramTitleFromMarkdown(string markdown, FolderItem fileItem)
        {
            const string defaultTitle = "Condition Transition Map";
            if (fileItem == null) return defaultTitle;

            if (string.IsNullOrWhiteSpace(markdown))
                return Path.GetFileNameWithoutExtension(fileItem.FullPath) ?? defaultTitle;

            var lines = markdown.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            foreach (var l in lines)
            {
                var t = l?.Trim();
                if (string.IsNullOrEmpty(t)) continue;
                if (t.StartsWith("# ") || t.StartsWith("## "))
                {
                    var title = t.TrimStart('#').Trim();
                    if (string.Equals(title, "画面一覧", StringComparison.OrdinalIgnoreCase))
                        return defaultTitle;
                    return title;
                }
            }

            return Path.GetFileNameWithoutExtension(fileItem.FullPath) ?? defaultTitle;
        }

        /// <summary>
        /// 指定フォルダを FolderItems に追加し、そのフォルダ内の .md ファイルを追加してからサブフォルダを再帰的に追加する。
        /// </summary>
        private void AddFolderRecursive(string path, int level)
        {
            var folderItem = new FolderItem
            {
                Name = Path.GetFileName(path),
                FullPath = path,
                Level = level,
                IsExpanded = true
            };
            FolderItems.Add(folderItem);

            foreach (var file in Directory.GetFiles(path).Where(f => Path.GetExtension(f).ToLower() == ".md"))
            {
                FolderItems.Add(new FolderItem
                {
                    Name = Path.GetFileName(file),
                    FullPath = file,
                    Level = level + 1
                });
            }

            foreach (var dir in Directory.GetDirectories(path))
                AddFolderRecursive(dir, level + 1);
        }


        // ===== 折りたたみ =====
        [RelayCommand]
        private void ToggleExpand(FolderItem folder)
        {
            if (folder == null || !folder.IsFolder) return;

            // クリックして展開/折りたたみしたフォルダを選択状態にする
            SelectedItem = folder;
            IsFolderSelected = false;

            folder.IsExpanded = !folder.IsExpanded;

            foreach (var item in FolderItems)
            {
                if (item.FullPath.StartsWith(folder.FullPath) && item.Level > folder.Level)
                    item.IsVisible = folder.IsExpanded;
            }
        }

        // ===== ファイル/フォルダ選択 =====
        [RelayCommand]
        private void SelectItem(FolderItem item)
        {
            if (item == null) return;

            // フォルダ・ファイルどちらでも選択状態にする
            SelectedItem = item;

            if (item.IsFile)
            {
                LoadMarkdownAndVdm(item.FullPath);
            }
            else if (item.IsFolder)
            {
                // フォルダが選ばれたときはエディタ内容をクリアして、そのフォルダを操作対象にする
                MarkdownContent = string.Empty;
                VdmContent = string.Empty;
                GuiElements = new ObservableCollection<GuiElement>();
                DiagramTitle = Path.GetFileName(item.FullPath) ?? "Condition Transition Map";

                // フォルダ選択画面は閉じる想定（SelectFolderAsync と同様の UI 切替）
                IsFolderSelected = false;
            }
        }


        private void LoadMarkdownAndVdm(string path)
        {
            MarkdownContent = File.Exists(path) ? File.ReadAllText(path) : "";

            var converter = new MarkdownToVdmConverter();
            VdmContent = converter.ConvertToVdm(MarkdownContent);

            string firstLine = File.ReadLines(path).FirstOrDefault() ?? "";
            if (firstLine.TrimStart().StartsWith("##", StringComparison.OrdinalIgnoreCase))
            {
                IsClassAddButtonVisible = false;
                IsScreenListAddButtonVisible = false;
                IsClassAllButtonVisible = true;
            }
            else if (firstLine.StartsWith("# 画面一覧"))
            {
                IsClassAddButtonVisible = false;
                IsScreenListAddButtonVisible = true;
                IsClassAllButtonVisible = false;
            }
            else
            {
                IsClassAddButtonVisible = true;
                IsScreenListAddButtonVisible = false;
                IsClassAllButtonVisible = false;
            }

            var uiConverter = new MarkdownToUiConverter();
            GuiElements = new ObservableCollection<GuiElement>(uiConverter.Convert(MarkdownContent));

            LoadGuiPositionsToElements();

            var displayItem = SelectedItem ?? new FolderItem { FullPath = path, Name = Path.GetFileName(path), Level = 0 };
            DiagramTitle = ExtractDiagramTitleFromMarkdown(MarkdownContent, displayItem);

            // 追加: Renderer に渡す画面名集合（正規化済み）
            ScreenNamesForRenderer = GetScreenManagementScreenNames();
        }

        // ===== 新規 Markdown 作成 =====
        [RelayCommand]
        private async Task CreateNewMdFileAsync()
        {
            if (SelectedItem == null)
            {
                await Shell.Current.DisplayAlert("エラー", "ファイルまたはフォルダが選択されていません", "OK");
                return;
            }

            string targetDir = SelectedItem.IsFile
                ? Path.GetDirectoryName(SelectedItem.FullPath)
                : SelectedItem.FullPath;

            string fileName = await Shell.Current.DisplayPromptAsync(
                "新規ファイル作成",
                "ファイル名を入力してください（拡張子不要）",
                "作成", "キャンセル", placeholder: "NewClass"
            );

            if (string.IsNullOrWhiteSpace(fileName)) return;
            if (!fileName.EndsWith(".md")) fileName += ".md";

            string newPath = Path.Combine(targetDir, fileName);
            if (File.Exists(newPath))
            {
                await Shell.Current.DisplayAlert("エラー", "同名ファイルが存在します", "OK");
                return;
            }

            File.WriteAllText(newPath, "New Class\n");

            LoadFolderItems();

            SelectedItem = FolderItems.FirstOrDefault(f => f.FullPath == newPath);
            ResolveSelectedItemByPath(newPath);
            LoadMarkdownAndVdm(newPath);

            IsClassAddButtonVisible = true;


        }

        [RelayCommand]
        private async Task RenameClassAsync()
        {
            if (SelectedItem == null || string.IsNullOrWhiteSpace(SelectedItem.FullPath) || !SelectedItem.IsFile)
            {
                await Application.Current.MainPage.DisplayAlert("エラー", "クラス名を変更するファイルが選択されていません。", "OK");
                return;
            }

            string path = SelectedItem.FullPath;
            string markdown = File.Exists(path) ? File.ReadAllText(path) : string.Empty;
            var lines = markdown.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None).ToList();

            // 変更対象の見出し行を探す（優先: "## "、次に "# " だが "# 画面一覧" は除外）
            int headingIndex = -1;
            bool isDoubleHash = false;
            for (int i = 0; i < lines.Count; i++)
            {
                var t = lines[i].TrimStart();
                if (t.StartsWith("## "))
                {
                    headingIndex = i;
                    isDoubleHash = true;
                    break;
                }
            }
            if (headingIndex == -1)
            {
                for (int i = 0; i < lines.Count; i++)
                {
                    var t = lines[i].TrimStart();
                    if (t.StartsWith("# ") && !t.StartsWith("# 画面一覧", StringComparison.OrdinalIgnoreCase))
                    {
                        headingIndex = i;
                        isDoubleHash = false;
                        break;
                    }
                }
            }

            // 既存クラス名の決定（見出しがなければファイル名ベースを既存名とする）
            string oldName;
            if (headingIndex != -1)
            {
                // 見出しから # を取り除いてトリム
                oldName = lines[headingIndex].TrimStart('#').Trim();
            }
            else
            {
                oldName = Path.GetFileNameWithoutExtension(path);
            }

            // 入力ダイアログ（初期値は既存名をプレースホルダに）
            string newName = await Shell.Current.DisplayPromptAsync(
                "クラス名変更",
                "新しいクラス名を入力してください",
                "変更", "キャンセル",
                placeholder: oldName
            );

            if (string.IsNullOrWhiteSpace(newName)) return;
            newName = newName.Trim();
            if (string.Equals(oldName, newName, StringComparison.Ordinal)) return;

            try
            {
                // 1) Markdown 内の見出しを置換（見出しがない場合は先頭行に追加）
                if (headingIndex != -1)
                {
                    lines[headingIndex] = (isDoubleHash ? "## " : "# ") + newName;
                }
                else
                {
                    // ファイルが空または見出し無し -> 先頭に "## newName" を入れる（既存の挙動に合わせる）
                    lines.Insert(0, "## " + newName);
                }

                var newMarkdown = string.Join(Environment.NewLine, lines);

                // 2) ファイル書き込みと VDM++ 再生成
                File.WriteAllText(path, newMarkdown, Encoding.UTF8);

                var converter = new MarkdownToVdmConverter();
                string newVdm = converter.ConvertToVdm(newMarkdown);
                File.WriteAllText(Path.ChangeExtension(path, ".vdmpp"), newVdm, Encoding.UTF8);

                // 3) 画面一覧ファイル（SelectedFolderPath 配下）にあれば "- {oldName}" を "- {newName}" に置換
                var screenListPath = FindScreenListFilePath();
                if (!string.IsNullOrWhiteSpace(screenListPath) && File.Exists(screenListPath))
                {
                    try
                    {
                        var screenLines = File.ReadAllLines(screenListPath).ToList();
                        bool updated = false;
                        for (int i = 0; i < screenLines.Count; i++)
                        {
                            var trimmed = screenLines[i].Trim();
                            if (trimmed.StartsWith("- ", StringComparison.Ordinal))
                            {
                                // リスト項目が完全一致する場合に置換（大文字小文字を無視）
                                if (string.Equals(trimmed.Substring(2).Trim(), oldName, StringComparison.OrdinalIgnoreCase))
                                {
                                    screenLines[i] = "- " + newName;
                                    updated = true;
                                }
                            }
                        }
                        if (updated)
                        {
                            File.WriteAllLines(screenListPath, screenLines, Encoding.UTF8);
                            // screen list の VDM++ も再生成
                            var screenMd = string.Join(Environment.NewLine, screenLines);
                            var screenVdm = converter.ConvertToVdm(screenMd);
                            File.WriteAllText(Path.ChangeExtension(screenListPath, ".vdmpp"), screenVdm, Encoding.UTF8);
                            // ツリー更新
                            LoadFolderItems();
                        }
                    }
                    catch
                    {
                        // 画面一覧更新失敗は無視（必要ならログ表示）
                    }
                }

                // 4) ViewModel プロパティを更新して UI を再解析（タイトル・GUI要素更新含む）
                MarkdownContent = newMarkdown;
                VdmContent = newVdm;
                LoadMarkdownAndVdm(path);

                await Application.Current.MainPage.DisplayAlert("完了", $"クラス名を '{oldName}' から '{newName}' に変更しました。", "OK");
            }
            catch (Exception ex)
            {
                await Application.Current.MainPage.DisplayAlert("エラー", $"クラス名変更中にエラーが発生しました: {ex.Message}", "OK");
            }
        }

        [RelayCommand]
　　　　　private async Task AddClassHeadingAsync()
        {
            if (SelectedItem == null || !SelectedItem.IsFile) return;

            // 画面一覧ファイルが存在するか確認（SelectedFolderPath または SelectedItem の親フォルダを探索）
            var screenListPath = FindScreenListFilePath();

            string classType;
            if (screenListPath != null)
            {
                // 画面一覧が既にある場合は種類選択を行わず「クラスの追加」を固定
                classType = "画面クラスの追加";
            }
            else
            {
                // 存在しなければユーザーに選ばせる
                classType = await Shell.Current.DisplayActionSheet(
                    "追加するクラスの種類を選んでください",
                    "キャンセル", null,
                    "画面一覧クラスの追加", "画面クラスの追加"
                );

                if (string.IsNullOrEmpty(classType) || classType == "キャンセル") return;
            }

            string className = null;
            string inputName = null;
            if (classType == "画面クラスの追加")
            {
                // ユーザー入力か自動遷移（画面一覧がある場合）どちらでもクラス名を取得する
                string temp = await Shell.Current.DisplayPromptAsync(
                    "画面クラス追加", "クラス名を入力してください", "OK", "キャンセル", placeholder: "MyClass"
                );
                if (string.IsNullOrWhiteSpace(temp)) return;
                inputName = temp.Trim();
                className = $"# {inputName}";
            }

            string path = SelectedItem.FullPath;
            string currentMarkdown = File.Exists(path) ? File.ReadAllText(path) : string.Empty;

            var builder = new UiToMarkdownConverter();
            string newMarkdown = classType switch
            {
                "画面一覧クラスの追加" => builder.AddClassHeading(currentMarkdown, " 画面一覧"),
                "画面クラスの追加" => builder.AddClassHeading(currentMarkdown, className),
                _ => currentMarkdown
            };

            File.WriteAllText(path, newMarkdown, Encoding.UTF8);

            var converter = new MarkdownToVdmConverter();
            string vdmContent = converter.ConvertToVdm(newMarkdown);
            File.WriteAllText(Path.ChangeExtension(path, ".vdmpp"), vdmContent, Encoding.UTF8);

            MarkdownContent = newMarkdown;
            VdmContent = vdmContent;

            LoadMarkdownAndVdm(path);

            // クラス追加時は画面一覧に同名がなければ追加
            if (classType == "画面クラスの追加" && !string.IsNullOrWhiteSpace(inputName))
            {
                await EnsureScreenListHasClass(inputName);
            }
            // FolderItems が更新されて選択が外れる問題を防ぐため、FolderItems 内の実インスタンスに SelectedItem を再解決
            ResolveSelectedItemByPath(path);

            // 再表示のため Markdown を再ロードして UI を安定化
            LoadMarkdownAndVdm(path);
        }

        private string FindScreenListFilePath()
        {
            string basePath = SelectedFolderPath;
            if (string.IsNullOrWhiteSpace(basePath) && SelectedItem != null && !string.IsNullOrWhiteSpace(SelectedItem.FullPath))
                basePath = Path.GetDirectoryName(SelectedItem.FullPath);

            if (string.IsNullOrWhiteSpace(basePath) || !Directory.Exists(basePath))
                return null;

            try
            {
                var mdFiles = Directory.GetFiles(basePath, "*.md", SearchOption.AllDirectories);
                foreach (var f in mdFiles)
                {
                    try
                    {
                        var first = File.ReadLines(f).FirstOrDefault() ?? "";
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

        private async Task EnsureScreenListHasClass(string className)
        {
            if (string.IsNullOrWhiteSpace(className)) return;

            try
            {
                // まず SelectedFolderPath を基準に検索
                string basePath = SelectedFolderPath;
                if (string.IsNullOrWhiteSpace(basePath))
                {
                    // フォルダが未選択ならルート (SelectedItem の親など) を試す
                    if (SelectedItem != null && !string.IsNullOrWhiteSpace(SelectedItem.FullPath))
                    {
                        basePath = Path.GetDirectoryName(SelectedItem.FullPath);
                    }
                }

                if (string.IsNullOrWhiteSpace(basePath) || !Directory.Exists(basePath))
                {
                    return;
                }

                var mdFiles = Directory.GetFiles(basePath, "*.md", SearchOption.AllDirectories);

                // 先頭行が "# 画面一覧" のファイルを探す
                string screenListFile = mdFiles.FirstOrDefault(f =>
                {
                    try
                    {
                        var first = File.ReadLines(f).FirstOrDefault() ?? "";
                        return first.TrimStart().StartsWith("# 画面一覧", StringComparison.OrdinalIgnoreCase);
                    }
                    catch
                    {
                        return false;
                    }
                });

                // 見つからなければルートに作成する
                if (screenListFile == null)
                {
                    screenListFile = Path.Combine(basePath, "ScreenList.md");
                    var init = "# 画面一覧" + Environment.NewLine + Environment.NewLine;
                    File.WriteAllText(screenListFile, init, Encoding.UTF8);
                }

                // 内容読み込みと重複チェック
                var content = File.ReadAllText(screenListFile);
                var lines = content.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None).ToList();
                var targetItem = $"- {className}";
                bool exists = lines.Any(l => string.Equals(l?.Trim(), targetItem, StringComparison.OrdinalIgnoreCase));
                if (!exists)
                {
                    var conv = new UiToMarkdownConverter();
                    var newContent = conv.AddScreenList(content, className);
                    File.WriteAllText(screenListFile, newContent, Encoding.UTF8);

                    LoadFolderItems();

                    await Application.Current.MainPage.DisplayAlert("更新", $"画面一覧に '{className}' を追加しました。", "OK");
                }
            }
            catch (Exception ex)
            {
                await Application.Current.MainPage.DisplayAlert("エラー", $"画面一覧更新中にエラーが発生しました: {ex.Message}", "OK");
            }
        }

        // ===== Markdown 保存 =====
        [RelayCommand]
        private void SaveMarkdown()
        {
            if (SelectedItem == null || !SelectedItem.IsFile) return;

            File.WriteAllText(SelectedItem.FullPath, MarkdownContent);

            var converter = new MarkdownToVdmConverter();
            VdmContent = converter.ConvertToVdm(MarkdownContent);
            File.WriteAllText(Path.ChangeExtension(SelectedItem.FullPath, ".vdmpp"), VdmContent);
        }

        // ===== VDM++ 変換（保存なし）=====
        [RelayCommand]
        private void ConvertToVdm()
        {
            if (SelectedItem == null || !SelectedItem.IsFile) return;

            string mdPath = SelectedItem.FullPath;
            var converter = new MarkdownToVdmConverter();
            VdmContent = converter.ConvertToVdm(File.ReadAllText(mdPath));
            File.WriteAllText(Path.ChangeExtension(mdPath, ".vdmpp"), VdmContent);
        }

        [RelayCommand]
        private async Task AddScreenListAsync()
        {
            if (SelectedItem == null || !SelectedItem.IsFile) return;

            string screenName = await Shell.Current.DisplayPromptAsync(
                "画面追加", "画面名を入力してください", "OK", "キャンセル", placeholder: "MyScreen"
            );
            if (string.IsNullOrWhiteSpace(screenName)) return;

            string path = SelectedItem.FullPath;
            string currentMarkdown = File.ReadAllText(path);

            var builder = new UiToMarkdownConverter();
            string newMarkdown = builder.AddScreenList(currentMarkdown, screenName.Trim());
            File.WriteAllText(path, newMarkdown);

            var converter = new MarkdownToVdmConverter();
            string vdmContent = converter.ConvertToVdm(newMarkdown);
            File.WriteAllText(Path.ChangeExtension(path, ".vdmpp"), vdmContent);

            MarkdownContent = newMarkdown;
            VdmContent = vdmContent;
        }

        [RelayCommand]
        private async Task AddBottonAsync()
        {
            if (SelectedItem == null || !SelectedItem.IsFile) return;

            string buttonName = await Shell.Current.DisplayPromptAsync(
                "ボタン", "ボタン名を入力してください", "OK", "キャンセル", placeholder: "Buton"
            );
            if (string.IsNullOrWhiteSpace(buttonName)) return;

            // 重複チェック（ViewModel 内の GuiElements を見て確認）
            var normalized = buttonName.Trim();
            bool existsInGui = GuiElements.Any(g => g.Type == GuiElementType.Button && !string.IsNullOrWhiteSpace(g.Name)
                                                    && string.Equals(g.Name.Trim(), normalized, StringComparison.OrdinalIgnoreCase));
            if (existsInGui)
            {
                await Application.Current.MainPage.DisplayAlert("重複", $"既に同名のボタン \"{normalized}\" が存在します。", "OK");
                return;
            }

            string path = SelectedItem.FullPath;
            string currentMarkdown = File.ReadAllText(path);

            var builder = new UiToMarkdownConverter();
            string newMarkdown = builder.AddButton(currentMarkdown, normalized);
            File.WriteAllText(path, newMarkdown);

            var converter = new MarkdownToVdmConverter();
            string vdmContent = converter.ConvertToVdm(newMarkdown);
            File.WriteAllText(Path.ChangeExtension(path, ".vdmpp"), vdmContent);

            MarkdownContent = newMarkdown;
            VdmContent = vdmContent;

            // 再読込して GuiElements を更新（AddButton でマークダウンが変わったので反映）
            LoadMarkdownAndVdm(path);
        }
        public async Task EditSelectedNodeAsync()
        {
            var el = SelectedGuiElement;
            if (el == null) return;

            // 分岐が選択されているなら分岐編集を優先
            if (SelectedBranchIndex.HasValue)
            {
                await EditSelectedBranchAsync();   // ← 分岐編集（後で実装）
                return;
            }

            // ノード種別で分岐
            switch (el.Type)
            {
                case GuiElementType.Button:
                    await RenameSelectedButtonAsync();   // いま作ったやつ
                    break;

                case GuiElementType.Screen:
                    await RenameSelectedScreenAsync();   // 画面名変更（必要なら）
                    break;

                case GuiElementType.Event:
                    await EditSelectedEventAsync();      // 遷移先や説明など
                    break;

                case GuiElementType.Timeout:
                    await EditSelectedTimeoutAsync();    // 秒数/遷移先
                    break;

                default:
                    await Application.Current.MainPage.DisplayAlert("情報", "このノードは編集対象外です。", "OK");
                    break;
            }
        }


        [RelayCommand]
        public async Task RenameSelectedButtonAsync()
        {
            var el = SelectedGuiElement;
            if (el == null || el.Type != GuiElementType.Button)
            {
                await Application.Current.MainPage.DisplayAlert("情報", "ボタンノードを選択してから実行してください。", "OK");
                return;
            }
            if (SelectedItem == null || string.IsNullOrWhiteSpace(SelectedItem.FullPath)) return;

            string oldName = (el.Name ?? "").Trim();
            if (string.IsNullOrWhiteSpace(oldName)) return;

            string newName = await Shell.Current.DisplayPromptAsync(
                "ボタン名変更",
                $"\"{oldName}\" の新しいボタン名を入力してください",
                "OK", "キャンセル",
                placeholder: "NewButtonName",
                initialValue: oldName
            );

            if (string.IsNullOrWhiteSpace(newName)) return;
            newName = newName.Trim();

            if (string.Equals(oldName, newName, StringComparison.Ordinal))
                return;

            // 重複チェック（同一ファイル内のボタン名）
            bool exists = GuiElements.Any(g =>
                g.Type == GuiElementType.Button &&
                !string.IsNullOrWhiteSpace(g.Name) &&
                !ReferenceEquals(g, el) &&
                string.Equals(g.Name.Trim(), newName, StringComparison.OrdinalIgnoreCase));

            if (exists)
            {
                await Application.Current.MainPage.DisplayAlert("重複", $"既に同名のボタン \"{newName}\" が存在します。", "OK");
                return;
            }

            string mdPath = SelectedItem.FullPath;

            try
            {
                string currentMarkdown = File.Exists(mdPath) ? File.ReadAllText(mdPath) : string.Empty;

                string updatedMarkdown = RenameButtonInMarkdown(currentMarkdown, oldName, newName);

                // 変化が無いなら中断（見つからなかった等）
                if (string.Equals(currentMarkdown, updatedMarkdown, StringComparison.Ordinal))
                {
                    await Application.Current.MainPage.DisplayAlert("情報", "置換対象が見つかりませんでした（ボタン一覧/イベント一覧の形式を確認してください）。", "OK");
                    return;
                }

                File.WriteAllText(mdPath, updatedMarkdown);

                // VDM++ 再生成
                var vdmConv = new MarkdownToVdmConverter();
                string newVdm = vdmConv.ConvertToVdm(updatedMarkdown);
                File.WriteAllText(Path.ChangeExtension(mdPath, ".vdmpp"), newVdm);

                // positions.json の名前キーもリネーム（位置保持）
                RenamePositionEntry(mdPath, oldName, newName);

                // 反映
                MarkdownContent = updatedMarkdown;
                VdmContent = newVdm;

                LoadMarkdownAndVdm(mdPath);

                // 選択の復元（同名要素があると曖昧なので Button 型だけ拾う）
                SelectedGuiElement = GuiElements.FirstOrDefault(g =>
                    g.Type == GuiElementType.Button &&
                    !string.IsNullOrWhiteSpace(g.Name) &&
                    string.Equals(g.Name.Trim(), newName, StringComparison.Ordinal));

                SelectedBranchIndex = null;
            }
            catch
            {
                await Application.Current.MainPage.DisplayAlert("エラー", "ボタン名変更に失敗しました。", "OK");
            }
        }

        private string RenameButtonInMarkdown(string markdown, string oldName, string newName)
        {
            if (markdown == null) markdown = string.Empty;

            var lines = markdown.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None).ToList();

            bool inButtonList = false;
            bool changed = false;

            for (int i = 0; i < lines.Count; i++)
            {
                string raw = lines[i];
                string trimStart = raw.TrimStart();
                string trim = raw.Trim();

                // セクション判定
                if (trimStart.StartsWith("### 有効ボタン一覧"))
                {
                    inButtonList = true;
                }
                else if (inButtonList && (trimStart.StartsWith("### ") || trimStart.StartsWith("## ")))
                {
                    inButtonList = false;
                }

                // 1) ボタン一覧内の "- 旧名" を置換
                if (inButtonList)
                {
                    // インデントは維持
                    if (trim == $"- {oldName}")
                    {
                        int lead = raw.Length - trimStart.Length;
                        string indent = raw.Substring(0, lead);
                        lines[i] = indent + $"- {newName}";
                        changed = true;
                        continue;
                    }
                }

                // 2) イベント一覧の "- 旧名押下..." を置換（条件分岐/非分岐どちらも親行に効く）
                // 例: "- ボタン1押下 → 画面A"
                //     "- ボタン1押下 →"
                //     "- ボタン1押下"
                if (trimStart.StartsWith("- "))
                {
                    string afterDash = trimStart.Substring(2); // "- " の後
                    string prefix = oldName + "押下";

                    if (afterDash.StartsWith(prefix, StringComparison.Ordinal))
                    {
                        // "旧名押下" の後ろを全部保持して、"新名押下" に差し替える
                        string rest = afterDash.Substring(prefix.Length);
                        int lead = raw.Length - trimStart.Length;
                        string indent = raw.Substring(0, lead);
                        lines[i] = indent + "- " + newName + "押下" + rest;
                        changed = true;
                        continue;
                    }
                }
            }

            if (!changed) return markdown;
            return string.Join(Environment.NewLine, lines);
        }

        private void RenamePositionEntry(string mdPath, string oldName, string newName)
        {
            try
            {
                var posPath = Path.ChangeExtension(mdPath, ".positions.json");
                if (!File.Exists(posPath)) return;

                var json = File.ReadAllText(posPath);
                var list = JsonSerializer.Deserialize<List<GuiElementPosition>>(json);
                if (list == null || list.Count == 0) return;

                bool changed = false;
                foreach (var p in list)
                {
                    if (string.Equals((p.Name ?? "").Trim(), oldName, StringComparison.Ordinal))
                    {
                        p.Name = newName;
                        changed = true;
                    }
                }

                if (!changed) return;

                var options = new JsonSerializerOptions { WriteIndented = true };
                File.WriteAllText(posPath, JsonSerializer.Serialize(list, options));
            }
            catch
            {
                // 無視（ログ追加するならここ）
            }
        }

        public async Task EditSelectedEventAsync()
        {
            var el = SelectedGuiElement;
            if (el == null || el.Type != GuiElementType.Event)
            {
                await Application.Current.MainPage.DisplayAlert("情報", "イベントノードを選択してから実行してください。", "OK");
                return;
            }
            if (SelectedItem == null || string.IsNullOrWhiteSpace(SelectedItem.FullPath)) return;

            // 分岐イベントはここでは扱わない（別コマンド推奨）
            if (el.IsConditional)
            {
                await Application.Current.MainPage.DisplayAlert("情報", "分岐イベントは「分岐編集」で対応してください。", "OK");
                return;
            }

            string mdPath = SelectedItem.FullPath;

            // 既存の表示名（通常は遷移先名）
            string oldTarget = (el.Name ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(oldTarget))
                oldTarget = (el.Target ?? string.Empty).Trim();

            if (string.IsNullOrWhiteSpace(oldTarget))
            {
                await Application.Current.MainPage.DisplayAlert("情報", "編集対象の遷移先が取得できませんでした。", "OK");
                return;
            }

            string newTarget = await Shell.Current.DisplayPromptAsync(
                "イベント遷移先変更",
                $"遷移先を変更する（現在: \"{oldTarget}\"）\n新しい遷移先を入力してください。",
                "OK", "キャンセル",
                placeholder: "例: 画面A",
                initialValue: oldTarget
            );

            if (string.IsNullOrWhiteSpace(newTarget)) return;
            newTarget = newTarget.Trim();

            if (string.Equals(oldTarget, newTarget, StringComparison.Ordinal))
                return;

            try
            {
                string currentMarkdown = File.Exists(mdPath) ? File.ReadAllText(mdPath) : string.Empty;

                string updatedMarkdown = ReplaceEventTargetInMarkdown(currentMarkdown, oldTarget, newTarget);

                if (string.Equals(currentMarkdown, updatedMarkdown, StringComparison.Ordinal))
                {
                    await Application.Current.MainPage.DisplayAlert(
                        "情報",
                        "置換対象が見つかりませんでした（イベント一覧の形式が想定と異なる可能性があります）。",
                        "OK"
                    );
                    return;
                }

                File.WriteAllText(mdPath, updatedMarkdown);

                // VDM++ 再生成
                var vdmConv = new MarkdownToVdmConverter();
                var newVdm = vdmConv.ConvertToVdm(updatedMarkdown);
                File.WriteAllText(Path.ChangeExtension(mdPath, ".vdmpp"), newVdm);

                // positions.json の名前キーをリネーム（イベントノードの表示名が変わるため）
                RenamePositionEntry(mdPath, oldTarget, newTarget);

                // 反映
                MarkdownContent = updatedMarkdown;
                VdmContent = newVdm;

                LoadMarkdownAndVdm(mdPath);

                // 選択復元（Eventで newTarget 名のもの）
                SelectedGuiElement = GuiElements.FirstOrDefault(g =>
                    g.Type == GuiElementType.Event &&
                    !string.IsNullOrWhiteSpace(g.Name) &&
                    string.Equals(g.Name.Trim(), newTarget, StringComparison.Ordinal));

                SelectedBranchIndex = null;
            }
            catch
            {
                await Application.Current.MainPage.DisplayAlert("エラー", "イベント編集に失敗しました。", "OK");
            }
        }
        private string ReplaceEventTargetInMarkdown(string markdown, string oldTarget, string newTarget)
        {
            if (markdown == null) markdown = string.Empty;

            var lines = markdown.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None).ToList();

            bool inEventList = false;
            bool changed = false;

            for (int i = 0; i < lines.Count; i++)
            {
                string raw = lines[i];
                string trimStart = raw.TrimStart();
                string trim = raw.Trim();

                // イベント一覧セクションの開始/終了
                if (trimStart.StartsWith("### イベント一覧", StringComparison.Ordinal))
                {
                    inEventList = true;
                    continue;
                }
                if (inEventList && (trimStart.StartsWith("### ", StringComparison.Ordinal) || trimStart.StartsWith("## ", StringComparison.Ordinal) || trimStart.StartsWith("# ", StringComparison.Ordinal)))
                {
                    inEventList = false;
                }

                if (!inEventList) continue;

                // ネスト行（分岐）はここでは変更しない（分岐編集で対応）
                if (raw.StartsWith("  ") || raw.StartsWith("\t"))
                    continue;

                // "- ..." の行だけ対象
                if (!trimStart.StartsWith("- ", StringComparison.Ordinal))
                    continue;

                string content = trimStart.Substring(2).Trim();

                // 形式1: "何か → 遷移先"
                int arrowIdx = content.IndexOf('→');
                if (arrowIdx >= 0)
                {
                    string left = content.Substring(0, arrowIdx).Trim();
                    string right = content.Substring(arrowIdx + 1).Trim();

                    // right が oldTarget と完全一致する場合のみ置換（誤爆防止）
                    if (string.Equals(right, oldTarget, StringComparison.Ordinal))
                    {
                        int lead = raw.Length - trimStart.Length;
                        string indent = raw.Substring(0, lead);

                        lines[i] = indent + "- " + left + " → " + newTarget;
                        changed = true;
                    }
                    continue;
                }

                // 形式2: "遷移先だけ" のイベント（まれ）
                if (string.Equals(content, oldTarget, StringComparison.Ordinal))
                {
                    int lead = raw.Length - trimStart.Length;
                    string indent = raw.Substring(0, lead);
                    lines[i] = indent + "- " + newTarget;
                    changed = true;
                }
            }

            return changed ? string.Join(Environment.NewLine, lines) : markdown;
        }

        public async Task EditSelectedBranchAsync()
        {
            var parent = SelectedGuiElement;
            var idxNullable = SelectedBranchIndex;

            if (parent == null || parent.Type != GuiElementType.Event)
            {
                await Application.Current.MainPage.DisplayAlert("情報", "分岐の親イベントが選択されていません。", "OK");
                return;
            }
            if (!idxNullable.HasValue)
            {
                await Application.Current.MainPage.DisplayAlert("情報", "分岐が選択されていません。", "OK");
                return;
            }
            if (!parent.IsConditional || parent.Branches == null || parent.Branches.Count == 0)
            {
                await Application.Current.MainPage.DisplayAlert("情報", "このイベントは分岐を持っていません。", "OK");
                return;
            }

            int branchIndex = idxNullable.Value;
            if (branchIndex < 0 || branchIndex >= parent.Branches.Count)
            {
                await Application.Current.MainPage.DisplayAlert("情報", "分岐番号が不正です。", "OK");
                return;
            }

            if (SelectedItem == null || string.IsNullOrWhiteSpace(SelectedItem.FullPath)) return;
            string mdPath = SelectedItem.FullPath;

            // 旧値
            var oldBranch = parent.Branches[branchIndex];
            string oldCond = (oldBranch.Condition ?? string.Empty).Trim();
            string oldTarget = (oldBranch.Target ?? string.Empty).Trim();

            // 新しい条件
            string newCond = await Shell.Current.DisplayPromptAsync(
                "分岐編集（条件）",
                $"分岐 {branchIndex + 1}/{parent.Branches.Count}\n条件を入力してください",
                "OK", "キャンセル",
                placeholder: "例: 表示部に1が入力されている",
                initialValue: oldCond
            );
            if (string.IsNullOrWhiteSpace(newCond)) return;
            newCond = newCond.Trim();

            // 新しい遷移先
            string newTarget = await Shell.Current.DisplayPromptAsync(
                "分岐編集（遷移先）",
                $"分岐 {branchIndex + 1}/{parent.Branches.Count}\n遷移先を入力してください",
                "OK", "キャンセル",
                placeholder: "例: 画面K",
                initialValue: oldTarget
            );
            if (newTarget == null) return; // キャンセル
            newTarget = newTarget.Trim();

            // 変化なしなら終了
            if (string.Equals(oldCond, newCond, StringComparison.Ordinal) &&
                string.Equals(oldTarget, newTarget, StringComparison.Ordinal))
            {
                return;
            }

            try
            {
                string currentMarkdown = File.Exists(mdPath) ? File.ReadAllText(mdPath) : string.Empty;

                // ★ 親イベントは Markdown 上では "- {parent.Name} →" 形式（例: "- ボタン1押下 →"）
                string parentEventLabel = (parent.Name ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(parentEventLabel))
                {
                    await Application.Current.MainPage.DisplayAlert("情報", "親イベント名が取得できませんでした。", "OK");
                    return;
                }

                string updatedMarkdown = ReplaceBranchLineInMarkdown(
                    currentMarkdown,
                    parentEventLabel,
                    branchIndex,
                    newCond,
                    newTarget
                );

                if (string.Equals(currentMarkdown, updatedMarkdown, StringComparison.Ordinal))
                {
                    await Application.Current.MainPage.DisplayAlert(
                        "情報",
                        "置換対象が見つかりませんでした（イベント一覧の形式やインデントを確認してください）。",
                        "OK"
                    );
                    return;
                }

                File.WriteAllText(mdPath, updatedMarkdown);

                // VDM++ 再生成
                var vdmConv = new MarkdownToVdmConverter();
                var newVdm = vdmConv.ConvertToVdm(updatedMarkdown);
                File.WriteAllText(Path.ChangeExtension(mdPath, ".vdmpp"), newVdm);

                // 反映して再解析
                MarkdownContent = updatedMarkdown;
                VdmContent = newVdm;

                LoadMarkdownAndVdm(mdPath);

                // 選択復元（親イベント）
                SelectedGuiElement = GuiElements.FirstOrDefault(g =>
                    g.Type == GuiElementType.Event &&
                    g.IsConditional &&
                    !string.IsNullOrWhiteSpace(g.Name) &&
                    string.Equals(g.Name.Trim(), parentEventLabel, StringComparison.Ordinal));

                SelectedBranchIndex = branchIndex;
            }
            catch
            {
                await Application.Current.MainPage.DisplayAlert("エラー", "分岐編集に失敗しました。", "OK");
            }
        }
        private string ReplaceBranchLineInMarkdown(
    string markdown,
    string parentEventLabel,  // 例: "ボタン1押下"
    int branchIndex,
    string newCondition,
    string newTarget
)
        {
            if (markdown == null) markdown = string.Empty;

            var lines = markdown.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None).ToList();

            bool inEventList = false;
            bool changed = false;

            for (int i = 0; i < lines.Count; i++)
            {
                string raw = lines[i];
                string trimStart = raw.TrimStart();

                // イベント一覧セクション検出
                if (trimStart.StartsWith("### イベント一覧", StringComparison.Ordinal))
                {
                    inEventList = true;
                    continue;
                }
                if (inEventList && (trimStart.StartsWith("### ", StringComparison.Ordinal) || trimStart.StartsWith("## ", StringComparison.Ordinal) || trimStart.StartsWith("# ", StringComparison.Ordinal)))
                {
                    inEventList = false;
                }
                if (!inEventList) continue;

                // 親行を探す: "- {parentEventLabel} →"（右辺が空の行）
                // 例: "- ボタン1押下 →"
                if (!trimStart.StartsWith("- ", StringComparison.Ordinal)) continue;

                string content = trimStart.Substring(2).Trim(); // "- " の後
                int arrowIdx = content.IndexOf('→');
                if (arrowIdx < 0) continue;

                string left = content.Substring(0, arrowIdx).Trim();
                string right = content.Substring(arrowIdx + 1).Trim();

                // 条件分岐の親行は右辺が空（"→" だけ、または空白だけ）
                if (!string.Equals(left, parentEventLabel, StringComparison.Ordinal)) continue;
                if (!string.IsNullOrEmpty(right)) continue;

                // 親行の直下に続くネスト行を走査し、branchIndex 番目の "- ..." を置換
                int found = -1;
                int j = i + 1;

                while (j < lines.Count)
                {
                    string nestedRaw = lines[j];
                    if (string.IsNullOrWhiteSpace(nestedRaw)) break;

                    // ネスト判定（MarkdownToUiConverter と同じ判定）
                    if (!(nestedRaw.StartsWith("  ") || nestedRaw.StartsWith("\t")))
                        break;

                    string nestedTrim = nestedRaw.TrimStart();

                    if (nestedTrim.StartsWith("- ", StringComparison.Ordinal))
                    {
                        found++;

                        if (found == branchIndex)
                        {
                            // 元のインデント維持
                            string indent = nestedRaw.Substring(0, nestedRaw.Length - nestedTrim.Length);

                            // 出力形式： "  - 条件 → 遷移先"
                            // 遷移先が空なら "  - 条件" だけにする（保険）
                            if (string.IsNullOrWhiteSpace(newTarget))
                                lines[j] = indent + "- " + newCondition;
                            else
                                lines[j] = indent + "- " + newCondition + " → " + newTarget;

                            changed = true;
                            break;
                        }
                    }

                    j++;
                }

                // 親イベントが見つかった時点で、変更できたかどうかに関わらず探索終了してよい
                break;
            }

            return changed ? string.Join(Environment.NewLine, lines) : markdown;
        }

        public async Task RenameSelectedScreenAsync()
        {
            var el = SelectedGuiElement;
            if (el == null || el.Type != GuiElementType.Screen)
            {
                await Application.Current.MainPage.DisplayAlert("情報", "画面ノードを選択してから実行してください。", "OK");
                return;
            }
            if (SelectedItem == null || string.IsNullOrWhiteSpace(SelectedItem.FullPath)) return;

            string mdPath = SelectedItem.FullPath;

            // 画面一覧ファイル（先頭が "# 画面一覧"）でのみ変更する
            string currentMarkdown = File.Exists(mdPath) ? File.ReadAllText(mdPath) : string.Empty;
            var firstLine = currentMarkdown.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None).FirstOrDefault()?.Trim();
            if (!string.Equals(firstLine, "# 画面一覧", StringComparison.Ordinal))
            {
                await Application.Current.MainPage.DisplayAlert("情報", "画面名変更は「# 画面一覧」ファイル上で実行してください。", "OK");
                return;
            }

            string oldName = (el.Name ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(oldName)) return;

            string newName = await Shell.Current.DisplayPromptAsync(
                "画面名変更",
                $"\"{oldName}\" の新しい画面名を入力してください",
                "OK", "キャンセル",
                placeholder: "例: 画面A",
                initialValue: oldName
            );
            if (string.IsNullOrWhiteSpace(newName)) return;
            newName = newName.Trim();

            if (string.Equals(oldName, newName, StringComparison.Ordinal)) return;

            // 重複チェック（画面一覧内）
            if (currentMarkdown.Contains("\n- " + newName) || currentMarkdown.Contains("\r\n- " + newName) || currentMarkdown.Contains("- " + newName + "\n") || currentMarkdown.Contains("- " + newName + "\r\n"))
            {
                await Application.Current.MainPage.DisplayAlert("重複", $"既に同名の画面 \"{newName}\" が存在します。", "OK");
                return;
            }

            try
            {
                string updatedMarkdown = RenameScreenInMarkdown(currentMarkdown, oldName, newName);

                if (string.Equals(currentMarkdown, updatedMarkdown, StringComparison.Ordinal))
                {
                    await Application.Current.MainPage.DisplayAlert("情報", "置換対象が見つかりませんでした。", "OK");
                    return;
                }

                File.WriteAllText(mdPath, updatedMarkdown);

                // 画面一覧は VDM 生成対象ではないかもしれないが、念のため更新しておく（必要なければ削ってOK）
                var vdmConv = new MarkdownToVdmConverter();
                var newVdm = vdmConv.ConvertToVdm(updatedMarkdown);
                File.WriteAllText(Path.ChangeExtension(mdPath, ".vdmpp"), newVdm);

                // positions のキー名も変更（位置維持）
                RenamePositionEntry(mdPath, oldName, newName);

                MarkdownContent = updatedMarkdown;
                VdmContent = newVdm;

                LoadMarkdownAndVdm(mdPath);

                SelectedGuiElement = GuiElements.FirstOrDefault(g =>
                    g.Type == GuiElementType.Screen &&
                    !string.IsNullOrWhiteSpace(g.Name) &&
                    string.Equals(g.Name.Trim(), newName, StringComparison.Ordinal));

                SelectedBranchIndex = null;
            }
            catch
            {
                await Application.Current.MainPage.DisplayAlert("エラー", "画面名変更に失敗しました。", "OK");
            }
        }
        private string RenameScreenInMarkdown(string markdown, string oldName, string newName)
        {
            if (markdown == null) markdown = string.Empty;

            var lines = markdown.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None).ToList();
            bool changed = false;

            for (int i = 0; i < lines.Count; i++)
            {
                var raw = lines[i];
                var trimStart = raw.TrimStart();
                var trim = raw.Trim();

                // "- 旧名" の完全一致だけ置換（誤爆防止）
                if (trim == $"- {oldName}")
                {
                    int lead = raw.Length - trimStart.Length;
                    string indent = raw.Substring(0, lead);
                    lines[i] = indent + $"- {newName}";
                    changed = true;
                }
            }

            return changed ? string.Join(Environment.NewLine, lines) : markdown;
        }
        public async Task EditSelectedTimeoutAsync()
        {
            var el = SelectedGuiElement;
            if (el == null || el.Type != GuiElementType.Timeout)
            {
                await Application.Current.MainPage.DisplayAlert("情報", "タイムアウトノードを選択してから実行してください。", "OK");
                return;
            }
            if (SelectedItem == null || string.IsNullOrWhiteSpace(SelectedItem.FullPath)) return;

            string mdPath = SelectedItem.FullPath;

            try
            {
                string currentMarkdown = File.Exists(mdPath) ? File.ReadAllText(mdPath) : string.Empty;
                var lines = currentMarkdown.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None).ToList();
                if (lines.Count < 2)
                {
                    await Application.Current.MainPage.DisplayAlert("情報", "タイムアウト定義（2行目）が存在しません。", "OK");
                    return;
                }

                // 画面仕様ファイル（先頭が "## "）でのみ編集する
                if (!lines[0].TrimStart().StartsWith("## ", StringComparison.Ordinal))
                {
                    await Application.Current.MainPage.DisplayAlert("情報", "タイムアウト編集は画面仕様（先頭が '## '）のMarkdownで実行してください。", "OK");
                    return;
                }

                // 現在のタイムアウト定義を解析（Converterと同じ思想）
                var parsed = ParseTimeoutDefinitionLine(lines[1]);
                if (parsed == null)
                {
                    await Application.Current.MainPage.DisplayAlert("情報", "タイムアウト定義行の形式が想定と異なります。", "OK");
                    return;
                }

                string oldTimeoutName = parsed.TimeoutName; // 例: "80秒"
                string oldTarget = parsed.Target;           // 例: "画面A" or ""

                // 新しいタイムアウト名（秒数など）
                string newTimeoutName = await Shell.Current.DisplayPromptAsync(
                    "タイムアウト編集（時間）",
                    $"タイムアウト時間（Name）を入力してください（現在: \"{oldTimeoutName}\"）\n例: 80秒",
                    "OK", "キャンセル",
                    placeholder: "例: 80秒",
                    initialValue: oldTimeoutName
                );
                if (string.IsNullOrWhiteSpace(newTimeoutName)) return;
                newTimeoutName = newTimeoutName.Trim();

                // 新しい遷移先（空OK）
                string newTarget = await Shell.Current.DisplayPromptAsync(
                    "タイムアウト編集（遷移先）",
                    "遷移先を入力してください（空なら遷移先なし）",
                    "OK", "キャンセル",
                    placeholder: "例: 画面A",
                    initialValue: oldTarget ?? string.Empty
                );
                if (newTarget == null) return; // キャンセル
                newTarget = newTarget.Trim();

                // 変更なしなら終了
                if (string.Equals(oldTimeoutName, newTimeoutName, StringComparison.Ordinal) &&
                    string.Equals(oldTarget ?? "", newTarget ?? "", StringComparison.Ordinal))
                {
                    return;
                }

                // 2行目を書き換え
                lines[1] = BuildTimeoutDefinitionLine(lines[1], newTimeoutName, newTarget);

                // イベント一覧の「タイムアウト系」の遷移も合わせて更新（誤爆しない範囲で）
                var updatedMarkdown = string.Join(Environment.NewLine, lines);
                updatedMarkdown = UpdateTimeoutReferencesInEventList(updatedMarkdown, oldTimeoutName, newTimeoutName, oldTarget ?? "", newTarget ?? "");

                File.WriteAllText(mdPath, updatedMarkdown);

                // VDM++ 再生成
                var vdmConv = new MarkdownToVdmConverter();
                var newVdm = vdmConv.ConvertToVdm(updatedMarkdown);
                File.WriteAllText(Path.ChangeExtension(mdPath, ".vdmpp"), newVdm);

                // timeoutノード名が変わるので positions もリネーム（位置維持）
                if (!string.Equals(oldTimeoutName, newTimeoutName, StringComparison.Ordinal))
                {
                    RenamePositionEntry(mdPath, oldTimeoutName, newTimeoutName);
                }

                MarkdownContent = updatedMarkdown;
                VdmContent = newVdm;

                LoadMarkdownAndVdm(mdPath);

                SelectedGuiElement = GuiElements.FirstOrDefault(g =>
                    g.Type == GuiElementType.Timeout &&
                    !string.IsNullOrWhiteSpace(g.Name) &&
                    string.Equals(g.Name.Trim(), newTimeoutName, StringComparison.Ordinal));

                SelectedBranchIndex = null;
            }
            catch
            {
                await Application.Current.MainPage.DisplayAlert("エラー", "タイムアウト編集に失敗しました。", "OK");
            }
        }
        private sealed class TimeoutDef
        {
            public string TimeoutName { get; set; } = "";
            public string Target { get; set; } = "";
        }

        private TimeoutDef? ParseTimeoutDefinitionLine(string line)
        {
            if (line == null) return null;

            var raw = line.Trim();
            if (!raw.StartsWith("- ")) return null;

            var content = raw.Substring(2).Trim();

            // "A → B" の形
            int arrow = content.IndexOf('→');
            string left = arrow >= 0 ? content.Substring(0, arrow).Trim() : content;
            string right = arrow >= 0 ? content.Substring(arrow + 1).Trim() : "";

            // "〇〇で..." の "〇〇" を TimeoutName にする（Converter準拠）
            int de = left.IndexOf('で');
            string timeoutName = de > 0 ? left.Substring(0, de).Trim() : left.Trim();

            if (string.IsNullOrWhiteSpace(timeoutName)) return null;

            return new TimeoutDef
            {
                TimeoutName = timeoutName,
                Target = right
            };
        }

        // 元のインデントを維持して "- {newName}でタイムアウト" (+ " → target") を組み立てる
        private string BuildTimeoutDefinitionLine(string originalLine, string newTimeoutName, string newTarget)
        {
            var raw = originalLine ?? "";
            var trimStart = raw.TrimStart();
            int lead = raw.Length - trimStart.Length;
            string indent = raw.Substring(0, Math.Max(0, lead));

            var baseText = $"- {newTimeoutName}でタイムアウト";
            if (!string.IsNullOrWhiteSpace(newTarget))
                baseText += $" → {newTarget}";

            return indent + baseText;
        }

        private string UpdateTimeoutReferencesInEventList(string markdown, string oldTimeoutName, string newTimeoutName, string oldTarget, string newTarget)
        {
            if (markdown == null) markdown = string.Empty;

            var lines = markdown.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None).ToList();
            bool inEventList = false;
            bool changed = false;

            for (int i = 0; i < lines.Count; i++)
            {
                var raw = lines[i];
                var trimStart = raw.TrimStart();
                var trim = raw.Trim();

                if (trimStart.StartsWith("### イベント一覧", StringComparison.Ordinal))
                {
                    inEventList = true;
                    continue;
                }
                if (inEventList && (trimStart.StartsWith("### ", StringComparison.Ordinal) || trimStart.StartsWith("## ", StringComparison.Ordinal) || trimStart.StartsWith("# ", StringComparison.Ordinal)))
                {
                    inEventList = false;
                }
                if (!inEventList) continue;

                // ネスト（分岐）は触らない
                if (raw.StartsWith("  ") || raw.StartsWith("\t")) continue;

                if (!trimStart.StartsWith("- ", StringComparison.Ordinal)) continue;

                string content = trimStart.Substring(2).Trim();
                int arrow = content.IndexOf('→');
                if (arrow < 0) continue;

                string left = content.Substring(0, arrow).Trim();
                string right = content.Substring(arrow + 1).Trim();

                // タイムアウト系っぽい左辺だけ対象（誤爆防止）
                // 例: "80秒でタイムアウト", "80秒でタイムアウト発生", "タイムアウト" など
                bool looksTimeout = left.Contains("タイムアウト", StringComparison.Ordinal) &&
                                    (left.Contains(oldTimeoutName, StringComparison.Ordinal) || string.Equals(oldTimeoutName, "タイムアウト", StringComparison.Ordinal));

                if (!looksTimeout) continue;

                // 左辺中の oldTimeoutName を newTimeoutName に置換（oldNameが含まれている場合のみ）
                if (!string.Equals(oldTimeoutName, newTimeoutName, StringComparison.Ordinal) && left.Contains(oldTimeoutName, StringComparison.Ordinal))
                {
                    left = left.Replace(oldTimeoutName, newTimeoutName);
                }

                // 遷移先の更新（oldTarget→newTarget を指定している場合のみ）
                if (!string.IsNullOrWhiteSpace(newTarget))
                {
                    if (string.IsNullOrWhiteSpace(oldTarget) || string.Equals(right, oldTarget, StringComparison.Ordinal))
                    {
                        right = newTarget;
                    }
                }

                int lead = raw.Length - trimStart.Length;
                string indent = raw.Substring(0, lead);
                lines[i] = indent + "- " + left + " → " + right;
                changed = true;
            }

            return changed ? string.Join(Environment.NewLine, lines) : markdown;
        }


        [RelayCommand]
        private async Task AddEventAsync()
        {
            if (SelectedItem == null || !SelectedItem.IsFile) return;

            var buttonNames = GuiElements?
                .Where(g => g.Type == GuiElementType.Button && !string.IsNullOrWhiteSpace(g.Name))
                .Select(g => g.Name.Trim())
                .Distinct()
                .ToArray() ?? Array.Empty<string>();

            if (buttonNames.Length == 0)
            {
                await Shell.Current.DisplayAlert("情報", "ファイル内にボタン定義が見つかりません。先にボタンを追加してください。", "OK");
                return;
            }

            string selectedButton = await Shell.Current.DisplayActionSheet(
                "イベントを追加するボタンを選んでください",
                "キャンセル", null,
                buttonNames
            );

            if (string.IsNullOrEmpty(selectedButton) || selectedButton == "キャンセル") return;

            bool isConditional = await Shell.Current.DisplayAlert(
                "条件分岐イベント",
                "このイベントに条件分岐を追加しますか？",
                "はい", "いいえ"
            );

            string path = SelectedItem.FullPath;
            string currentMarkdown = File.ReadAllText(path);
            var builder = new UiToMarkdownConverter();
            string newMarkdown;

            if (!isConditional)
            {
                // 非条件イベントでも ConditionInputPopup を再利用する（条件欄は非表示にする）
                var popup = new ConditionInputPopup
                {
                    RequireCondition = false
                };
                popup.UpdateRequireCondition();

                var result = await Shell.Current.CurrentPage.ShowPopupAsync(popup);
                if (result is not ValueTuple<string, string> values)
                    return;

                // popup は条件不要なので values.Item1 は null（または無視して良い）
                string eventName = values.Item2?.Trim();
                if (string.IsNullOrWhiteSpace(eventName)) return;

                // 重複チェック：マークダウン内に既に同一の "- {button}押下 → {target}" が存在するか
                string candidateLine = $"- {selectedButton}押下 → {eventName}";
                var lines = currentMarkdown.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None).Select(l => l.Trim()).ToArray();
                if (lines.Any(l => string.Equals(l, candidateLine, StringComparison.OrdinalIgnoreCase)))
                {
                    await Shell.Current.DisplayAlert("重複", $"同じイベント \"{candidateLine}\" は既に存在します。", "OK");
                    return;
                }

                newMarkdown = builder.AddEvent(currentMarkdown, selectedButton.Trim(), eventName);
            }
            else
            {
                // 条件分岐イベント（既存ロジックを維持）
                var lines = currentMarkdown.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
                bool blockExists = lines.Any(l => l.TrimStart().StartsWith($"- {selectedButton}押下", StringComparison.OrdinalIgnoreCase));

                if (blockExists)
                {
                    bool addAnyway = await Shell.Current.DisplayAlert("既存のイベントがあります", $"ボタン \"{selectedButton}\" には既にイベント定義があります。新しい条件分岐を追加しますか？", "追加する", "キャンセル");
                    if (!addAnyway) return;
                }

                var branches = new List<(string Condition, string Target)>();
                bool addMore = true;

                while (addMore)
                {
                    var popup = new ConditionInputPopup();
                    var result = await Shell.Current.CurrentPage.ShowPopupAsync(popup);

                    if (result is not ValueTuple<string, string> values)
                        break;

                    string condition = values.Item1;
                    string target = values.Item2;

                    branches.Add((condition, target));

                    addMore = await Shell.Current.DisplayAlert(
                        "追加", "別の条件分岐を追加しますか？", "はい", "いいえ");
                }

                if (branches.Count == 0) return;

                newMarkdown = builder.AddConditionalEvent(currentMarkdown, selectedButton.Trim(), branches);
            }

            // --- Markdown と VDM++ 出力更新 ---
            File.WriteAllText(path, newMarkdown);
            var converter = new MarkdownToVdmConverter();
            string vdmContent = converter.ConvertToVdm(newMarkdown);
            File.WriteAllText(Path.ChangeExtension(path, ".vdmpp"), vdmContent);

            MarkdownContent = newMarkdown;
            VdmContent = vdmContent;

            // 再読込して GuiElements を更新（マークダウン → GUI 表示の整合性を取る）
            LoadMarkdownAndVdm(path);
        }

        [RelayCommand]
        private async Task AddTimeoutEventAsync()
        {
            if (SelectedItem == null || !SelectedItem.IsFile) return;

            // TimeoutPopup で秒数とターゲットを入力してもらう
            var popup = new TimeoutPopup();
            var result = await Shell.Current.CurrentPage.ShowPopupAsync(popup);

            if (result is not ValueTuple<int, string> timeoutData)
                return;

            int seconds = timeoutData.Item1;
            string target = timeoutData.Item2;

            string path = SelectedItem.FullPath;
            string currentMarkdown = File.ReadAllText(path);

            var builder = new UiToMarkdownConverter();
            string newMarkdown = builder.AddTimeoutEvent(currentMarkdown, seconds, target);
            File.WriteAllText(path, newMarkdown);

            var converter = new MarkdownToVdmConverter();
            string vdmContent = converter.ConvertToVdm(newMarkdown);
            File.WriteAllText(Path.ChangeExtension(path, ".vdmpp"), vdmContent);

            MarkdownContent = newMarkdown;
            VdmContent = vdmContent;
        }


        private void EnsureGuiPositionsJsonExists()
        {
            if (SelectedItem == null || string.IsNullOrWhiteSpace(SelectedItem.FullPath)) return;

            string posPath = Path.ChangeExtension(SelectedItem.FullPath, ".positions.json");

            if (File.Exists(posPath)) return; // 既にある場合は何もしない

            // 現在の GUI 要素を元に JSON を作成
            var list = GuiElements.Select(e => new GuiElementPosition
            {
                Name = e.Name,
                X = e.X,
                Y = e.Y
            }).ToList();

            if (list.Count == 0) return; // 要素がなければ作らない

            try
            {
                var options = new JsonSerializerOptions { WriteIndented = true };
                string json = JsonSerializer.Serialize(list, options);
                File.WriteAllText(posPath, json);
            }
            catch
            {
                // 保存失敗は無視（必要ならログ出力を追加する）
            }
        }



        // ===== スタートページに戻る =====

        /// <summary>
        /// Shell ナビゲーションを利用してスタートページへ遷移する。
        /// ルートに戻る操作の実装例。URI は AppShell のルーティングに依存する。
        /// </summary>
        [RelayCommand]
        private async Task GoToStartPageAsync()
        {
            await Shell.Current.GoToAsync("//StartPage");
        }

        partial void OnMarkdownContentChanged(string value)
        {
            // Markdownが変更されたら GUI 要素を更新
            var converter = new MarkdownToUiConverter();
            GuiElements = new ObservableCollection<GuiElement>(converter.Convert(value));

            // タイムアウトは固定フラグ（ユーザーが移動できない）
            foreach (var el in GuiElements.Where(g => g.Type == GuiElementType.Timeout))
                el.IsFixed = true;

            // 位置保存ファイルがあれば読み込んで反映
            LoadGuiPositionsToElements();

            // GUI 要素がある場合は位置 JSON を確実に作成しておく（存在しない場合のみ作成）
            EnsureGuiPositionsJsonExists();

            // 追加: 編集時にも選択ファイルがあればタイトルを更新する
            if (SelectedItem != null)
            {
                DiagramTitle = ExtractDiagramTitleFromMarkdown(value, SelectedItem);
            }
            else
            {
                DiagramTitle = "Condition Transition Map";
            }
        }

        public void SaveGuiPositions(IEnumerable<GuiElement> elements)
        {
            try
            {
                if (SelectedItem == null || string.IsNullOrWhiteSpace(SelectedItem.FullPath)) return;

                var list = elements.Select(e => new GuiElementPosition
                {
                    Name = e.Name,
                    X = e.X,
                    Y = e.Y
                }).ToList();

                var options = new JsonSerializerOptions { WriteIndented = true };
                var json = JsonSerializer.Serialize(list, options);

                var posPath = Path.ChangeExtension(SelectedItem.FullPath, ".positions.json");
                File.WriteAllText(posPath, json);
            }
            catch
            {
                // 保存失敗は UI に響かないように黙殺（必要ならログに出す）
            }
        }

        private void LoadGuiPositionsToElements()
        {
            try
            {
                if (SelectedItem == null || string.IsNullOrWhiteSpace(SelectedItem.FullPath))
                    return;

                var posPath = Path.ChangeExtension(SelectedItem.FullPath, ".positions.json");

                // JSON ファイルが存在しない場合はスキップ
                if (!File.Exists(posPath))
                    return;

                var json = File.ReadAllText(posPath);
                var list = JsonSerializer.Deserialize<List<GuiElementPosition>>(json);

                if (list == null || list.Count == 0)
                    return;

                // GUI 要素に反映（名前でマッチさせる）
                foreach (var pos in list)
                {
                    var el = GuiElements.FirstOrDefault(e => e.Name == pos.Name);
                    if (el != null)
                    {
                        el.X = pos.X;
                        el.Y = pos.Y;
                    }
                }
            }
            catch
            {
                // エラー時も安全に無視（将来はログ出力やユーザー通知を検討）
                return;
            }
        }

        [RelayCommand]
        public async Task DeleteSelectedGuiElementAsync()
        {
            var el = SelectedGuiElement;
            if (el == null) return;
            if (SelectedItem == null || string.IsNullOrWhiteSpace(SelectedItem.FullPath)) return;

            // 分岐が選択されている場合は分岐単体の削除フローに入る
            if (SelectedBranchIndex.HasValue && el.Branches != null && SelectedBranchIndex.Value >= 0 && SelectedBranchIndex.Value < el.Branches.Count)
            {
                var branch = el.Branches[SelectedBranchIndex.Value];
                string branchLabel = string.IsNullOrWhiteSpace(branch.Condition) ? branch.Target ?? "(未指定)" : $"{branch.Condition} -> {branch.Target}";
                bool okBranch = await Shell.Current.DisplayAlert("分岐削除確認", $"この分岐 \"{branchLabel}\" を削除しますか？", "はい", "いいえ");
                if (!okBranch) return;

                var mdPath = SelectedItem.FullPath;
                try
                {
                    string currentMarkdown = File.Exists(mdPath) ? File.ReadAllText(mdPath) : string.Empty;
                    string newMarkdown = RemoveBranchFromMarkdown(currentMarkdown, el, SelectedBranchIndex.Value);

                    File.WriteAllText(mdPath, newMarkdown);

                    // VDM++ 再生成
                    var converter = new MarkdownToVdmConverter();
                    var newVdm = converter.ConvertToVdm(newMarkdown);
                    File.WriteAllText(Path.ChangeExtension(mdPath, ".vdmpp"), newVdm);

                    // positions.json の更新（親イベント自体は残るので positions は基本的に変わらないが安全のため再書込）
                    RemovePositionEntry(mdPath, el.Name);

                    // ViewModel のプロパティを更新して UI を再構築
                    MarkdownContent = newMarkdown;
                    VdmContent = newVdm;

                    // 再解析して GuiElements を更新
                    LoadMarkdownAndVdm(mdPath);

                    // 選択解除
                    SelectedBranchIndex = null;
                    SelectedGuiElement = null;
                }
                catch
                {
                    await Application.Current.MainPage.DisplayAlert("エラー", "分岐削除に失敗しました。", "OK");
                }

                return;
            }

            // 既存の要素削除フロー（要素全体を削除）
            bool ok = await Shell.Current.DisplayAlert("削除確認", $"\"{el.Name}\" をこのファイルから削除しますか？", "はい", "いいえ");
            if (!ok) return;

            var mdPathFull = SelectedItem.FullPath;
            try
            {
                // Markdown 編集
                string currentMarkdown = File.Exists(mdPathFull) ? File.ReadAllText(mdPathFull) : string.Empty;
                string newMarkdown = RemoveElementFromMarkdown(currentMarkdown, el);

                File.WriteAllText(mdPathFull, newMarkdown);

                // VDM++ 再生成
                var converter = new MarkdownToVdmConverter();
                var newVdm = converter.ConvertToVdm(newMarkdown);
                File.WriteAllText(Path.ChangeExtension(mdPathFull, ".vdmpp"), newVdm);

                // positions.json から削除（存在する場合）
                RemovePositionEntry(mdPathFull, el.Name);

                // ViewModel のプロパティを更新して UI を再構築
                MarkdownContent = newMarkdown;
                VdmContent = newVdm;

                // 再解析して GuiElements を更新
                LoadMarkdownAndVdm(mdPathFull);

                // 選択解除
                SelectedGuiElement = null;
            }
            catch
            {
                await Application.Current.MainPage.DisplayAlert("エラー", "削除処理に失敗しました。", "OK");
            }
        }

        private string RemoveElementFromMarkdown(string markdown, GuiElement el)
        {
            if (markdown == null) markdown = string.Empty;

            var lines = markdown.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None).ToList();
            string name = (el.Name ?? string.Empty).Trim();

            if (string.IsNullOrEmpty(name))
                return markdown; // 名前がない要素は削除対象とできない

            bool changed = false;

            // 1) 有効ボタン一覧等の "- {name}" 単一行を削除（全体）
            for (int i = lines.Count - 1; i >= 0; i--)
            {
                if (lines[i].Trim() == $"- {name}")
                {
                    lines.RemoveAt(i);
                    changed = true;
                }
            }

            // 2) イベントブロック（"- {name}押下" または "- {name}押下 → ...", その後に続くインデント行）を削除
            for (int i = 0; i < lines.Count; i++)
            {
                var trimmed = lines[i].TrimStart();
                if (trimmed.StartsWith("- ") && trimmed.Contains($"{name}押下"))
                {
                    // remove top line and subsequent indented/nested lines
                    int start = i;
                    int j = i + 1;
                    while (j < lines.Count)
                    {
                        if (string.IsNullOrWhiteSpace(lines[j])) { j++; continue; }
                        // stop if next top-level item or heading
                        var t = lines[j].TrimStart();
                        if (t.StartsWith("- ") && !lines[j].StartsWith("  ")) break;
                        if (t.StartsWith("#")) break;
                        j++;
                    }
                    lines.RemoveRange(start, j - start);
                    changed = true;
                    i = Math.Max(0, start - 1);
                }
            }

            // 3) イベント要素（Event）を削除したい場合、先頭行テキストに完全一致するブロックを削除
            if (el.Type == GuiElementType.Event)
            {
                for (int i = 0; i < lines.Count; i++)
                {
                    var trimmed = lines[i].TrimStart();
                    // マッチ条件を緩めにして部分一致で検出
                    if (trimmed.StartsWith("- ") && trimmed.Contains(name))
                    {
                        int start = i;
                        int j = i + 1;
                        while (j < lines.Count)
                        {
                            if (string.IsNullOrWhiteSpace(lines[j])) { j++; continue; }
                            var t = lines[j].TrimStart();
                            if (t.StartsWith("- ") && !lines[j].StartsWith("  ")) break;
                            if (t.StartsWith("#")) break;
                            j++;
                        }
                        lines.RemoveRange(start, j - start);
                        changed = true;
                        i = Math.Max(0, start - 1);
                    }
                }
            }

            // 4) 画面一覧（"- {name}"）などで残る可能性がある重複も上で削除済みなのでそのまま
            if (!changed) return markdown;

            return string.Join(Environment.NewLine, lines);
        }

        private void RemovePositionEntry(string mdPath, string name)
        {
            try
            {
                var posPath = Path.ChangeExtension(mdPath, ".positions.json");
                if (!File.Exists(posPath)) return;

                var json = File.ReadAllText(posPath);
                var list = JsonSerializer.Deserialize<List<GuiElementPosition>>(json);
                if (list == null) return;

                var newList = list.Where(p => !string.Equals(p.Name?.Trim(), name, StringComparison.Ordinal)).ToList();

                if (newList.Count == 0)
                {
                    try { File.Delete(posPath); } catch { }
                }
                else
                {
                    var options = new JsonSerializerOptions { WriteIndented = true };
                    File.WriteAllText(posPath, JsonSerializer.Serialize(newList, options));
                }
            }
            catch
            {
                // 無視（ログ追加するならここ）
            }
        }

        private string RemoveBranchFromMarkdown(string markdown, GuiElement parentEvent, int branchIndex)
        {
            if (markdown == null) markdown = string.Empty;
            if (parentEvent == null || parentEvent.Branches == null || branchIndex < 0 || branchIndex >= parentEvent.Branches.Count) return markdown;

            var branch = parentEvent.Branches[branchIndex];
            var lines = markdown.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None).ToList();

            // 親イベントブロックの開始行を探す（例: "- {button}押下" を含む行）
            int start = lines.FindIndex(l => l.TrimStart().StartsWith("- ") && l.Contains((parentEvent.Name ?? "").Trim()) && l.Contains("押下"));
            if (start == -1) return markdown;

            // ブロックの終端を探す
            int j = start + 1;
            while (j < lines.Count)
            {
                if (string.IsNullOrWhiteSpace(lines[j])) { j++; continue; }
                var t = lines[j].TrimStart();
                // stop if next top-level item or heading
                if (t.StartsWith("- ") && !lines[j].StartsWith("  ")) break;
                if (t.StartsWith("#")) break;
                j++;
            }

            // ブロック内で該当する分岐行を探して削除（条件またはターゲットを含む行）
            int removeIndex = -1;
            for (int k = start + 1; k < j; k++)
            {
                var trimmed = lines[k].Trim();
                if (string.IsNullOrWhiteSpace(trimmed)) continue;
                // 分岐行っぽいインデントを持つ行を対象にする
                if (lines[k].StartsWith("  -") || lines[k].StartsWith("-") || lines[k].StartsWith("　-"))
                {
                    // マッチ条件：Condition または Target が含まれている
                    var cond = (branch.Condition ?? "").Trim();
                    var targ = (branch.Target ?? "").Trim();
                    if ((!string.IsNullOrEmpty(cond) && trimmed.IndexOf(cond, StringComparison.OrdinalIgnoreCase) >= 0) ||
                        (!string.IsNullOrEmpty(targ) && trimmed.IndexOf(targ, StringComparison.OrdinalIgnoreCase) >= 0))
                    {
                        removeIndex = k;
                        break;
                    }
                }
            }

            // 見つからなければブロック内で "-> Target" によるマッチも試す
            if (removeIndex == -1 && !string.IsNullOrWhiteSpace(branch.Target))
            {
                for (int k = start + 1; k < j; k++)
                {
                    var trimmed = lines[k].Trim();
                    if (trimmed.IndexOf(branch.Target.Trim(), StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        removeIndex = k;
                        break;
                    }
                }
            }

            if (removeIndex == -1) return markdown;

            lines.RemoveAt(removeIndex);

            // ブロック内に分岐行が残っていない場合は親イベントヘッダも削除
            bool anyBranchLeft = false;
            for (int k = start + 1; k < j; k++)
            {
                if (k >= lines.Count) break;
                var t = lines[k].TrimStart();
                if (string.IsNullOrWhiteSpace(t)) continue;
                if (t.StartsWith("- ") && !lines[k].StartsWith("  ")) { anyBranchLeft = true; break; }
            }

            if (!anyBranchLeft)
            {
                // 親行を削除（ブロック全体を除去）
                // 再計算：親イベント行の現在インデックスを検索（remove により位置がずれている可能性があるため名前で再検索）
                int parentLine = lines.FindIndex(l => l.TrimStart().StartsWith("- ") && l.Contains((parentEvent.Name ?? "").Trim()) && l.Contains("押下"));
                if (parentLine >= 0)
                {
                    lines.RemoveAt(parentLine);

                    // その後続く空行もクリーニング
                    while (parentLine < lines.Count && string.IsNullOrWhiteSpace(lines[parentLine]))
                        lines.RemoveAt(parentLine);
                }
            }

            return string.Join(Environment.NewLine, lines);
        }

        partial void OnSelectedGuiElementChanged(GuiElement value)
        {
            // GuiElements 内の IsSelected を一意に保つ例（既存コードと整合）
            if (GuiElements == null) return;
            foreach (var g in GuiElements)
                g.IsSelected = ReferenceEquals(g, value);

            // 要素が切り替わったら分岐選択は解除する（新たに分岐タップでセットされる想定）
            SelectedBranchIndex = null;
        }

        // 追加：選択された分岐インデックス（null のときは分岐未選択）
        [ObservableProperty] private int? selectedBranchIndex;

        /// <summary>
        /// 指定された画面名に対応する Markdown (.md) ファイルを検索して開く。
        /// - SelectedFolderPath 配下を再帰検索する。
        /// - まずファイル名（拡張子除く）一致を探し、見つからなければファイル内の見出しで検索する。
        /// - 見つかったら LoadMarkdownAndVdm を呼んで開く。
        /// </summary>
        public async Task OpenFileForScreen(string screenName)
        {
            if (string.IsNullOrWhiteSpace(screenName)) return;
            if (string.IsNullOrWhiteSpace(SelectedFolderPath)) 
            {
                await Application.Current.MainPage.DisplayAlert("エラー", "フォルダが選択されていません。", "OK");
                return;
            }

            try
            {
                // 1) ファイル名（拡張子なし）で一致するものを探す
                var mdFiles = Directory.GetFiles(SelectedFolderPath, "*.md", SearchOption.AllDirectories);
                var byName = mdFiles.FirstOrDefault(f => string.Equals(Path.GetFileNameWithoutExtension(f), screenName, StringComparison.OrdinalIgnoreCase));
                string found = byName;

                // 2) 見つからなければ、ファイル内の先頭見出しを探す（"# " または "## "）
                if (found == null)
                {
                    foreach (var f in mdFiles)
                    {
                        // 最初の数行だけ読む（大きいファイル対策）
                        var lines = File.ReadLines(f).Take(30);
                        foreach (var line in lines)
                        {
                            var trimmed = line.Trim();
                            if ((trimmed.StartsWith("# ") || trimmed.StartsWith("## ")) && trimmed.IndexOf(screenName, StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                found = f;
                                break;
                            }
                        }
                        if (found != null) break;
                    }
                }

                if (found == null)
                {
                    await Application.Current.MainPage.DisplayAlert("見つかりません", $"\"{screenName}\" に対応するファイルが見つかりませんでした。", "OK");
                    return;
                }

                // SelectedItem を更新してファイルをロードする（FolderItems を経由しなくても LoadMarkdownAndVdm を呼べばよい）
                var existing = FolderItems.FirstOrDefault(f => string.Equals(f.FullPath, found, StringComparison.OrdinalIgnoreCase));
                if (existing != null)
                {
                    SelectedItem = existing;
                }
                else
                {
                    SelectedItem = new FolderItem
                    {
                        Name = Path.GetFileName(found),
                        FullPath = found,
                        Level = 0
                    };
                }

                // LoadMarkdownAndVdm は同期的にファイルを読み込み UI を更新するため直接呼ぶ
                LoadMarkdownAndVdm(found);
            }
            catch (Exception ex)
            {
                await Application.Current.MainPage.DisplayAlert("エラー", $"ファイル検索中にエラーが発生しました: {ex.Message}", "OK");
            }
        }

        // ヘルパー: FolderItems 内の実インスタンスに SelectedItem を一致させる（見つからなければ代替作成）
        private void ResolveSelectedItemByPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return;

            // FolderItems が最新でない可能性があるため、LoadFolderItems を呼ぶ場所からは呼び出し順に注意してください。
            var existing = FolderItems.FirstOrDefault(f => string.Equals(f.FullPath, path, StringComparison.OrdinalIgnoreCase));
            if (existing != null)
            {
                SelectedItem = existing;
            }
            else
            {
                SelectedItem = new FolderItem
                {
                    Name = Path.GetFileName(path),
                    FullPath = path,
                    Level = 0
                };
            }
        }

        // 追加: Renderer に渡す画面名集合（正規化済み）
        public IEnumerable<string> ScreenNamesForRenderer { get; private set; } = Enumerable.Empty<string>();

        // 既存の LoadMarkdownAndVdm メソッド末尾で呼ぶことを想定したヘルパー
        public IEnumerable<string> GetScreenManagementScreenNames()
        {
            try
            {
                var path = FindScreenListFilePath();
                if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                    return Enumerable.Empty<string>();

                var lines = File.ReadAllLines(path);
                // 見出し "# 画面一覧" を探す
                int idx = Array.FindIndex(lines, l => !string.IsNullOrWhiteSpace(l) && l.TrimStart().StartsWith("# 画面一覧", StringComparison.OrdinalIgnoreCase));
                if (idx < 0) return Enumerable.Empty<string>();

                var results = new List<string>();
                for (int i = idx + 1; i < lines.Length; i++)
                {
                    var t = lines[i].Trim();
                    if (string.IsNullOrEmpty(t)) continue; // 空行はスキップ
                    if (t.StartsWith("#")) break; // 次の見出しで終了
                    if (t.StartsWith("- "))
                    {
                        var name = t.Substring(2).Trim();
                        // 末尾の 'へ' を削る（もしあれば）、余分な矢印も除去
                        if (name.EndsWith("へ")) name = name.Substring(0, name.Length - 1).Trim();
                        // normalize: トリムだけ。Renderer 側でもさらに正規化するがここでも正規化を行う
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
    }

    /// <summary>
    /// GuiElementPosition 用 DTO
    /// .positions.json にシリアライズされる単純な構造体（クラス）
    /// </summary>
    public class GuiElementPosition
        {
            public string Name { get; set; }
            public float X { get; set; }
            public float Y { get; set; }
        }

        // OnMarkdownContentChanged の最後に LoadGuiPositionsToElements を呼ぶようにしてください
       
    }