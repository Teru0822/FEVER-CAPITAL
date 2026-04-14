# Issue #4 完了報告

## 概要
「カードのデータ構造設計」として、お金・移動・アイテムを表すカードのデータ基盤（ScriptableObject）実装を完了しました。

## 新機能・変更点
- **列挙型（Enum）定義の追加**
  - `CardType.cs` : お金、移動、アイテムの3種類を定義。
  - `DirectionType.cs` : 下、左、上、右を定義（README指定に準拠）。
  - `ItemEffectType.cs` : アイテム特化の各種効果の定数。
- **データ構造（ScriptableObject）の追加**
  - `CardData.cs` : 共通プロパティとなる（名前・種類・画像・詳細）を保持する抽象レイヤ。
  - `MoneyCardData.cs` : お金カード（増減用のパラメーターを所持）。
  - `MoveCardData.cs` : 移動カード（方向やマス数のパラメーターを所持）。
  - `ItemCardData.cs` : アイテムカード（効果種類と数値等のパラメーターを所持）。

## コミット情報
- 開始コミット: `Refs #4` にてブランチを切り、作業を開始。
- 対応ブランチ: `feature/issue4_card_data` （`main` へマージ完了・プッシュ済み）。

## 次のステップへの引き継ぎ
- 今回追加したScriptableObjectを用いて、UnityエディタのProjectウィンドウから右クリック (`Create > Card Data > ...`) によって各種データを作成できるようになりました。実際のデータアセットを作っていく段階で使用してください。
