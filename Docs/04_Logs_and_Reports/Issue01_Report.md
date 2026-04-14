# Issue #1 完了報告

## 概要
「進行ステートマシンのベース & 所持金増減スクリプト」の基盤実装を完了しました。

## 新機能・変更点
- **`GameState.cs` の追加**
  - ゲームの進行状態 (`Preparation`, `PackOpening`, `ResultCheck`, `GameClear`, `GameOver` など) を定義。
- **`GameManager.cs` の追加**
  - 現在の `GameState` を保持し、状態が変化した際にイベント (`OnStateChanged`) で他のクラスへ通知を行う Singleton クラス。
- **`MoneyManager.cs` の追加**
  - 現在の所持金 (`_currentMoney`) を管理。初期値を設定可能。
  - 所持金を変動させるメソッド (`AddMoney`) と、指定額を消費可能かチェックして消費するメソッド (`TryConsumeMoney`) を実装。
  - 変動時にはイベント (`OnMoneyChanged`) を通知。

## コミット情報
- 対応ブランチ: `feature/game_state_and_money` -> `main` へマージ完了。

## 次のステップへの引き継ぎ
- これらのマネージャーロジックは単体で動作するベース部分であるため、ゲーム内に適用するには空のGameObject等の適切な場所にアタッチして使用してください。
- 各種UI（所持金表示など）は `MoneyManager.Instance.OnMoneyChanged` をサブスクライブすることで更新処理を実装できます。
