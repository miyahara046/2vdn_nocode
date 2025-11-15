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

namespace _2vdm_spec_generator.ViewModel
{
    // NoCodePageのViewModel
    public partial class NoCodePageViewModel : ObservableObject
    {
        // ========== バインド可能プロパティ (ObservableProperty により自動でプロパティが生成される) ===========
        // これらは View 側から TwoWay バインドされる想定である。

        // 選択されたフォルダのフルパス
        [ObservableProperty] private string selectedFolderPath;

        // 選択中の FolderItem （ファイルまたはフォルダ）
        [ObservableProperty] private FolderItem selectedItem;

        // 編集中の Markdown 本文
        [ObservableProperty] private string markdownContent;

        // Markdown から変換した VDM++ の文字列
        [ObservableProperty] private string vdmContent;

        // クラス追加ボタンを表示するかどうか
        [ObservableProperty] private bool isClassAddButtonVisible;

        // 画面一覧追加ボタンを表示するかどうか
        [ObservableProperty] private bool isScreenListAddButtonVisible;

        //クラスに関するボタンを表示するかどうか（ボタン追加・イベント追加・タイムアウト追加）
        [ObservableProperty] private bool isClassAllButtonVisible;

        // フォルダが選択されたか (UI の切り替え用フラグ)
        [ObservableProperty] private bool isFolderSelected = true;

        [ObservableProperty]
        private ObservableCollection<GuiElement> guiElements = new();

        // フォルダ・ファイルのツリーを表すコレクション
        // FolderItem 型は Name, FullPath, Level, IsExpanded, IsVisible, IsFolder, IsFile 等のプロパティを持つ想定である。
        public ObservableCollection<FolderItem> FolderItems { get; } = new();

        // 新規作成時のデフォルトファイル名（将来利用のために定義してあるが、現状は直接文字列を使っている箇所もある）
        private readonly string mdFileName = "NewClass.md";

        // ===== フォルダ選択 =====
        // Windows の FolderPicker を使ってフォルダを選択する。選択後はフォルダの中身を読み込む。
        [RelayCommand]
        private async Task SelectFolderAsync()
        {
#if WINDOWS
            var hwnd = ((MauiWinUIWindow)App.Current.Windows[0].Handler.PlatformView).WindowHandle;
            var picker = new Windows.Storage.Pickers.FolderPicker();
            // FolderPicker はファイル拡張子フィルタが必須なのでワイルドカードを追加
            picker.FileTypeFilter.Add("*");
            // WinRT の初期化（Window ハンドルを渡す）
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

            var folder = await picker.PickSingleFolderAsync();
            if (folder != null)
            {
                // 選択したフォルダパスを設定してフォルダ項目をロード
                SelectedFolderPath = folder.Path;
                LoadFolderItems();
            }
            // フォルダが選択された状態なのでフラグを切り替える
            IsFolderSelected = false;
#else
            // Windows 以外は未サポートであることを通知する
            await Application.Current.MainPage.DisplayAlert("未対応", "このプラットフォームではフォルダ選択は未対応です。", "OK");
#endif
        }

        // ===== フォルダ読み込み =====
        // SelectedFolderPath の直下にあるフォルダ/Markdown ファイルを FolderItems に読み込む。
        // 再帰的にサブフォルダも読み込み、FolderItem の Level によってツリー表示のインデントを決める。
        private void LoadFolderItems()
        {
            FolderItems.Clear();
            if (string.IsNullOrWhiteSpace(SelectedFolderPath)) return;

            // まずトップレベルのディレクトリを列挙して再帰的に追加する。
            foreach (var dir in Directory.GetDirectories(SelectedFolderPath))
                AddFolderRecursive(dir, 0);

            // ルート直下の .md ファイルも追加する（拡張子チェックは厳密に小文字化している）
            foreach (var file in Directory.GetFiles(SelectedFolderPath).Where(f => Path.GetExtension(f) == ".md"))
                FolderItems.Add(new FolderItem { Name = Path.GetFileName(file), FullPath = file, Level = 0 });
        }

