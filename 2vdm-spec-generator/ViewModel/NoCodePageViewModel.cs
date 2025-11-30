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

namespace _2vdm_spec_generator.ViewModel
{
    public partial class NoCodePageViewModel : ObservableObject
    {
        // ========== バインド可能プロパティ (ObservableProperty により自動でプロパティが生成される) ===========
        // CommunityToolkit.MVVM の [ObservableProperty] を使うと backing field から自動的に
        // public プロパティ + PropertyChanged の発行が生成される。

        /// <summary>
        /// 選択されたフォルダのフルパス。フォルダ選択後にセットされ、ツリーの読み込みに利用される。
        /// </summary>
        [ObservableProperty] private string selectedFolderPath;

        /// <summary>
        /// 現在選択中の FolderItem（ファイルまたはフォルダ）。ファイルが選ばれたら Markdown の読み込み処理が走る。
        /// </summary>
        [ObservableProperty] private FolderItem selectedItem;

        /// <summary>
        /// 編集中の Markdown 本文。UI のテキストエディタと TwoWay バインドされる想定。
        /// OnMarkdownContentChanged が発火したときに GuiElements を更新する。
        /// </summary>
        [ObservableProperty] private string markdownContent;

        /// <summary>
        /// Markdown から変換した VDM++ の文字列。保存時や変換コマンドで更新される。
        /// </summary>
        [ObservableProperty] private string vdmContent;

        /// <summary>
        /// 「クラス追加」ボタンを表示するかどうか。Markdown の先頭行によって切り替わる。
        /// </summary>
        [ObservableProperty] private bool isClassAddButtonVisible;

        /// <summary>
        /// 「画面一覧追加」ボタンを表示するかどうか（画面一覧ファイルの場合のみ true）。
        /// </summary>
        [ObservableProperty] private bool isScreenListAddButtonVisible;

        /// <summary>
        /// クラス関連のすべてのボタン（ボタン追加・イベント追加・タイムアウト追加）を表示するかどうか。
        /// </summary>
        [ObservableProperty] private bool isClassAllButtonVisible;

        /// <summary>
        /// フォルダが選択されたか（UI の切り替え用フラグ。true = フォルダ選択画面を表示している等）。
        /// デフォルトは true（フォルダ選択待ち）。
        /// </summary>
        [ObservableProperty] private bool isFolderSelected = true;

        /// <summary>
        /// Markdown から変換された GUI 要素一覧。UI 側のドラッグ／選択などの操作対象となる。
        /// ObservableCollection にしておくことで要素追加/削除が UI に反映される。
        /// </summary>
        [ObservableProperty]
        private ObservableCollection<GuiElement> guiElements = new();

        [ObservableProperty]
　　　　private GuiElement selectedGuiElement;

        /// <summary>
        /// フォルダ・ファイルのツリーを表すコレクション。
        /// FolderItem は Name, FullPath, Level, IsExpanded, IsVisible, IsFolder, IsFile 等を持つ想定。
        /// Level によってツリーのインデントを表現する。
        /// </summary>
        public ObservableCollection<FolderItem> FolderItems { get; } = new();

        /// <summary>
        /// 新規作成時のデフォルトファイル名（将来利用のために定義）。
        /// 現在の実装では直接文字列を使っている箇所もあるため将来的統一が可能。
        /// </summary>
        private readonly string mdFileName = "NewClass.md";

        // ===== フォルダ選択 =====
        // Windows の FolderPicker を使ってフォルダを選択する。選択後はフォルダの中身を読み込む。
        [RelayCommand]
        private async Task SelectFolderAsync()
        {
#if WINDOWS
            // Windows 固有: MAUI の Window ハンドラを用いて WinRT の FolderPicker を初期化する。
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
            // フォルダが選択された状態なのでフラグを切り替える（UI の表示を変更する想定）
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
            // Directory.GetDirectories は IO 例外を投げる可能性があるが、ここでは最小限の保護で実装している。
            foreach (var dir in Directory.GetDirectories(SelectedFolderPath))
                AddFolderRecursive(dir, 0);

            // ルート直下の .md ファイルも追加する（拡張子チェックは厳密に小文字化している）
            foreach (var file in Directory.GetFiles(SelectedFolderPath).Where(f => Path.GetExtension(f) == ".md"))
                FolderItems.Add(new FolderItem { Name = Path.GetFileName(file), FullPath = file, Level = 0 });
        }

