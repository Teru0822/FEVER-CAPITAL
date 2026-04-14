# カードフローリワーク 作業ログ報告書

## 対応概要
自販機ボタン押下〜移動カード使用〜アイテム使用〜マネー処理の一連の流れを自動進行する「CardFlowManager」を導入し、カードゲーム進行の根幹となるフローをリワークしました。

## 変更内容
1. **ステートマシンの導入**
   - `CardFlowManager.cs` を新規作成し、`ShowingCards` -> `OptionalItemUse` -> `SelectingMoveOrder` -> `ProcessingMoney` -> `SavingItems` -> `MovingDoll` というフェーズ管理を実装。
2. **アイテムフェーズと移動フェーズの順序整理**
   - パックから出た直後に引き直し（Redraw）等が行えるよう、アイテム即時使用フェーズを移動順序選択フェーズの前に配置。
3. **引き直し（Redraw）カードの実装**
   - アイテム即時使用フェーズ中、および平時の所持手札からの使用時に対応。「5枚引き直してフローを開始/リスタートする」処理を追加。
4. **旧スクリプトからの処理委譲**
   - `InteractGachaButton.cs` や `CardObject.cs` の古いカード使用ロジックを整理し、進行管理を `CardFlowManager` に集約。
5. **UIの追加と整備**
   - テキストのレイキャスト貫通防止として、`InteractDollBox`、`InteractCardDeck` 等の 3D オブジェクト側に `IsPointerOverGameObject` によるガードを追加。不要な視点移動バグを解消。
   - `CameraFollow.cs` にて、フロー進行中のESCキーによる視点リセットを無効化。
   - `PlayerMovement.cs` に複数回移動を視覚化するコルーチン `MoveSequence` を追加。
   - `DebugMoneyUI.cs` をデバッグ用に作成。

## 変更理由
プレイヤーの操作回数を減らし、より直感的かつスムーズなカード処理を行えるようにするため。また、今後の追加拡張（アイテム効果や演出）を組み込みやすい状態にするため。

## 対象ファイル
- `Assets/Scripts/Managers/CardFlowManager.cs` (新規)
- `Assets/Scripts/Managers/PlayerHand.cs`
- `Assets/Scripts/Managers/DiscardManager.cs`
- `Assets/Scripts/Player/CardObject.cs`
- `Assets/Scripts/Player/PlayerMovement.cs`
- `Assets/Scripts/Interactables/InteractGachaButton.cs` ほか各種インタラクトスクリプト
- `Assets/Scripts/Camera/CameraFollow.cs`
- `Assets/Scripts/UI/DebugMoneyUI.cs` (新規)

## 確認した内容
- 自販機ボタン（または保持デッキからのRedraw使用）経由で手札が5枚並ぶこと。
- UI上でアイテム使用と移動順序が正しく選択・処理されること。
- マネー自動追加と、使わなかったアイテムの保持/破棄フローへの移行が機能すること。
- UIのボタンクリック時に背景の3Dオブジェクト（人形の箱など）への判定が貫通しないこと。
- ESCキーでフロー中にカメラが初期化されず、無効化されていること。

## 未確認事項 / 懸念点
- 今後の新しいアイテム効果（Redraw、移動力増加以外）が追加された際の確認。
- アンドロイド/iOS環境におけるUIのタッチ操作と貫通防止の追試。
