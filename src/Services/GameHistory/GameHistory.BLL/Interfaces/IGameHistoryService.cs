using SeaBattle.Contracts.Dtos;

namespace GameHistory.BLL.Interfaces
{
    public interface IGameHistoryService
    {
        Task<IEnumerable<GameHistoryResponse>> GetAllGameHistories(GameHistoryRequest gameHistoryRequest);
        Task<TopPlayersResponse> GetTopPlayers(TopPlayersRequest topPlayersRequest);
    }
}