        // 指定フォルダを FolderItems に追加し、そのフォルダ内の .md ファイルを追加してからサブフォルダを再帰的に追加する。
        private void AddFolderRecursive(string path, int level)
        {
            // 親フォルダ自身を FolderItems に追加する
            var folderItem = new FolderItem
            {
                Name = Path.GetFileName(path),
                FullPath = path,
                Level = level,
                // デフォルトで展開状態にしておく。
                IsExpanded = true
            };
            FolderItems.Add(folderItem);

            // 同階層の Markdown ファイル (.md) をフォルダの直下として追加
            foreach (var file in Directory.GetFiles(path).Where(f => Path.GetExtension(f).ToLower() == ".md"))
            {
                FolderItems.Add(new FolderItem
                {
                    Name = Path.GetFileName(file),
                    FullPath = file,
                    Level = level + 1
                });
            }

            // サブフォルダは最後に再帰追加する（ファイル一覧を先に見せたい設計）
            foreach (var dir in Directory.GetDirectories(path))
                AddFolderRecursive(dir, level + 1);
        }


        // ===== 折りたたみ =====
        // フォルダの展開・折りたたみを切り替える。

        [RelayCommand]
        private void ToggleExpand(FolderItem folder)
        {
            if (folder == null || !folder.IsFolder) return;

            folder.IsExpanded = !folder.IsExpanded;

            // 子アイテムの表示/非表示を切り替える
            foreach (var item in FolderItems)
            {
                if (item.FullPath.StartsWith(folder.FullPath) && item.Level > folder.Level)
                    item.IsVisible = folder.IsExpanded;
            }
        }

        // ===== ファイル選択 =====
        // ファイル（FolderItem.IsFile が true）を選択したときの処理。ファイルでなければ何もしない。
        // 選択したファイルの Markdown と VDM をロードする。

        [RelayCommand]
        private void SelectItem(FolderItem item)
        {
            if (item == null || !item.IsFile) return;

            SelectedItem = item;
            LoadMarkdownAndVdm(item.FullPath);
        }


        // 指定パスの Markdown を読み込み、MarkdownContent と VdmContent を更新する。
        // 同時にファイル先頭の行を見て UI 上のボタン表示（クラス追加／画面一覧追加）を切り替える。
        private void LoadMarkdownAndVdm(string path)
        {
            // ファイルが存在すれば全文を読み込む。存在しなければ空文字列をセットする。
            MarkdownContent = File.Exists(path) ? File.ReadAllText(path) : "";

            // Markdown -> VDM++ の変換処理を呼び出す（変換ロジックは MarkdownToVdmConverter に委譲）
            var converter = new MarkdownToVdmConverter();
            VdmContent = converter.ConvertToVdm(MarkdownContent);

            // ファイル先頭行を取得して、ファイル種別判定に使う。
            //  - 先頭が "## " なら既にクラス見出しがあると判断してボタン非表示
            //  - 先頭が "# 画面一覧" なら画面一覧編集用ボタンを表示
            //  - それ以外はクラス追加用ボタンを表示
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
        }

        // ===== 新規 Markdown 作成 =====
        // 選択中のフォルダまたはファイルの直下に新しい Markdown ファイルを作成する。
        // ファイル名はユーザー入力を受け付け、同名ファイルがあれば作成を中止する。
        [RelayCommand]
        private async Task CreateNewMdFileAsync()
        {
            if (SelectedItem == null)
            {
                await Shell.Current.DisplayAlert("エラー", "ファイルまたはフォルダが選択されていません", "OK");
                return;
            }

            // 追加先ディレクトリの決定。選択がファイルならその親ディレクトリ、フォルダならそのフォルダ自身。
            string targetDir = SelectedItem.IsFile
                ? Path.GetDirectoryName(SelectedItem.FullPath)  // ファイルなら親ディレクトリ
                : SelectedItem.FullPath;                        // フォルダならそのフォルダ直下

            // ユーザーにファイル名を入力してもらう（拡張子は不要にしている）
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
                // 同名ファイルがあればエラー表示して終了
                await Shell.Current.DisplayAlert("エラー", "同名ファイルが存在します", "OK");
                return;
            }