        /// <summary>
        /// 指定フォルダを FolderItems に追加し、そのフォルダ内の .md ファイルを追加してからサブフォルダを再帰的に追加する。
        /// Level はツリーの深さを表す（UI 側のインデントに利用）。
        /// ファイル一覧を先に表示するため、サブフォルダは最後に追加する設計。
        /// </summary>
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

            // 展開状態を反転
            folder.IsExpanded = !folder.IsExpanded;

            // 子アイテムの表示/非表示を切り替える
            // 判定: FullPath が親の FullPath で始まり、かつ Level が親より深い要素を子とみなす
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


        /// <summary>
        /// 指定パスの Markdown を読み込み、MarkdownContent と VdmContent を更新する。
        /// また、先頭行の見出しで UI 上のボタン表示（クラス追加／画面一覧追加など）を切り替える。
        /// 最後に Markdown から GUI 要素を生成して位置情報を反映する。
        /// </summary>
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

            // Markdown から GUI 要素を生成（表示用）
            var uiConverter = new MarkdownToUiConverter();
            GuiElements = new ObservableCollection<GuiElement>(uiConverter.Convert(MarkdownContent));

            // JSON から位置を反映（.positions.json があれば GUI 要素に位置を適用する）
            LoadGuiPositionsToElements();
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

            // Markdown を上書き保存
            File.WriteAllText(SelectedItem.FullPath, MarkdownContent);

            // VDM++ に変換して同名で拡張子を .vdmpp にして保存
            var converter = new MarkdownToVdmConverter();
            VdmContent = converter.ConvertToVdm(MarkdownContent);
            File.WriteAllText(Path.ChangeExtension(SelectedItem.FullPath, ".vdmpp"), VdmContent);
        }

        // ===== VDM++ 変換（保存なし）=====
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

            // 再読み込みして内部状態（GuiElements, ボタン表示等）を同期
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

            // プロパティを更新して UI に反映させる
            MarkdownContent = newMarkdown;
            VdmContent = vdmContent;

