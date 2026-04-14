# 作業ログ：手元デッキ（PlayerHand）システムの実装

## 変更内容
- **PlayerHand.cs (新規)**: シングルトンとしてプレイヤーが保持しているカードデータ（最大10枚）を管理するクラスを作成。
- **InteractGachaButton.cs (改修)**: パック購入後、自動でカメラを手元へ移動させ、中身を即座に展開するフローを実装。
- **InteractCardDeck.cs (改修)**: デッキをクリックした際、新規パックを開封するのではなく、PlayerHandに保存されているカードを展開するように変更。
- **HandManager.cs (改修)**: 
    - 最大10枚のカードを上下2段（5枚×2段）に並べるレイアウト計算を実装。
    - 展開済みのカードをPlayerHandに保存する `SaveRemainingCardsToPlayerHand()` を実装。
- **CardObject.cs (改修)**: 
    - カードに効果テキスト（WorldSpace TMP）を表示するロジックを追加。
    - 移動カード使用時に、手元に残っている他のカードをデッキに保存する処理をフック。

## 変更理由
- パック購入後の手元でのカード選び、および選びきれなかったカードを後で使うための「保持デッキ」の概念を実現するため。
- カードの効果が視覚的にわかるようにするため。

## 対象ファイル
- `Assets/Scripts/Managers/PlayerHand.cs`
- `Assets/Scripts/Managers/HandManager.cs`
- `Assets/Scripts/Player/CardObject.cs`
- `Assets/Scripts/Interactables/InteractGachaButton.cs`
- `Assets/Scripts/Interactables/InteractCardDeck.cs`

## 確認した内容
（※コードレベルでの実装確認。Unityエディタ上での最終確認をユーザーに依頼）
- 各クラス間のデータの受け渡し（CardDataのリストの保存と取得）
- 10枚時のレイアウト座標計算
- 購入→展開→移動→保存のフローロジック

## 未確認事項 / 懸念点
- **アイテム効果の具体的実装**: 各アイテムの効果（移動数追加など）は `ItemCardData` の `TODO` として残っています。
- **UIのアサイン**: カードPrefabへのTextMeshPro追加とアサインはUnityエディタ側での手動作業が必要です。