            // 簡易テンプレートを書き出す。将来的にはテンプレート選択などの拡張が可能である。
            File.WriteAllText(newPath, "New Class\n");

            // フォルダツリーをリロードして、作成したファイルを選択状態にして読み込む
            LoadFolderItems();

            SelectedItem = FolderItems.FirstOrDefault(f => f.FullPath == newPath);
            LoadMarkdownAndVdm(newPath);

            // 新規作成直後はクラス追加ボタンを表示しておく
            IsClassAddButtonVisible = true;
        }


        // ===== Markdown 保存 =====
        // 編集中の MarkdownContent をファイルに上書き保存し、同時に VDM++ に変換して .vdmpp ファイルを作る。
        [RelayCommand]
        private void SaveMarkdown()
        {
            if (SelectedItem == null || !SelectedItem.IsFile) return;

            File.WriteAllText(SelectedItem.FullPath, MarkdownContent);

            var converter = new MarkdownToVdmConverter();
            VdmContent = converter.ConvertToVdm(MarkdownContent);
            File.WriteAllText(Path.ChangeExtension(SelectedItem.FullPath, ".vdmpp"), VdmContent);
        }

        // ===== VDM++ 変換（保存なし） =====
        // 現在の選択ファイルを読み込み、VDM++ に変換してファイル（.vdmpp）に保存する。
        // ConvertToVdm は UI から変換を即時に実行したいときに使われる。
        [RelayCommand]
        private void ConvertToVdm()
        {
            if (SelectedItem == null || !SelectedItem.IsFile) return;

            string mdPath = SelectedItem.FullPath;
            var converter = new MarkdownToVdmConverter();
            VdmContent = converter.ConvertToVdm(File.ReadAllText(mdPath));
            File.WriteAllText(Path.ChangeExtension(mdPath, ".vdmpp"), VdmContent);
        }

        // ===== クラス追加 / 画面追加 =====
        // Markdown に対して「画面一覧の追加」または「クラスの追加」を行う。
        // 画面一覧追加は固定の見出しを挿入し、クラスの追加はユーザーにクラス名を入力させて見出しを作る。
        // Markdown を編集したあと、VDM++ に変換して保存する。
        [RelayCommand]
        private async Task AddClassHeadingAsync()
        {
            if (SelectedItem == null || !SelectedItem.IsFile) return;

            // ユーザーに追加種類を選ばせる
            string classType = await Shell.Current.DisplayActionSheet(
                "追加するクラスの種類を選んでください",
                "キャンセル", null,
                "画面一覧の追加", "クラスの追加"
            );

            if (string.IsNullOrEmpty(classType) || classType == "キャンセル") return;

            string className = null;
            if (classType == "クラスの追加")
            {
                // クラス名をユーザー入力で取得する
                string inputName = await Shell.Current.DisplayPromptAsync(
                    "クラス追加", "クラス名を入力してください", "OK", "キャンセル", placeholder: "MyClass"
                );
                if (string.IsNullOrWhiteSpace(inputName)) return;
                className = $"# {inputName}"; // Markdown の見出し形式にする
            }

            string path = SelectedItem.FullPath;
            string currentMarkdown = File.ReadAllText(path);

            var builder = new UiToMarkdownConverter();
            // UiToMarkdownConverter に処理を委譲
            string newMarkdown = classType switch
            {
                "画面一覧の追加" => builder.AddClassHeading(currentMarkdown, " 画面一覧"),
                "クラスの追加" => builder.AddClassHeading(currentMarkdown, className),
                _ => currentMarkdown
            };

            // ファイルを書き換え、VDM++ に変換して保存
            File.WriteAllText(path, newMarkdown);

            var converter = new MarkdownToVdmConverter();
            string vdmContent = converter.ConvertToVdm(newMarkdown);
            File.WriteAllText(Path.ChangeExtension(path, ".vdmpp"), vdmContent);

            // ViewModel のバインディングプロパティを更新
            MarkdownContent = newMarkdown;
            VdmContent = vdmContent;

            LoadMarkdownAndVdm(path);
        }

