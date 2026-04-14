# Issue 36 作業報告 (BGM・SE用 AudioManagerの作成)

- 変更内容: BGMおよびSEを管理・再生するための `AudioManager.cs` を新規作成
- 変更理由: BGMやSEをDictionaryで管理し、シングルトンで各所から呼び出せる仕組みが必要だったため
- 対象ファイル: `Assets/Scripts/Managers/AudioManager.cs`
- 確認した内容: コーディング規約（`CodingRules.md`）に準拠したフォーマットで実装を作成したこと
- 未確認事項 / 懸念点: 
  - Unityエディタ上でのアタッチ（AudioSource2つの追加、データリストへのAudioClip登録設定）および実際の音声再生テストは未実施。
