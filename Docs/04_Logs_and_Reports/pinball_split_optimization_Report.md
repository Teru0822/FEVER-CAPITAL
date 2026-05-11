# 「5」分裂処理の軽量化 — 要約

**対応日**: 2026-04-25 / **ブランチ**: `feature/pinball_manual_physics_manager` (main マージ済み)

## 何をやったか (4 点)

1. **分裂後のボールから Rigidbody と Collider を外した**
   → Unity 物理エンジンの管理対象外にして、数千個でも重くならないようにした。

2. **全ボールの位置/速度を 1 つの配列にまとめ、C# Jobs + Burst で一括並列処理**
   → 個別 `Update()` を回さず、ネイティブコード化した並列ジョブで毎フレーム一括計算。

3. **ボール同士の衝突判定を空間ハッシュ (グリッド) に置き換え**
   → 全ペア比較 O(N²) ではなく、近くのマス目 3×3 だけ見る O(N·k) に改善。
   6500 個での比較回数が約 **100 倍** 削減。

4. **高世代 (例: 4 世代目以降) は ParticleSystem のバーストに切り替え**
   → GameObject を増やさず見た目だけ維持。

## 得られた成果

- 6500 個規模でも実用フレームレートで動作
- Inspector パラメータ (`PinballBallConfig`) で反発係数・重力方向・UI 等をランタイム調整可
- `pinballRoot` を動かすと位置/スケール/重力が自動追従

## 関連ファイル

- [PinballBallManager.cs](../../Assets/Scripts/PinballBallManager.cs) — 一括管理 + Jobs 定義
- [PinballBallController.cs](../../Assets/Scripts/PinballBallController.cs) — gen 0 専用 (分裂トリガーのみ)
- [PinballBallConfig.cs](../../Assets/Scripts/PinballBallConfig.cs) — Inspector 設定
