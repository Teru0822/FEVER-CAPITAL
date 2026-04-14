# Issue #25 完了報告 (モニター視点の投影とブラウン管表現)

## 概要
監視対象（人形）の主観視点をモニターに投影する機能を実装しました。
さらに映像のクオリティアップとして、ブラウン管モニター特有のアーケード/レトロ感を出すための専用カスタムシェーダーを作成・適用しました。

## 実装・変更内容
- **Render Textureを活用したカメラ映像の転送**
  - `DollViewTexture.renderTexture` を新規作成。
  - `_doll` の子オブジェクトとして目線用のカメラを配置し、`Output Texture` に上記をセットしました（※Unity6以降の環境では `Target Texture` ではなく `Output Texture` となる点を確認）。
- **ブラウン管表現（CRT Shader）の実装**
  - 新規シェーダー `Assets/Shaders/CRTMonitor.shader` を作成。
    - パラメーターとしてスキャンライン（横シマ模様）の数や濃さ、画面の湾曲（Distortion）、および四隅の暗さ（ビネット）を Inspector 上から設定できるように実装。
  - レンダリング先のマテリアル `MonitorScreenMat.mat` にこのシェーダーを適用し、テストを通して画作りの動作検証を完了しました。

## 対象ファイル (コミット済み)
- `Assets/Shaders/CRTMonitor.shader`
- `Assets/models/Materials/MonitorScreenMat.mat`
- `Assets/models/textures/DollViewTexture.renderTexture`
- `Assets/Scenes/PLAY.unity`

## 未確認事項 / 懸念点
モニターの設定そのものは完了しましたが、複数カメラがアクティブになっていることにより、今後の処理負荷や「ゲーム画面としてプレイヤーが操作するメインのカメラと、モニター用カメラの映像の切り替え」の制御システムをどうするかについては別途検討（別Issueでの実装等）が必要となる可能性があります。
