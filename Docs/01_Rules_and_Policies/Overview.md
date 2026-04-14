# プロジェクト運用ルール 概要 (Overview)

このディレクトリ（`Docs/01_Rules_and_Policies`）では、本プロジェクトにおける各種ルールを階層別に管理しています。上位の階層ほどプロジェクト全体に関わる大きなルールであり、下位の階層になるほどより具体的な作業指示となります。

## ルールインデックス

### 01. チーム方針 (`01_Team_Policies/`)
プロジェクトに携わるメンバー全員が守るべき最も基礎的なルールです。
* [BasicPolicies.md](01_Team_Policies/BasicPolicies.md)
  * 基本方針、言語方針、作業範囲、禁止事項のまとめ。

### 02. 開発・コーディング環境 (`02_Development_Guidelines/`)
実際のコーディング作業や、Unity、エディタ環境などに関するルールです。
* [EnvironmentRules.md](02_Development_Guidelines/EnvironmentRules.md)
  * 文字コード、改行コード、ファイル名などの環境共通ルール。
* [UnityRules.md](02_Development_Guidelines/UnityRules.md)
  * `.meta` ファイル、Scene / Prefab などのUnity固有の注意事項。
* [CodingRules.md](02_Development_Guidelines/CodingRules.md)
  * C#の命名規則、コメント規約、ファイル分割などのコーディング規約。

### 03. ワークフロー・作業手順 (`03_Workflows/`)
Gitの運用、タスクの進め方、ドキュメントの管理に関する手順書です。
* [GitWorkflow.md](03_Workflows/GitWorkflow.md)
  * ブランチ運用、コミット・PR、マージや競合解決のルール。
* [TaskManagement.md](03_Workflows/TaskManagement.md)
  * タスクの分割、着手・完了時の対応、事後報告フォーマット。
* [DocumentRules.md](03_Workflows/DocumentRules.md)
  * ドキュメントの更新タイミングや、構成・ディレクトリ管理ルール。

### 04. AI特化のルール (`04_AI_Agents/`)
AIエージェントがプロジェクト内で活動する際の振る舞いに関する制限。
* [AIAgentRules.md](04_AI_Agents/AIAgentRules.md)
  * AIの作業範囲や注意事項、報告ルール。
