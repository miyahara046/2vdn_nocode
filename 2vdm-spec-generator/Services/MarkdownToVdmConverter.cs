using Markdig;
using Markdig.Syntax;
using System.Text;
using System.Collections.Generic;

namespace _2vdm_spec_generator.Services
{
    public class MarkdownToVdmConverter
    {
        public string ConvertToVdm(string markdown)
        {
            // Markdigパイプラインの設定
            var pipeline = new MarkdownPipelineBuilder()
                .UseAdvancedExtensions()
                .Build();

            // Markdownをパース
            var document = Markdown.Parse(markdown, pipeline);
            var vppr = new VdmPlusPlusRenderer();
            return vppr.Render(document);
        }
    }

    public class VdmPlusPlusRenderer
    {
        private readonly StringBuilder _classSb;
        private readonly StringBuilder _typesSb;
        private readonly StringBuilder _valueSb;
        private readonly StringBuilder _instanceVariablesSb;
        private readonly StringBuilder _operationsSb;
        private readonly StringBuilder _functionsSb;
        private List<Block> _blocks;
        private int _currentBlockIndex;

        // 初期行の長さをクラスレベルで定義
        private int initialTypesLength;
        private int initialValuesLength;
        private int initialInstanceVariablesLength;
        private int initialOperationsLength;
        private int initialFunctionsLength;

        // インデント倍率を定義（例えば、2倍のインデント）
        private readonly double indentMultiplier = 1.5;

        // コンストラクタ
        public VdmPlusPlusRenderer()
        {
            _classSb = new StringBuilder();
            _typesSb = new StringBuilder();
            _valueSb = new StringBuilder();
            _instanceVariablesSb = new StringBuilder();
            _operationsSb = new StringBuilder();
            _functionsSb = new StringBuilder();
            _blocks = new List<Block>();
            _currentBlockIndex = 0;

            // 初期値の追加
            _typesSb.AppendLine("types");
            _valueSb.AppendLine("values");
            _instanceVariablesSb.AppendLine("instance variables");
            _operationsSb.AppendLine("operations");
            _functionsSb.AppendLine("functions");

            // 初期長さを実際の StringBuilder の長さに設定
            initialTypesLength = _typesSb.Length;
            initialValuesLength = _valueSb.Length;
            initialInstanceVariablesLength = _instanceVariablesSb.Length;
            initialOperationsLength = _operationsSb.Length;
            initialFunctionsLength = _functionsSb.Length;
        }

        /// <summary>
        /// _classSbに_valueSb、_instanceVariablesSb、_operationsSb、_functionsSbを順に追加します。
        /// 初期化時に追加された文字列のみの場合はスキップします。
        /// </summary>
        public void BuildFinalVdm()
        {
            // 各StringBuilderの内容を_classSbに条件付きで追加
            if (_typesSb.Length > initialTypesLength)
            {
                _classSb.AppendLine(_typesSb.ToString());
            }

            if (_valueSb.Length > initialValuesLength)
            {
                _classSb.AppendLine(_valueSb.ToString());
            }

            if (_instanceVariablesSb.Length > initialInstanceVariablesLength)
            {
                _classSb.AppendLine(_instanceVariablesSb.ToString());
            }

            if (_operationsSb.Length > initialOperationsLength)
            {
                _classSb.AppendLine(_operationsSb.ToString());
            }

            if (_functionsSb.Length > initialFunctionsLength)
            {
                _classSb.AppendLine(_functionsSb.ToString());
            }

            return;
        }

        public string Render(MarkdownDocument document)
        {
            // すべてのブロックを配列に格納
            _blocks = document.ToList();

            // 最初のヘッダーがレベル1の場合は画面管理クラスとして処理
            var firstHeading = document.Descendants<HeadingBlock>().FirstOrDefault();
            if (firstHeading?.Level == 1)
            {
                return RenderScreenManagement(document);
            }

            // それ以外は既存の処理を実行
            var className = ExtractClassName(document);
            _classSb.AppendLine($"class 「{className}」");

            // タイムアウト値の処理を追加
            ProcessTimeoutValue(document);

            // 見出しレベルに基づいて要素を処理
            for (_currentBlockIndex = 0; _currentBlockIndex < _blocks.Count; _currentBlockIndex++)
            {
                if (_blocks[_currentBlockIndex] is HeadingBlock heading)
                {
                    switch (heading.Level)
                    {
                        case 1:
                            RenderHelloWorld(heading);
                            break;
                        case 2:
                            RenderClass(heading);
                            break;
                        case 3:
                            RenderBlocks(heading);
                            break;
                    }
                }
            }

            // 最終的なVDMを構築
            BuildFinalVdm();

            // クラスの終了を追加
            _classSb.AppendLine($"end 「{className}」");
            return _classSb.ToString();
        }