            // 再読込して GuiElements を更新（AddButton でマークダウンが変わったので反映）
            LoadMarkdownAndVdm(path);
        }

        [RelayCommand]
        private async Task AddEventAsync()
        {
            if (SelectedItem == null || !SelectedItem.IsFile) return;

            // --- ボタン候補の取得 ---
            // GuiElements からボタン名だけを抽出し、重複を排除して ActionSheet に渡す
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

            // 重複チェック（マークダウン内の既存イベントを直接検索して判定）
            // 非条件イベント: "- {button}押下 → {target}" の完全一致を避ける
            if (!isConditional)
            {
                string eventName = await Shell.Current.DisplayPromptAsync(
                    "イベント", "イベントの遷移先や名前を入力してください", "OK", "キャンセル", placeholder: "TargetScreen"
                );
                if (string.IsNullOrWhiteSpace(eventName)) return;

                // 比較のためにトリミングして候補行を作る
                string candidateLine = $"- {selectedButton}押下 → {eventName.Trim()}";
                var lines = currentMarkdown.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None).Select(l => l.Trim()).ToArray();

                if (lines.Any(l => string.Equals(l, candidateLine, StringComparison.OrdinalIgnoreCase)))
                {
                    await Shell.Current.DisplayAlert("重複", $"同じイベント \"{candidateLine}\" は既に存在します。", "OK");
                    return;
                }

                newMarkdown = builder.AddEvent(currentMarkdown, selectedButton.Trim(), eventName.Trim());
            }
            else
            {
                // 条件分岐イベント：既にそのボタンのイベントブロックが存在しないか確認
                var lines = currentMarkdown.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
                bool blockExists = lines.Any(l => l.TrimStart().StartsWith($"- {selectedButton}押下", StringComparison.OrdinalIgnoreCase));

                if (blockExists)
                {
                    // 既存ブロックがある場合は重複追加を禁止（もしくは確認してブランチ追加する案がある）
                    bool addAnyway = await Shell.Current.DisplayAlert("既存のイベントがあります", $"ボタン \"{selectedButton}\" には既にイベント定義があります。新しい条件分岐を追加しますか？", "追加する", "キャンセル");
                    if (!addAnyway) return;

                    // ユーザーが "追加する" を選んだ場合は既存ブロックに追加する実装は UiToMarkdownConverter にないため、
                    // 簡易的に既存マークダウンの末尾に条件分岐ブロックを追加する（既存ブロックとの整合性は簡易処理）
                }

                // 条件分岐ブランチの入力ループ
                var branches = new List<(string Condition, string Target)>();
                bool addMore = true;

                while (addMore)
                {
                    // カスタム Popup を呼び出して条件とターゲットを取得する
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


        /// <summary>
        /// 選択中の Markdown ファイルに対応する .positions.json がなければ作成する。
        /// - GuiElements の現在位置情報を収集し、要素が一つ以上あれば JSON としてファイル出力する。
        /// - 例外は内部で吸収して UI を壊さない (最小限のフォールトトレランス)。
        /// </summary>
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

        /// <summary>
        /// MarkdownContent が変更されたタイミングで呼ばれる自動生成された partial メソッド。
        /// - Markdown から GUI 要素を再生成し、タイムアウト要素は固定ノードとしてフラグを付与する
        /// - 保存済みの位置情報があれば読み込んで反映し、必要なら位置 JSON を作成する
        /// </summary>
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
        }

        /// <summary>
        /// 保存 (SelectedItem がセットされている前提)
        /// - elements の位置情報を .positions.json にシリアライズして保存する
        /// - UI に影響を与える可能性があるため失敗は黙殺（将来はログ出力推奨）
        /// </summary>
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

        /// <summary>
        /// 読み込み（ファイルから位置を復元して GuiElements に適用）
        /// - .positions.json が存在すれば読み込み、要素名でマッチするものに位置を反映する
        /// - JSON の形式が破損している場合や要素が見つからない場合は安全に無視する
        /// </summary>
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
        /// <summary>
        /// 選択中の GUI 要素を削除するコマンド。
        /// - 確認ダイアログを表示してから、Markdown 内の該当行／イベントブロックを削除し .vdmpp を再出力、
        ///   .positions.json から該当エントリを削除する。
        /// </summary>
        [RelayCommand]
        private async Task DeleteSelectedGuiElementAsync()
        {
            var el = SelectedGuiElement;
            if (el == null) return;
            if (SelectedItem == null || string.IsNullOrWhiteSpace(SelectedItem.FullPath)) return;

            bool ok = await Shell.Current.DisplayAlert("削除確認", $"\"{el.Name}\" をこのファイルから削除しますか？", "はい", "いいえ");
            if (!ok) return;

            var mdPath = SelectedItem.FullPath;
            try
            {
                // Markdown 編集
                string currentMarkdown = File.Exists(mdPath) ? File.ReadAllText(mdPath) : string.Empty;
                string newMarkdown = RemoveElementFromMarkdown(currentMarkdown, el);

                File.WriteAllText(mdPath, newMarkdown);

                // VDM++ 再生成
                var converter = new MarkdownToVdmConverter();
                var newVdm = converter.ConvertToVdm(newMarkdown);
                File.WriteAllText(Path.ChangeExtension(mdPath, ".vdmpp"), newVdm);

                // positions.json から削除（存在する場合）
                RemovePositionEntry(mdPath, el.Name);

                // ViewModel のプロパティを更新して UI を再構築
                MarkdownContent = newMarkdown;
                VdmContent = newVdm;

                // 再解析して GuiElements を更新
                LoadMarkdownAndVdm(mdPath);

                // 選択解除
                SelectedGuiElement = null;
            }
            catch
            {
                await Application.Current.MainPage.DisplayAlert("エラー", "削除処理に失敗しました。", "OK");
            }
        }

        // Markdown の行列を操作して指定要素に関連する行／ブロックを削除する簡易実装
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

        // positions.json から該当 Name を削除する。残りが無ければファイル削除。
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
                    try { File.Delete(posPath); } catch { /* ignore */ }
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

        partial void OnSelectedGuiElementChanged(GuiElement value)
        {
            // GuiElements 内の IsSelected を一意に保つ例（既存コードと整合）
            if (GuiElements == null) return;
            foreach (var g in GuiElements)
                g.IsSelected = ReferenceEquals(g, value);
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









