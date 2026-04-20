using System;

namespace MiniGames
{
    /// <summary>
    /// 全てのミニゲームモジュールが実装すべき共通インターフェース。
    /// Game Loop の「増幅フェーズ」で統合システム (GameManager) から呼び出されます。
    /// </summary>
    public interface IMiniGame
    {
        /// <summary>
        /// ミニゲームの初期化を行います。
        /// </summary>
        /// <param name="betAmount">プレイヤーが賭けた金額</param>
        void Initialize(int betAmount);

        /// <summary>
        /// ミニゲームを開始します。
        /// </summary>
        void StartGame();

        /// <summary>
        /// ミニゲーム完了時に発火するイベント。
        /// Action<bool isSuccess, float multiplier>
        /// - isSuccess: ゲームに成功したかどうか
        /// - multiplier: 成功時の獲得倍率（失敗時は通常0）
        /// </summary>
        event Action<bool, float> OnGameCompleted;
    }
}
