# 2vdm-spec-generator

## 概要

**2vdm-spec-generator**は、画面遷移システムの自然言語仕様からVDM++記述を自動生成するツールです。このツールは、仕様の記述を効率化し、モデル検証やシステム設計のプロセスを支援します。
![スクリーンショット 2025-01-05 164328](https://github.com/user-attachments/assets/1e84741f-0c46-41b2-9e87-fa303ff22213)

## 目次

- [概要](#概要)
- [機能](#機能)
- [仕様技術](#仕様技術)
- [インストール方法](#インストール方法)
- [使用方法](#使用方法)
- [貢献](#貢献)
- [ライセンス](#ライセンス)

## 機能

- **フォルダ選択**: 仕様ファイルが含まれるフォルダを選択します。
- **ファイルの読み込みと表示**: 選択したフォルダ内のMarkdown (.md) およびVDM++ (.vdmpp) ファイルを読み込み、ツリービューで表示します。
- **ファイルの編集と保存**: MarkdownファイルおよびVDM++ファイルの内容を編集し、保存することができます。
- **VDM++への変換**: Markdown形式の仕様をVDM++記述に変換します。
- **フォントサイズの調整**: エディタのフォントサイズを大きく、小さく、またはリセットする機能があります。
- **ファイルの新規作成と削除**: 新しいMarkdownファイルを作成したり、既存のファイルを削除することができます。
- **リアルタイムファイル監視**: フォルダ内のファイル変更をリアルタイムで監視し、自動的に更新します。

## 仕様技術

- [.NET MAUI](https://dotnet.microsoft.com/en-us/apps/maui)
- [CommunityToolkit.Maui](https://learn.microsoft.com/en-us/communitytoolkit/maui/)
- [Markdig](https://github.com/lunet-io/markdig)

## インストール方法

### 前提条件

- [.NET 7 SDK](https://dotnet.microsoft.com/download/dotnet/7.0) 以上
- [Visual Studio 2022](https://visualstudio.microsoft.com/) または他の対応エディタ

### クローンとビルド

1. リポジトリをクローンします:

    ```bash
    git clone https://github.com/yourusername/2vdm-spec-generator.git
    ```

2. プロジェクトディレクトリに移動します:

    ```bash
    cd 2vdm-spec-generator
    ```

3. 依存関係を復元します:

    ```bash
    dotnet restore
    ```

4. プロジェクトをビルドします:

    ```bash
    dotnet build
    ```

## 使用方法

1. アプリケーションを起動します。
2. **フォルダを選択**ボタンをクリックし、仕様ファイルが含まれるフォルダを選択します。
3. ツリービューに表示されたMarkdownファイルを選択します。
4. ファイルの内容を編集し、**ファイルの変更を保存**ボタンで保存します。
5. **VDM++に変換**ボタンをクリックすると、選択したMarkdownファイルがVDM++記述に変換されます。
6. 生成されたVDM++ファイルは自動的に保存場所に追加されます。

## 貢献

貢献を歓迎します！以下の手順に従ってください:

1. リポジトリをフォークします。
2. 新しいブランチを作成します:

    ```bash
    git checkout -b feature/新機能名
    ```

3. 変更をコミットします:

    ```bash
    git commit -m "新機能の追加"
    ```

4. ブランチをプッシュします:

    ```bash
    git push origin feature/新機能名
    ```

5. プルリクエストを作成してください。

## ライセンス

このプロジェクトはMITライセンスの下で公開されています。詳細については、[LICENSE](LICENSE)ファイルを参照してください。