        private string RenderScreenManagement(MarkdownDocument document)
        {
            var sb = new StringBuilder();
            sb.AppendLine("class 「画面管理」");
            sb.AppendLine();

            // レベル1ヘッダーの直後のリストを探す
            var firstHeading = document.Descendants<HeadingBlock>().First();
            var nextBlock = _blocks[_blocks.IndexOf(firstHeading) + 1];

            if (nextBlock is ListBlock listBlock)
            {
                var states = GetListItems(listBlock).ToList();
                if (states.Any())
                {
                    sb.AppendLine("types");
                    sb.AppendLine($"  「画面状態」= {string.Join(" | ", states.Select(s => $"「{s}」"))};\n");
                    sb.AppendLine();
                    sb.AppendLine("instance variables");
                    sb.AppendLine($"  static 現在画面:「画面状態」:= new「{states.First()}」()");
                    sb.AppendLine();
                }
            }

            sb.AppendLine("end「画面管理」");
            return sb.ToString();
        }

        private Block GetNextBlock()
        {
            if (_currentBlockIndex + 1 < _blocks.Count)
            {
                return _blocks[_currentBlockIndex + 1];
            }
            return null;
        }

        private void RenderHelloWorld(HeadingBlock block)
        {
            _classSb.AppendLine("Hello World!!");
        }

        private void RenderClass(HeadingBlock block)
        {
            var nextBlock = GetNextBlock();
            if (nextBlock is ListBlock listBlock)
            {
                _currentBlockIndex++; // リストブロックを処理したのでインデックスを進める
            }
        }

        private void RenderBlocks(HeadingBlock block)
        {
            var headingText = block.Inline?.FirstChild?.ToString();
            var nextBlock = GetNextBlock();

            if (nextBlock is ListBlock listBlock)
            {
                switch (headingText)
                {
                    case "有効ボタン一覧":
                        RenderButtonStates(listBlock);
                        break;
                    case "イベント一覧":
                        RenderEventOperations(listBlock);
                        break;
                    default:
                        RenderDefaultOperations(listBlock);
                        break;
                }
                _currentBlockIndex++;
            }
        }

        private void RenderButtonStates(ListBlock listBlock)
        {
            var buttons = GetListItems(listBlock).ToList();
            if (buttons.Any())
            {
                _typesSb.AppendLine($"  「ボタン状態」= <非押下> | {string.Join(" | ", buttons.Select(b => $"<{b}>"))};");
                _instanceVariablesSb.AppendLine("  押下ボタン:「ボタン状態」:= <非押下>;");
            }
        }

        private void RenderEventOperations(ListBlock listBlock)
        {
            _operationsSb.AppendLine("  private");
            _operationsSb.AppendLine("    押下時操作:「ボタン状態」==> ()");
            _operationsSb.AppendLine("    押下時操作 (押下ボタン) ==");
            _operationsSb.AppendLine("      cases 押下ボタン:");

            var conditions = new HashSet<string>();
            // タイムアウト時の画面遷移先を格納する
            string screenNameForTimeOut = null;

            foreach (var listItem in listBlock.Descendants<ListItemBlock>()
                .Where(li => li.Parent == listBlock))
            {
                var paragraphBlock = listItem.Descendants<ParagraphBlock>().FirstOrDefault();
                var item = paragraphBlock?.Inline?.FirstChild?.ToString();

                if (item != null)
                {
                    var parts = item.Split("→").Select(p => p.Trim()).ToList();
                    if (parts.Count == 2)
                    {
                        if (parts[0].Contains("タイムアウト"))
                        {
                            screenNameForTimeOut = parts[1].Replace("へ", "").Trim();
                            continue;
                        }

                        var buttonName = parts[0].Replace("押下", "");
                        var subList = listItem.Descendants<ListBlock>().FirstOrDefault();

                        if (subList != null)
                        {
                            var subConditions = GetListItems(subList).ToList();
                            foreach (var condition in subConditions)
                            {
                                var conditionParts = condition.Split("→").Select(p => p.Trim()).ToList();
                                if (conditionParts.Count == 2)
                                {
                                    conditions.Add(conditionParts[0]); // 分岐条件を収集
                                }
                            }
                            GenerateConditionCode(buttonName, subConditions);
                        }
                        else
                        {
                            ProcessSingleAction(buttonName, parts[1]);
                        }
                    }
                }
            }


            _operationsSb.AppendLine("        others -> skip");
            _operationsSb.AppendLine("      end");
            _operationsSb.AppendLine("    pre 押下ボタン <> <非押下>");
            _operationsSb.AppendLine("    post 押下ボタン = <非押下>");

            // タイムアウトイベント時の関数を追加する
            if (screenNameForTimeOut != null)
            {
                _operationsSb.AppendLine("  private");
                _operationsSb.AppendLine("    タイムアウト時画面遷移: () ==> ()");
                _operationsSb.AppendLine("    タイムアウト時画面遷移 () ==");
                _operationsSb.AppendLine($"    「画面管理」'現在画面 := new「{screenNameForTimeOut}」();");
            }
            else
            {
                _operationsSb.AppendLine("  private");
                _operationsSb.AppendLine("    タイムアウト時遷移: () ==> ()");
                _operationsSb.AppendLine("    タイムアウト時遷移 () ==");
                _operationsSb.AppendLine("      「画面管理」'現在画面 := /* 仕様に記載なし */");
            }

            // 分岐条件の関数定義を追加
            if (conditions.Any())
            {
                foreach (var condition in conditions)
                {
                    _operationsSb.AppendLine("  private");
                    _operationsSb.AppendLine($"    {condition}: () ==> bool");
                    _operationsSb.AppendLine($"    {condition}() == is not yet specified;");
                }
            }

        }

