# ガチャシステム実装 作業報告

- 変更内容: ガチャパック購入用の管理スクリプト `PackManager.cs` および購入ボタンスクリプト `GachaButton.cs` を新規作成
- 変更理由: プレイヤーが所持金を消費してパックを購入し、その所持数を保持するシステムが必要だったため
- 対象ファイル:
  - `Assets/Scripts/Managers/PackManager.cs`
  - `Assets/Scripts/Interactables/GachaButton.cs`
- 確認した内容:
  - `MoneyManager` の `TryConsumeMoney` を利用し、消費に成功した場合のみパック所持数を増やす処理を実装した。
  - 既存コード同様に `IClickInteractable` と `Physics.Raycast` を用いて、3DオブジェクトのCubeに対するクリック判定が動作するように実装した。
  - クリックされた際、Z軸方向へ押し込まれて元に戻るコルーチンアニメーションを実装した。
  - コーディング規約に従った命名規則で記述し、日本語の XMLコメント 等を付与した。
- 未確認事項 / 懸念点: 
  - Unityエディタでの実際のアタッチ作業（`GachaButton` コンポーネントと `Collider` の設定、パックIDや売価の割り当て）、およびPlayモードでのマネー減算やアニメーションの動作確認は未実施です。
  - 必要に応じて `PackManager` をシーン内のマネージャー用オブジェクトに追加してください。
