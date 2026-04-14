## 1. このファイルの目的
このファイルは、本プロジェクトにおけるコーディング規約を定義するためのものである。  
可読性・保守性・複数人開発時の作業効率を高めるため、命名規則や記述ルールを統一する。

---

## 2. 基本方針
- 命名は一貫性を最優先とする。
- 誰が見ても意味が分かる名前を付ける。
- 過度な略称は使わない。
- 人が読む必要のあるコメントは日本語で記述する。
- 既存コードと規約の両方を確認し、規約に従って実装する。
- 規約の統一によって、可読性、保守性、レビュー効率を高める。
- 読む人が迷わない書き方を優先する。

---

## 3. 命名規則

### 3.1 private 変数
private 変数は、先頭に `_` を付けたキャメルケースで記述する。

#### 例
```csharp
private int _playerHp;
private float _moveSpeed;
private Rigidbody2D _rigidBody2D;
private Animator _playerAnimator;
```

#### 禁止例
```csharp
private int playerHp;
private int PlayerHp;
private int _PlayerHp;
```

---

### 3.2 public 変数
public 変数は、キャメルケースで記述する。

#### 例
```csharp
public int playerHp;
public float moveSpeed;
public GameObject targetObject;
```

#### 禁止例
```csharp
public int PlayerHp;
public int _playerHp;
```

---

### 3.3 定数（const）
定数は、すべて大文字で記述し、単語の区切りは `_` を用いる。

#### 例
```csharp
private const int MAX_HP = 100;
private const float DEFAULT_MOVE_SPEED = 5.0f;
private const string PLAYER_TAG = "Player";
```

#### 禁止例
```csharp
private const int MaxHp = 100;
private const int maxHp = 100;
private const int max_hp = 100;
```

---

### 3.4 関数名
関数名は、パスカルケースで記述する。

#### 例
```csharp
public void MovePlayer()
private void UpdateAnimation()
public bool CheckGround()
```

#### 禁止例
```csharp
public void movePlayer()
private void update_animation()
public bool checkGround()
```

---

### 3.5 Script 名
Script 名は、パスカルケースで記述する。  
ファイル名とクラス名は一致させること。

#### 例
```csharp
PlayerController.cs
GameManager.cs
TitleSceneController.cs
```

#### 禁止例
```csharp
playerController.cs
game_manager.cs
title_scene_controller.cs
```

---

### 3.6 仮オブジェクト・アセット名
開発中の仮素材や一時的なScene内オブジェクトの名前は、先頭に `_` を付けて判別しやすくする。

#### 例
```text
_doll
_testCube
_tempMusic
```

---

## 4. 略称に関するルール
- 過度な略称は使用しない。
- 一部の開発者しか分からない略称は禁止する。
- 型名や役割が分かる命名を優先する。
- 変数名を短くしすぎて意味が分からなくならないようにする。

### 良い例
```csharp
private Rigidbody2D _rigidBody2D;
private Animator _playerAnimator;
private AudioSource _audioSource;
private GameObject _targetObject;
```

### 悪い例
```csharp
private Rigidbody2D _rb2;
private Animator _anim;
private AudioSource _au;
private GameObject _obj;
```

---

## 5. コメント規約
- コメントは日本語で記述する。
- 「何をしているか」だけでなく、「なぜ必要か」が分かる内容を優先する。
- 一時的なメモや意味の薄いコメントは残さない。
- 将来的に誤解を招く曖昧なコメントは書かない。
- コメントと実装内容がずれないようにする。

### 例
```csharp
// プレイヤーが地面に接しているかを判定する
private bool CheckGround()
{
    ...
}
```

---

## 6. ファイル記述ルール
- ソースコードおよびテキストファイルは UTF-8 で保存する。
- Shift-JIS などの環境依存文字コードは使用しない。
- 改行コードは原則 LF に統一する。
- 文字コードや改行コードの変更だけを目的とした不要な差分は作らない。
- エディタ設定の違いによる大量差分が出た場合は、そのままコミットしない。

---

## 7. ファイル分割ルール
- 1ファイルには、1つの明確な責務だけを持たせる。
- 1つの動作や役割につき、原則として1ファイルで管理する。
- 複数の大きな役割を1つの Script に詰め込まない。
- 処理が増えて責務が複数にまたがる場合は、機能ごとに Script を分割する。
- ファイル名は、そのファイルの責務が分かる名前にする。
- Unity では、1つの主要な MonoBehaviour クラスにつき1ファイルを原則とする。

### 例
- 移動処理は `PlayerMovement.cs`
- ジャンプ処理は `PlayerJump.cs`
- 攻撃処理は `PlayerAttack.cs`
- 体力管理は `PlayerHealth.cs`

### 避ける例
- `PlayerController.cs` の中に移動、ジャンプ、攻撃、体力管理、UI更新をすべて書く
- 1つの Script に複数の大きな責務を持たせる

---

## 8. 実装時の注意
- 新しいゲームシステムのファイルを追加する際は、必ず関連する他のファイル（既存の実装や参照元・先など）を確認し、処理の重複や無駄な実装が生じないように作成する。
- 変数名、関数名、Script 名は規約に従って統一する。
- 規約に反する既存コードを見つけた場合でも、依頼範囲外で無関係な全面修正は行わない。
- 新規作成分および修正対象箇所は必ず本規約に従う。
- 読む人が迷わない名前を優先する。
- 一時しのぎの命名を避ける。
- 将来の保守を意識して、責務が分かる名前を付ける。

---

## 9. ブランチ作成時の規約

### 9.1 基本ルール
- ブランチ名はスネークケースで記述する。
- 用途が分かる接頭辞を付ける。
- 内容を見ただけで作業内容が分かる名前にする。
- 曖昧な名前は使用しない。

---

### 9.2 機能追加
機能追加の場合は、以下の形式で作成する。

```text
feature/(追加機能内容)
```

#### 例
```text
feature/data_save
feature/player_jump
feature/title_scene
```

---

### 9.3 バグ修正
バグ修正の場合は、以下の形式で作成する。

```text
fix/(修正内容)
```

#### 例
```text
fix/player_move_bug
fix/title_button_error
fix/enemy_spawn_issue
```

---

### 9.4 緊急バグ修正
緊急のバグ修正で、レビューを待たずにマージする必要がある場合は、以下の形式で作成する。

```text
hotfix/(緊急の修正内容)
```

#### 例
```text
hotfix/game_start_error
hotfix/save_data_break
hotfix/build_failure
```

---

## 10. 禁止事項
- 規約に反した命名を新規追加すること
- 意味が伝わりにくい略称を使うこと
- 関数名をキャメルケースやスネークケースで書くこと
- Script 名を小文字始まりやスネークケースで書くこと
- ブランチ名に曖昧な名前を付けること  
  例: `fix/test`, `feature/work`, `feature/tmp`
- Shift-JIS など環境依存文字コードで保存すること
- 改行コードだけが変わった差分を安易にコミットすること
- 1つの Script に複数の大きな責務を詰め込みすぎること

---

## 11. まとめ
本プロジェクトでは、以下を徹底する。

- private 変数は `_` + キャメルケース
- public 変数はキャメルケース
- 定数は大文字 + `_`
- 関数名はパスカルケース
- Script 名はパスカルケース
- 過度な略称は禁止
- ブランチ名はスネークケース
- 機能追加は `feature/...`
- バグ修正は `fix/...`
- 緊急修正は `hotfix/...`
- コメントは日本語で記述する
- ソースコードやテキストは UTF-8 を使用する
- 改行コードは原則 LF に統一する
- 1ファイル1責務を原則とする