        private void ProcessSingleAction(string buttonName, string action)
        {
            if (action.Contains("表示部に") && action.Contains("を追加"))
            {
                var number = action.Replace("表示部に", "").Replace("を追加", "");
                _operationsSb.AppendLine($"        <{buttonName}> -> 表示部.入力操作 ({number}),");
            }
            else if (action.Contains("表示部の文字削除"))
            {
                _operationsSb.AppendLine($"        <{buttonName}> -> 表示部.削除操作 (),");
            }
            else if (action.Contains("画面へ"))
            {
                var screenName = action.Replace("へ", "");
                _operationsSb.AppendLine($"        <{buttonName}> ->「画面管理」'現在画面 := new「{screenName}」(),");
            }
        }

        private void GenerateConditionCode(string buttonName, List<string> conditions)
        {
            var sb = new StringBuilder();
            bool isFirst = true;
            foreach (var condition in conditions)
            {
                var parts = condition.Split("→").Select(p => p.Trim()).ToList();
                if (parts.Count == 2)
                {
                    if (sb.Length > 0) sb.AppendLine();
                    var screenName = parts[1].Replace("へ", "");
                    // 'if'の前までの文字数を計算し、倍率を適用
                    int size = (int)($"        <{buttonName}> -> ".Length * indentMultiplier);
                    if (isFirst)
                    {
                        sb.Append($"        <{buttonName}> -> if {parts[0]} ()\n");
                        sb.Append($"{new string(' ', size)}then 「画面管理」'現在画面 := new「{screenName}」()");
                        isFirst = false;
                    }
                    else
                    {
                        sb.Append($"{new string(' ', size)}if {parts[0]} ()\n");
                        sb.Append($"{new string(' ', size)}then 「画面管理」'現在画面 := new「{screenName}」()");
                    }
                }
            }
            _operationsSb.AppendLine($"{sb.ToString()},");
        }

        private void RenderDefaultOperations(ListBlock listBlock)
        {
            // 既存の処理をここに移動
            _operationsSb.AppendLine("operations");
            _operationsSb.AppendLine("  private");
            _operationsSb.AppendLine("    押下時操作:「ボタン状態」==> ()");
            _operationsSb.AppendLine("    押下時操作 (押下ボタン) ==");
            _operationsSb.AppendLine("      cases 押下ボタン:");

            foreach (var item in GetListItems(listBlock))
            {
                _operationsSb.AppendLine($"        <{item}> -> skip");
            }

            _operationsSb.AppendLine("      end");
        }

        private IEnumerable<string> GetListItems(ListBlock listBlock)
        {
            return listBlock.Descendants<ParagraphBlock>()
                .Select(p => p.Inline?.FirstChild?.ToString() ?? string.Empty)
                .Where(s => !string.IsNullOrEmpty(s));
        }

        private string ExtractClassName(MarkdownDocument document)
        {
            var firstHeading = document.Descendants<HeadingBlock>().FirstOrDefault();
            return firstHeading?.Inline?.FirstChild?.ToString() ?? "UnknownClass";
        }

        private void ProcessTimeoutValue(MarkdownDocument document)
        {
            var level2Headers = document.Descendants<HeadingBlock>()
                .Where(h => h.Level == 2);

            foreach (var header in level2Headers)
            {
                var nextBlock = _blocks[_blocks.IndexOf(header) + 1];
                if (nextBlock is ListBlock listBlock)
                {
                    foreach (var item in GetListItems(listBlock))
                    {
                        if (item.Contains("秒でタイムアウト"))
                        {
                            var timeoutValue = item.Replace("秒でタイムアウト", "").Trim();
                            _valueSb.AppendLine($"  タイムアウト時間 := {timeoutValue};");
                            return;
                        }
                    }
                }
            }
        }
    }
}