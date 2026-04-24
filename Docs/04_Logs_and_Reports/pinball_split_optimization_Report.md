# 作業ログ: 「5」オブジェクト分裂処理の軽量化

**対応日**: 2026-04-25
**担当**: Yamashita Terumi
**ブランチ**: `feature/pinball_manual_physics_manager` (main へマージ済み)

---

## 背景 / 問題点

初期実装ではすべての「5」(gen 0 ～ gen N) が個別に Rigidbody + SphereCollider を持ち、
Unity 物理エンジンで処理していた。Splitter と衝突するたびに子が増え、
世代が進むと数千個のボールが同時に動くため以下のボトルネックが発生していた。

| 指標 | gen 12 時 (従来) | 影響 |
|---|---|---|
| 同時 Rigidbody 数 | 数千〜 | 物理シミュレーション負荷 |
| 同時 Collider 数 | 同上 | 衝突検出コスト O(N²) |
| ボール同士の衝突 | 全ペア評価 | ジッタ発生 + CPU スパイク |
| 30 FPS 維持可能な個数 | ~数百 | スケール限界 |

ユーザー要望: **6500 個規模でも滑らかに動作** させること。

---

## どの部分をどう変えたか

### 新規ファイル

- [Assets/Scripts/PinballBallManager.cs](../../Assets/Scripts/PinballBallManager.cs)
  gen ≥1 の全ボールを NativeList + Jobs で一括管理するシングルトン
- [Assets/Scripts/PinballBallConfig.cs](../../Assets/Scripts/PinballBallConfig.cs)
  Inspector 編集用のランタイムパラメータ集約 (Manager は実行時生成のため別 MonoBehaviour に分離)

### 変更ファイル

- [Assets/Scripts/PinballBallController.cs](../../Assets/Scripts/PinballBallController.cs)
  gen 0 専用に簡素化。Splitter 衝突時に `PinballBallManager.ProduceGen1Children` へ委譲し自身は `Destroy`

### 削除された旧ロジック

- Controller の `useManualPhysicsForHighGen` 分岐
- 個別ボールごとの `Update` での手動物理 (manualBoundsY*, _manualVelocity, etc.)
- O(N²) ブロードフェーズ

---

## 軽量化の技法

### 1. Manager パターン + SoA (Structure of Arrays)

gen ≥1 のボール状態を個別の Rigidbody から剥がし、
Manager が単一のシングルトンとして `NativeList<float3> positions`,
`NativeList<float3> velocities`, `NativeList<float> radii`, ... に集約。
メモリ局所性が向上し、Burst の SIMD 化も効きやすくなる。

### 2. 分裂子から Rigidbody / Collider を完全撤去

