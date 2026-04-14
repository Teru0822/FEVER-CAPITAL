# カメラ・UIインタラクション機能 作業報告

- 変更内容: マネー、モニター、人形の箱、カードの束、自販機をクリックした際にカメラが指定視点へ移動する機能、およびUIの表示制御を追加
- 変更理由: シーン内のオブジェクトをクリックした際のインタラクション（視点のズームアップ、専用UIの表示）を実現するため
- 対象ファイル:
  - `Assets/Scripts/Camera/CameraFollow.cs` (拡張)
  - `Assets/Scripts/Interfaces/IClickInteractable.cs` (新規作成)
  - `Assets/Scripts/Interactables/InteractMoney.cs` (新規作成)
  - `Assets/Scripts/Interactables/InteractMonitor.cs` (新規作成)
  - `Assets/Scripts/Interactables/InteractDollBox.cs` (新規作成)
  - `Assets/Scripts/Interactables/InteractCardDeck.cs` (新規作成)
  - `Assets/Scripts/Interactables/InteractVendingMachine.cs` (新規作成)
  - `Assets/Scenes/PLAY.unity` (設定変更)
- 確認した内容:
  - 対象のColliderを持つオブジェクトをクリックした際に、指定された視点(ダミーのTransform)へ滑らかにカメラが移動することを確認
  - マネーをクリックしたときはUIが表示され、それ以外の領域（ESCキー含む）を押すとUIが非表示になることを確認
  - ESCキー押下でゲーム初期のカメラ位置へリセットされることを確認
  - Update内でのInputSystem.Mouseを用いたRaycast判定により、新Input System環境下でも確実にクリックが取得できることを確認
- 未確認事項 / 懸念点:
  - カード展開時の具体的な描画処理や中身のUI実装は未着手（TODOコメントのみ）
