# カード連携機能（パック購入〜手札展開〜効果発動）実装 作業報告

## 変更内容
パックを購入してデッキをクリックすると、ランダムな5枚のカードが手元に飛んでくるアニメーションが再生され、
展開された各カードをクリックするとカードの種類に応じた効果（所持金増加・人形の移動など）が発動する機能を実装しました。

## 変更理由
企画書のコア・ゲームループ「パックを買ってカードを引いてコマが動く」の基本サイクルをコードで実現するため。

## 対象ファイル

### 変更（MODIFY）
- `Assets/Scripts/Managers/PackManager.cs`
  - `_allAvailableCards` フィールドの追加（InspectorでCardDataを登録するDatabaseとして機能）
  - `OpenPack(int packId)` メソッドの追加（パック消費・必ず1枚移動カードを含む5枚ランダム抽選）
- `Assets/Scripts/Managers/HandManager.cs`
  - `DrawCards` の引数を `int count` から `List<CardData>` に変更
  - `CardObject` を動的にアタッチして `Initialize()` を呼び出すロジックを追加
  - `RemoveCardFromHand(GameObject)` 追加（使用後カードの手札リスト除去用）
- `Assets/Scripts/Interactables/InteractCardDeck.cs`
  - デッキクリック時に `PackManager.OpenPack()` を呼んでカードデータを取得
  - 取得結果（List<CardData>）を `HandManager.DrawCards()` へ橋渡しする連携処理を実装

### 新規作成（NEW）
- `Assets/Scripts/Player/CardObject.cs`
  - 展開されたカードが保持するデータ（CardData）を格納し、クリックで効果（AddMoney, Move）を発動するコンポーネント
  - IClickInteractable を実装し、New Input System（Mouse + Raycast）で自分自身のクリックを検出
  - カードが使用された後は、HandManagerのリストから除去してから自己破棄する

## 確認した内容
- PackManager.OpenPack() は移動カードが登録されていれば最低1枚必ず含めてシャッフルした5枚のリストを返すことを実装上確認
- HandManager.DrawCardsRoutine() でCardObjectの動的AddComponent → Initialize呼び出しが接続されていることを確認
- CardObject.OnInteract() でMoneyCardData/MoveCardData/ItemCardDataの効果分岐がそれぞれ正しくMoneyManager・PlayerMovementのAPIへアクセスすることを確認

## 未確認事項 / 懸念点
- **Unityエディタでの実動作確認はまだです。** 以下の設定が必要です：

### ユーザーによるUnity上の設定作業（必須）
1. **PackManager の InspectorでCardDataを登録**
   - シーン内の `PackManager` オブジェクトの Inspector を開く
   - `All Available Cards` リストに、作成済みの `MoneyCardData`・`MoveCardData`・`ItemCardData` アセット（ScriptableObject）を必要なだけ追加する

2. **CardPrefabにCardObjectコンポーネントを事前付与（推奨）**
   - 現在は動的 `AddComponent` で自動追加されますが、Prefabに最初からアタッチしておくと安定します
   
3. **CardPrefab に Collider が付いていることを確認**（クリック判定に必要）

4. **パックを購入してからデッキをクリック**してカード展開を試してください
