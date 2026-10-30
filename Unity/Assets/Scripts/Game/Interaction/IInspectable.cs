using System.Collections.Generic;

namespace Napolitan.Game.Interaction
{
    /// <summary>바라보고 E키로 조사할 수 있는 대상</summary>
    public interface IInspectable
    {
        /// <summary>조준 시 표시할 이름 (예: "자판기")</summary>
        string Prompt { get; }

        /// <summary>조사 실행. 이번 조사로 얻은 Clue id 목록을 돌려준다 (없으면 빈 목록)</summary>
        IReadOnlyList<string> Inspect();
    }
}
