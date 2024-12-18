using Markdig;
using Markdig.Syntax;
using System.Text;

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
        private readonly StringBuilder _sb;
        private List<Block> _blocks;
        private int _currentBlockIndex;

        public VdmPlusPlusRenderer()
        {
            _sb = new StringBuilder();
            _blocks = new List<Block>();
            _currentBlockIndex = 0;
        }

        public string Render(MarkdownDocument document)
        {
            // すべてのブロックを配列に格納
            _blocks = document.ToList();
            
            // クラス名を取得
            var className = ExtractClassName(document);
            _sb.AppendLine($"class 「{className}」");

            // 見出しレベルに基づいて要素を処理
            for (_currentBlockIndex = 0; _currentBlockIndex < _blocks.Count; _currentBlockIndex++)
            {
                if (_blocks[_currentBlockIndex] is HeadingBlock heading)
                {
                    switch (heading.Level)
                    {
                        case 1:
                            RenderClassDefinition(heading);
                            break;
                        case 2:
                            RenderTypes(heading);
                            break;
                        case 3:
                            RenderOperations(heading);
                            break;
                    }
                }
            }

            _sb.AppendLine($"end 「{className}」");
            return _sb.ToString();
        }

        private Block GetNextBlock()
        {
            if (_currentBlockIndex + 1 < _blocks.Count)
            {
                return _blocks[_currentBlockIndex + 1];
            }
            return null;
        }

        private void RenderClassDefinition(HeadingBlock block)
        {
            _sb.AppendLine("types");
            _sb.AppendLine("  /* 変換ルールA-1 */");
        }

        private void RenderTypes(HeadingBlock block)
        {
            var nextBlock = GetNextBlock();
            if (nextBlock is ListBlock listBlock)
            {
                _sb.AppendLine("  /* 変換ルールB-2 */");
                _sb.AppendLine("  「ボタン状態」= <非押下> | " +
                    string.Join(" | ", GetListItems(listBlock).Select(item => $"<{item}>")));
                _currentBlockIndex++; // リストブロックを処理したのでインデックスを進める
            }
        }

        private void RenderOperations(HeadingBlock block)
        {
            var nextBlock = GetNextBlock();
            if (nextBlock is ListBlock listBlock)
            {
                _sb.AppendLine("operations");
                _sb.AppendLine("  /* 変換ルールB-3 */");
                _sb.AppendLine("  private");
                _sb.AppendLine("    押下時操作:「ボタン状態」==> ()");
                _sb.AppendLine("    押下時操作 (押下ボタン) ==");
                _sb.AppendLine("      cases 押下ボタン:");

                foreach (var item in GetListItems(listBlock))
                {
                    _sb.AppendLine($"        <{item}> -> skip");
                }

                _sb.AppendLine("      end");
                _currentBlockIndex++; // リストブロックを処理したのでインデックスを進める
            }
        }

        private IEnumerable<string> GetListItems(ListBlock listBlock)
        {
            return listBlock.Descendants<ParagraphBlock>()
                .Select(p => p.Inline?.FirstChild?.ToString() ?? string.Empty);
        }

        private string ExtractClassName(MarkdownDocument document)
        {
            var firstHeading = document.Descendants<HeadingBlock>().FirstOrDefault();
            return firstHeading?.Inline?.FirstChild?.ToString() ?? "UnknownClass";
        }
    }
} 