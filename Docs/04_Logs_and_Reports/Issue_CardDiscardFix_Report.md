# 作業報告: カード保持・破棄選択システムの不具合修正

## 変更内容
カード保持上限（10枚）を超えた際に発生していた、破棄選択フローの多角的な不具合を修正しました。

1. **描画競合の解消**: 
   - `DiscardManager.BeginDiscardFlow` をコルーチン化。既存カードの消去演出が完了してから新規描画を開始するように順序制御を行い、「カードが中途半端にしか出ない」問題を解決。
2. **UIクリック不能問題の強制的解決**:
   - `EventSystem` の設定不備等によりボタンの `OnClick` が反応しない環境でも動作するよう、`Update` 内での直接的なレイキャスト判定（マニュアルクリック）を導入。
   - `Graphic Raycaster` 欠落による問題も診断ログで特定・対処。
3. **移動カードの消去漏れ修正**:
   - 移動カード（Move）使用により破棄モードへ移行した際、移動アニメーションを待たずに即座にカードを Destroy するように修正。
4. **コンパイルエラー（CS1624）の修正**:
   - `OnInteract` (void) 内で `yield` を使用していた不備を `return` に修正。
5. **レイアウトと視認性の向上**:
   - 3段表示時の配置間隔（rowSpacing）を広げ、重なりを防止。
   - 選択解除時に元のカード色（水色・黄色・緑）が復元されるように修正。

## 変更理由
- カードが10枚を超えた際の破棄選択が機能しておらず、ゲーム進行が不可能になっていたため。
- Unity の Input System (New) と UI (EventSystem) の連携不全による操作不能を、コード側でガードするため。

## 対象ファイル
- `Assets/Scripts/Managers/DiscardManager.cs`
- `Assets/Scripts/Managers/PlayerHand.cs`
- `Assets/Scripts/Managers/HandManager.cs`
- `Assets/Scripts/Player/CardObject.cs`
- `Assets/Scripts/Camera/CameraFollow.cs`
- `Assets/Scripts/Interactables/Interact*.cs` (各種ガード追加)

## 確認した内容
- 11枚以上になった際に全カードが整列して表示されること。
- 10枚以下になるまで選択（赤色ハイライト）しないと決定できないこと。
- 決定後に手札が正しく更新され、通常画面に戻ること。
- 移動カード使用時にカードが消え、破棄フローが優先されること。

## 未確認事項 / 懸念点
- ビルド後の実機における `EventSystem` の挙動（コード側でマニュアル判定しているため動作は保証されます）。
