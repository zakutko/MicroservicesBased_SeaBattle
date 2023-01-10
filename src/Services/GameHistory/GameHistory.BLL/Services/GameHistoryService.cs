using AutoMapper;
using Dapper;
using GameHistory.BLL.Interfaces;
using GameHistory.DAL.Interfaces;
using SeaBattle.Contracts.Dtos;
using System.Data.SqlClient;

namespace GameHistory.BLL.Services
{
    public class GameHistoryService : IGameHistoryService
    {
        private readonly IMapper _mapper;
        private readonly IRepository<DAL.Models.GameHistory> _gameHistoryRepository;

        public GameHistoryService(IMapper mapper, IRepository<DAL.Models.GameHistory> gameHistoryRepository)
        {
            _mapper = mapper;
            _gameHistoryRepository = gameHistoryRepository;
        }

        public async Task<IEnumerable<GameHistoryResponse>> GetAllGameHistories(GameHistoryRequest gameHistoryRequest)
        {
            // Using EntityFramework (3 seconds)
            //var gameHistoryList = await _gameHistoryRepository.GetAllAsync();
            //return gameHistoryList.Reverse().Select(_mapper.Map<GameHistoryResponse>).ToList();

            // Using dapper (200 ms)
            using var connection = new SqlConnection(Environment.GetEnvironmentVariable("DB_CONNECTION_STRING"));
            var gameHistoryList = await connection.QueryAsync<DAL.Models.GameHistory>("SELECT * FROM GameHistory");
            return gameHistoryList.Reverse().Select(_mapper.Map<GameHistoryResponse>).ToList();
        }

        public async Task<TopPlayersResponse> GetTopPlayers(TopPlayersRequest topPlayersRequest)
        {
            // Using EntityFramework
            //var gameHistoryList = _gameHistoryRepository.GetAllAsync().Result;

            //var result = GetAllGameHistorySortedByDescOrderOfOccurrenceInTheTable(gameHistoryList);

            //var topPlayers = new TopPlayersResponse
            //{
            //    FirstPlacePlayer = result[0].Item1,
            //    SecondPlacePlayer = result[1].Item1,
            //    ThirdPlacePlayer = result[2].Item1,
            //    FirstPlaceNumberOfWins = result[0].Item2,
            //    SecondPlaceNumberOfWins = result[1].Item2,
            //    ThirdPlaceNumberOfWins = result[2].Item2
            //};

            //return topPlayers;

            // Using Dapper
            using var connection = new SqlConnection(Environment.GetEnvironmentVariable("DB_CONNECTION_STRING"));
            var gameHistoryList = await connection.QueryAsync<DAL.Models.GameHistory>("SELECT * FROM GameHistory");

            var result = GetAllGameHistorySortedByDescOrderOfOccurrenceInTheTable(gameHistoryList);
            var topPlayers = new TopPlayersResponse
            {
                FirstPlacePlayer = result[0].Item1,
                SecondPlacePlayer = result[1].Item1,
                ThirdPlacePlayer = result[2].Item1,
                FirstPlaceNumberOfWins = result[0].Item2,
                SecondPlaceNumberOfWins = result[1].Item2,
                ThirdPlaceNumberOfWins = result[2].Item2
            };

            return topPlayers;
        }

        private static (string, int)[] GetAllGameHistorySortedByDescOrderOfOccurrenceInTheTable(IEnumerable<DAL.Models.GameHistory> gameHistories)
        {
            var firstPlace = string.Empty;
            var secondPlace = string.Empty;
            var thirdPlace = string.Empty;

            var firstPlaceNumberOfWins = 0;
            var secondPlaceNumberOfWins = 0;
            var thirdPlaceNumberOfWins = 0;

            if (gameHistories.Select(x => x.WinnerName).Distinct().Count() == 1)
            {
                firstPlace = gameHistories.GroupBy(x => x.WinnerName).OrderByDescending(g => g.Count()).ToArray()[0].Key;
                firstPlaceNumberOfWins = gameHistories.Where(x => x.WinnerName == firstPlace).Count();
            }

            else if (gameHistories.Select(x => x.WinnerName).Distinct().Count() == 2)
            {
                firstPlace = gameHistories.GroupBy(x => x.WinnerName).OrderByDescending(g => g.Count()).ToArray()[0].Key;
                secondPlace = gameHistories.GroupBy(x => x.WinnerName).OrderByDescending(g => g.Count()).ToArray()[1].Key;
                firstPlaceNumberOfWins = gameHistories.Where(x => x.WinnerName == firstPlace).Count();
                secondPlaceNumberOfWins = gameHistories.Where(x => x.WinnerName == secondPlace).Count();
            }

            else
            {
                firstPlace = gameHistories.GroupBy(x => x.WinnerName).OrderByDescending(g => g.Count()).ToArray()[0].Key;
                secondPlace = gameHistories.GroupBy(x => x.WinnerName).OrderByDescending(g => g.Count()).ToArray()[1].Key;
                thirdPlace = gameHistories.GroupBy(x => x.WinnerName).OrderByDescending(g => g.Count()).ToArray()[2].Key;

                firstPlaceNumberOfWins = gameHistories.Where(x => x.WinnerName == firstPlace).Count();
                secondPlaceNumberOfWins = gameHistories.Where(x => x.WinnerName == secondPlace).Count();
                thirdPlaceNumberOfWins = gameHistories.Where(x => x.WinnerName == thirdPlace).Count();
            }

            var arrayResult = new (string, int)[]
            {
                (firstPlace, firstPlaceNumberOfWins),
                (secondPlace, secondPlaceNumberOfWins),
                (thirdPlace, thirdPlaceNumberOfWins)
            };

            return arrayResult;
        }
    }
}