# タスク報告: ドキュメントディレクトリの統一

- 変更内容: `ODD-ODDS/Docs` に存在していた `.md` ファイルを `/Docs` 直下の対応するディレクトリへ移動し、不要になった `ODD-ODDS` ディレクトリ全体を削除しました。また、プロジェクト全体のドキュメント管理ルールである `AGENTS.md` を更新し、今後のドキュメント作成時に `/Docs` を利用するよう明記しました。
- 変更理由: ドキュメントが複数ディレクトリに分散しているのを防ぎ、管理を `/Docs` に一本化するため。
- 対象ファイル:
  - `AGENTS.md`
  - `ODD-ODDS/Docs/04_Logs/Issue_CardDiscardFix_Report.md` -> `Docs/04_Logs/Issue_CardDiscardFix_Report.md`
  - `ODD-ODDS/Docs/02_Specifications/CardFlowRework_Plan.md` -> `Docs/02_Specifications/CardFlowRework_Plan.md`
  - 新規作成: `Docs/04_Logs/Task_UnifyDocs_Report.md`
- 確認した内容: 対象の `.md` ファイルが正しく `/Docs` 以下に移動し、`ODD-ODDS` フォルダ自体が正常に削除されたこと。
- 未確認事項 / 懸念点: 特になし。
