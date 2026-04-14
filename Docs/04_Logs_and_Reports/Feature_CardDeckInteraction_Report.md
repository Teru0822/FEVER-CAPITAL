# カード手札展開アニメーション機能 作業報告

- 変更内容: 
  - カードデッキをクリックした際、3Dカードが1枚ずつ手元視点へ飛んできて等間隔に並ぶアニメーション機能の実装
  - ECSキー等の視点キャンセル時に、並んだカードがデッキ位置へスッと戻っていく格納アニメーションの実装
- 変更理由: ローグライクゲームとして、手札をドローする動きをUnityの標準Coroutineを用いて滑らかかつ動的に表現するため
- 対象ファイル:
  - `[NEW] Assets/Scripts/Managers/HandManager.cs` (ドロー枚数・並び順の計算・生成・回収管理)
  - `[NEW] Assets/Scripts/Player/CardAnimator.cs` (指定座標へのLerp/Slerp移動単体処理)
  - `[MODIFY] Assets/Scripts/Interactables/InteractCardDeck.cs` (クリック時にカメラ移動とドローを連動)
  - `[MODIFY] Assets/Scripts/Camera/CameraFollow.cs` (ESCキャンセル時に `ClearHand` を呼び出す連携)
  - `Assets/Scenes/PLAY.unity` (Prefabや設定パラメータの調整適用)
- 確認した内容:
  - デッキをクリック後、カメラが手元へ移動し少し経ってから、指定枚数分の設定済みPrefabが1枚ずつ等間隔で配置されることを確認
  - `zOffset`, `yOffset`, `cardRotationOffset` の各パラメータにより、表裏の向きやカメラとの距離感がインスペクターから容易に調整できる機能が稼働していることを確認
  - ESCキーを押した際、展開されている最後尾のカードから順にデッキの発生源へ戻り、完了後にDestroyされる一連のクリーンアップ処理が動作することを確認
- 未確認事項 / 懸念点:
  - 実際の各種機能を持ったScriptableObject等（MoneyCardDataなど）との紐づけは今回は行っておらず、あくまで「見た目のアニメーション」の完成までとしています。今後は実際のデータとPrefabを紐付ける連携処理が必要になります。
