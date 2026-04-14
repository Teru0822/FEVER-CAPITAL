# 完了報告: HP・MP・レベルアップシステム（ローグライク要素）の実装

## 概要
ローグライク要素の土台として、プレイヤーのHP・MP・レベルを管理するシステムと、それらを可視化・操作するUI（Tabキー開閉メニュー含む）を実装および検証しました。

## 新機能・変更点（実装ファイル）
- **`Assets/Scripts/Managers/PlayerStatusManager.cs`**
  - レベル、HP、MPのデータ管理を行うシングルトンクラス。
  - お金（`MoneyManager`）を消費してレベルアップするコア機能を実装。コストは500から始まり、レベルが上がるごとに倍増する設定を採用しました。
- **`Assets/Scripts/UI/PlayerStatusUI.cs`**
  - 画面左下の表示を管理し、HP/MPバーと現在レベルの文字をManagerのイベントと連動させてリアルタイムに更新します。（残量0の時の描画バグ対策も適応済み）
- **`Assets/Scripts/UI/LevelUpMenuUI.cs`**
  - New Input System を用いてTabキーで開閉制御を行い、自身の現在レベルと必要コストを表示した上で、レベルアップを実行できるメニュー機能です。
- **`Assets/Scripts/Player/_TempStatusTest.cs`**
  - 1〜4のテンキー（あるいは数字キー）でHP/MPへのダメージや回復が行えるデバッグ機能を提供します。
- **`Assets/Editor/SetupStatusUI.cs`**
  - エディタ上部 `ODD-ODDS > Setup > Setup Status UI` メニューから、上記の複雑なUI Hierarchy構築や文字サイズ・フォントカラーなどのパラメータを1クリックで完全生成する手順を自動化しました。

## 作業とコミット情報
- 対応ブランチ: `feature/roguelike_status_and_levelup`
- プロジェクトの管理ルール（`GitWorkflow.md`）に従い、直接コミットは避け、新設した機能ブランチから `--no-ff` オプションを用いて `main` へ履歴ごとマージしました。