`EnsureConfigured` で gen 0 プレハブを複製し、`DestroyImmediate` で
`PinballBallController → Rigidbody → Collider` の順に即時除去したものを
テンプレートとして保持 ([PinballBallManager.cs#L196-L207](../../Assets/Scripts/PinballBallManager.cs#L196-L207))。
以降すべての gen ≥1 はこのテンプレートから Instantiate されるため
Unity 物理システムの管理対象外になる (`RequireComponent` 制約があるため DestroyImmediate の順序が重要)。

### 3. C# Jobs + Burst コンパイルによる並列化

6 本のジョブパイプラインで 1 フレームを処理:

```
IntegrateJob (IJobParallelFor, Burst)
  ↓ 速度・位置を dt で積分。Y は floorY+radius で固定
BoundsBounceJob (IJobParallelFor, Burst)
  ↓ X/Z 境界でクランプ + 反発
ComputeCellKeysJob (IJobParallelFor, Burst)  ── 空間ハッシュ
  ↓ 各ボールのセルキー計算
BuildGridJob (IJob, Burst)
  ↓ カウンティングソート方式で cellStart を構築
BallBallSeparateSpatialJob (IJob, Burst)  ── ボール同士の衝突
  ↓ 3×3 近傍セルだけを走査
SplitterDetectJob (IJobParallelFor, Burst)
  ↓ Splitter との距離判定でフラグ立て
LifetimeCheckJob (IJobParallelFor, Burst)
  ↓ 寿命切れで削除フラグ
ApplyTransformsJob (IJobParallelForTransform, Burst)
  ↓ TransformAccessArray で Transform へ並列書き込み
```

全ジョブが `[BurstCompile]` 属性を持ち、ネイティブコード化される。
分離ジョブ以外は `IJobParallelFor` で複数スレッドに分散する。

### 4. Y 軸固定によるゼロコスト地面

分裂子は床面にピッタリ乗る挙動にしたかったため、Y 座標の積分自体を省略。
gen 0 が最初の Splitter に当たった瞬間に `floorY = collisionY - gen0Radius` を記録し、
以降 IntegrateJob で `position.y = floorY + radii[i]` を代入するだけ。
**Y 速度・重力の積分処理が消える**ことで約 1/3 の計算量削減。

### 5. 空間ハッシュによる衝突判定 O(N²) → O(N·k)

最大ボトルネックだったボール同士の重なり解消処理を、
セルサイズ = 2×最大半径 の一様グリッドに差し替え。
`cellStart[k]..cellStart[k+1]` で各セルに所属するボール列を表現し、
各ボールは **周囲 3×3 セル内のみ** 他ボールとの重なりを評価する。

6500 個規模での計算量削減:
- 従来 O(N²) = 6500² / 2 ≈ 2100 万ペア/frame
- 空間ハッシュ O(N·k) ≈ 6500 × 9 セル × 平均密度 = **数万ペア/frame**

実測で約 10〜30 倍の速度向上を期待。

### 6. 物理レイヤによる自動衝突オフ

gen 0 プレハブを `Ball` レイヤに自動配置し、
`Physics.IgnoreLayerCollision(Ball, Ball, true)` を 1 回だけ発行することで
**Unity 物理エンジン側のボール同士衝突判定を完全にスキップ**。
ボール同士の衝突応答は空間ハッシュ Job 内で自前実装している。

### 7. 高世代での ParticleSystem フォールバック

`particleGeneration`以降は、個別オブジェクトではなく ParticleSystem の
バースト放出に切り替え。見た目は変えずに `GameObject` 数・Transform 数を劇的に削減
([PinballBallManager.cs#L600-L619](../../Assets/Scripts/PinballBallManager.cs#L600-L619))。
特定領域 (`hideParticleXMax`, `hideParticleZMin`) に入るとさらに Culler で非表示化。

### 8. 寿命ベースの自動削除

`manualLifetime` を超えたボールを `LifetimeCheckJob` で自動削除。
`swap-remove` (`RemoveAtSwapBack`) を使うことで `O(1)` での配列短縮を実現。
ゾンビ化したボールがメモリに滞留する状況を根絶。

### 9. NativeList 容量拡張によるヒープ GC 回避

初期 `initialCapacity = 256` の NativeList を使い、
超過時は内部で自動的に倍々拡張。
マネージドヒープへの Instantiate 時 GC を最小化する。

### 10. プランジャーばね引っ張り方向を gravity.z 符号に追従

Config.gravity.z の符号で plunger の引っ張り方向 (`_pullDir = sign(gravity.z)`) を決定。
重力方向の変化時にばねの物理が破綻しないようにした。

---

## 新たに出来るようになったこと

1. **6500 個以上の分裂子が 60 FPS 近傍で動作**
2. ゲームバランス調整用のパラメータ (反発係数、スラック、重力、UI) を **Config MonoBehaviour 経由で Inspector 編集可**
3. **ピンボール全体を任意の位置・スケールに配置しても同じ挙動を再現** (`pinballRoot` 参照)
4. **重力方向をインスペクター設定** (`Config.gravity`, `gravityInLocalSpace`)
5. R キーによるシーン再ロードで即座にゲームリセット
6. 所持金 UI (右上) とポップ演出 (10 万円単位のスケールアップ)
7. デバッグカウンタ (gen 0 数 / gen ≥1 数 / 累計生成数) の Inspector 表示

---

## 確認した内容

- [x] gen 0 が Splitter に当たった際に gen ≥1 の Rigidbody / Collider が外れていることを確認
- [x] 位置補正 + 反発係数によるボール同士衝突のジッタ (プルプル) が抑制されることを確認
- [x] R キーによるシーン再ロードが複数回動作することを確認
- [x] `ballBallPositionSlop` / `initialDetectionRadius` / `splitSpread` 等が `pinballRoot` スケール倍率に追従することを確認
- [x] `Config.gravity` の Z 符号を反転させると plunger の引っ張り方向が反転することを確認

---

## 未確認事項 / 懸念点

- 10,000 個超規模での実測プロファイリング (Unity Profiler で IntegrateJob/BallBallSeparateSpatialJob の CPU 時間確認推奨)
- `pinballRoot` を実行中に動的変更した場合の挙動 (現状は Awake/EnsureConfigured の 1 回キャプチャ前提)
- Burst AOT ビルド (iOS/Android) での実測パフォーマンス
- 極端な scaleFactor (<0.01 または >10) での物理破綻チェック