        // 画面を追加する専用メソッド。画面名を入力して、UiToMarkdownConverter.AddScreenList を呼ぶ。
        // 呼び出し後は Markdown と VDM++ を更新する。
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

            // プロパティを更新して UI に反映させる
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

            string path = SelectedItem.FullPath;
            string currentMarkdown = File.ReadAllText(path);

            var builder = new UiToMarkdownConverter();
            string newMarkdown = builder.AddButton(currentMarkdown, buttonName.Trim());
            File.WriteAllText(path, newMarkdown);

            var converter = new MarkdownToVdmConverter();
            string vdmContent = converter.ConvertToVdm(newMarkdown);
            File.WriteAllText(Path.ChangeExtension(path, ".vdmpp"), vdmContent);

            // プロパティを更新して UI に反映させる
            MarkdownContent = newMarkdown;
            VdmContent = vdmContent;
        }

        [RelayCommand]
        private async Task AddEventAsync()
        {
            if (SelectedItem == null || !SelectedItem.IsFile) return;

            // --- ボタン候補の取得 ---
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

            // --- ボタン選択 ---
            string selectedButton = await Shell.Current.DisplayActionSheet(
                "イベントを追加するボタンを選んでください",
                "キャンセル", null,
                buttonNames
            );

            if (string.IsNullOrEmpty(selectedButton) || selectedButton == "キャンセル") return;

            // --- 条件分岐の有無を確認 ---
            bool isConditional = await Shell.Current.DisplayAlert(
                "条件分岐イベント",
                "このイベントに条件分岐を追加しますか？",
                "はい", "いいえ"
            );

            string path = SelectedItem.FullPath;
            string currentMarkdown = File.ReadAllText(path);
            var builder = new UiToMarkdownConverter();
            string newMarkdown;

            if (isConditional)
            {
                var branches = new List<(string Condition, string Target)>();
                bool addMore = true;

                while (addMore)
                {
                    // カスタムPopupを呼び出す
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
            else
            {
                // --- 通常のイベント追加 ---
                string eventName = await Shell.Current.DisplayPromptAsync(
                    "イベント", "イベントの遷移先や名前を入力してください", "OK", "キャンセル", placeholder: "TargetScreen"
                );
                if (string.IsNullOrWhiteSpace(eventName)) return;

                newMarkdown = builder.AddEvent(currentMarkdown, selectedButton.Trim(), eventName.Trim());
            }

            // --- Markdown と VDM++ 出力更新 ---
            File.WriteAllText(path, newMarkdown);
            var converter = new MarkdownToVdmConverter();
            string vdmContent = converter.ConvertToVdm(newMarkdown);
            File.WriteAllText(Path.ChangeExtension(path, ".vdmpp"), vdmContent);

            MarkdownContent = newMarkdown;
            VdmContent = vdmContent;
        }

        [RelayCommand]
        private async Task AddTimeoutEventAsync()
        {
            if (SelectedItem == null || !SelectedItem.IsFile) return;

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



        // ===== スタートページに戻る =====

        // Shell ナビゲーションを利用してスタートページへ遷移する。
        [RelayCommand]
        private async Task GoToStartPageAsync()
        {
            await Shell.Current.GoToAsync("//StartPage");
        }

        partial void OnMarkdownContentChanged(string value)
        {
            // Markdownが変更されたらGUI要素を更新
            var converter = new MarkdownToUiConverter();
            GuiElements = new ObservableCollection<GuiElement>(converter.Convert(value));
        }



    }


}
