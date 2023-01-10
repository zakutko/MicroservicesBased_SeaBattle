using AutoMapper;
using Dapper;
using Game.BLL.Interfaces;
using Game.DAL.Models;
using SeaBattle.Constants;
using SeaBattle.Constants.GameServiceConstants;
using SeaBattle.Contracts.Dtos;
using System.Data;
using System.Data.SqlClient;

namespace Game.BLL.Services
{
    public class GameService : IGameService
    {
        private readonly IGameServiceHelper _gameServiceHelper;
        private readonly IMapper _mapper;

        public GameService(
            IGameServiceHelper gameServiceHelper, 
            IMapper mapper)
        {
            _gameServiceHelper = gameServiceHelper;
            _mapper = mapper;
        }

        // Should add mapper!
        public async Task<List<GameListResponse>> GetAllGames(GameListRequest gameListRequest)
        {
            using var connection = new SqlConnection(DBConnectionConstants.DBConnectionString);
            var playerGameList = await connection.QueryAsync<PlayerGame>(GameService_Queries.queryGetAllPlayerGames);

            var playerGameResponseList = new List<GameListResponse>();

            foreach (var playerGame in playerGameList)
            {
                var game = await connection.QueryFirstOrDefaultAsync<DAL.Models.Game>(GameService_Queries.queryGetGameById, new { gameId = playerGame.GameId });
                var firstPlayer = await connection.QueryFirstOrDefaultAsync<AppUser>(GameService_Queries.queryGetPlayerByPlayerId, new { playerId = playerGame.FirstPlayerId });
                var secondPlayer = await connection.QueryFirstOrDefaultAsync<AppUser>(GameService_Queries.queryGetPlayerByPlayerId, new { playerId = playerGame.SecondPlayerId });
                var gameState = await connection.QueryFirstOrDefaultAsync<GameState>(GameService_Queries.queryGetGameStateById, new { gameStateId = game.GameStateId });

                var numberOfPlayers = 2;
                if (secondPlayer == null)
                {
                    numberOfPlayers = 1;
                }

                playerGameResponseList.Add(new GameListResponse
                {
                    Id = game.Id,
                    FirstPlayer = firstPlayer.UserName,
                    SecondPlayer = secondPlayer?.UserName,
                    GameState = gameState.GameStateName,
                    NumberOfPlayers = numberOfPlayers
                });
            }

            var username = _gameServiceHelper.GetUsernameByDecodingJwtToken(gameListRequest.Token);

            var playerGameResponseListWithoutCurrUser = playerGameResponseList.Where(playerGame => playerGame.FirstPlayer != username && playerGame.SecondPlayer != username).ToList();
            return playerGameResponseListWithoutCurrUser;
        }

        public async Task CreateGame(CreateGameRequest createGameRequest)
        {
            using var connection = new SqlConnection(DBConnectionConstants.DBConnectionString);
            var username = _gameServiceHelper.GetUsernameByDecodingJwtToken(createGameRequest.Token);

            var player = await connection.QueryFirstOrDefaultAsync<AppUser>(GameService_Queries.queryGetPlayerByUsername, new { username = username });
            await connection.ExecuteAsync("gameCreationProcedure", new { playerId = player.Id }, commandType: CommandType.StoredProcedure);

            var field = await connection.QueryFirstOrDefaultAsync<Field>(GameService_Queries.queryGetFieldByPlayerId, new { playerId = player.Id });
            var shipWrappers = await connection.QueryAsync<ShipWrapper>(GameService_Queries.queryGetShipWrappersByFieldIdWhereShipIdIsNotNull, new { fieldId = field.Id });

            if (!shipWrappers.Any())
            {
                var defaultCells = _gameServiceHelper.SetDafaultCells();
                var defaultCellIds = new List<int>();

                foreach (var cell in defaultCells)
                {
                    defaultCellIds.Add(await connection.QuerySingleAsync<int>(GameService_Queries.queryInsertIntoCellWithOutput, new
                    {
                        x = cell.X,
                        y = cell.Y,
                        cellStateId = cell.CellStateId
                    }));
                }
                var defaultShipWrapperId = await connection.QuerySingleAsync<int>(GameService_Queries.queryInsertDefaultShipWrapper, new { fieldId = field.Id });
                var defaultPositions = defaultCellIds.Select(cellId => new Position { ShipWrapperId = defaultShipWrapperId, CellId = cellId }).ToList();

                foreach (var position in defaultPositions)
                {
                    await connection.ExecuteAsync(GameService_Queries.queryInsertIntoPosition, new { shipWrapperId = position.ShipWrapperId, cellId = position.CellId });
                }
            }
        }

        public async Task<IsGameOwnerResponse> IsGameOwner(IsGameOwnerRequest isGameOwnerRequest)
        {
            using var connection = new SqlConnection(DBConnectionConstants.DBConnectionString);
            var username = _gameServiceHelper.GetUsernameByDecodingJwtToken(isGameOwnerRequest.Token);
            var player = await connection.QueryFirstOrDefaultAsync<AppUser>(GameService_Queries.queryGetPlayerByUsername, new { username = username });
            var playerGame = await connection.QueryFirstOrDefaultAsync<PlayerGame>(GameService_Queries.queryGetPlayerGameByPlayerId, new { playerId = player.Id });

            if (playerGame == null)
            {
                return new IsGameOwnerResponse
                {
                    IsGameOwner = false,
                    IsSecondPlayerConnected = false
                };
            }

            var isGameOwner = false;
            if (playerGame.FirstPlayerId == player.Id)
            {
                isGameOwner = true;
            }

            var isSecondPlayerConnected = true;
            if (playerGame.FirstPlayerId == player.Id && playerGame.SecondPlayerId == null)
            {
                isSecondPlayerConnected = false;
            }

            return new IsGameOwnerResponse
            {
                IsGameOwner = isGameOwner,
                IsSecondPlayerConnected = isSecondPlayerConnected
            };
        }

        public async Task<DeleteGameResponse> DeleteGame(DeleteGameRequest deleteGameRequest)
        {
            try
            {
                using var connection = new SqlConnection(DBConnectionConstants.DBConnectionString);
                var username = _gameServiceHelper.GetUsernameByDecodingJwtToken(deleteGameRequest.Token);

                var firstPlayer = await connection.QueryFirstOrDefaultAsync<AppUser>(GameService_Queries.queryGetPlayerByUsername, new { username = username });
                var playerGame = await connection.QueryFirstOrDefaultAsync<PlayerGame>(GameService_Queries.queryGetPlayerGameByPlayerId, new { playerId = firstPlayer.Id });
                var field = await connection.QueryFirstOrDefaultAsync<Field>(GameService_Queries.queryGetFieldByPlayerId, new { playerId = firstPlayer.Id });

                var result = await connection.ExecuteAsync("deleteGameAndAllData",
                    new { fieldId = field.Id, playerId = firstPlayer.Id, gameId = playerGame.GameId }, commandType: CommandType.StoredProcedure);

                return new DeleteGameResponse { Message = $"Delete game was successful! Affected rows: {result}" };
            }
            catch(Exception ex)
            {
                return new DeleteGameResponse { Message = ex.Message };
            }
        }

        public async Task JoinSecondPlayer(JoinSecondPlayerRequest joinSecondPlayerRequest)
        {
            using var connection = new SqlConnection(DBConnectionConstants.DBConnectionString);
            var username = _gameServiceHelper.GetUsernameByDecodingJwtToken(joinSecondPlayerRequest.Token);
            var player = await connection.QueryFirstOrDefaultAsync<AppUser>(GameService_Queries.queryGetPlayerByUsername, new { username = username });
            var playerGame = await connection.QueryFirstOrDefaultAsync<PlayerGame>(GameService_Queries.queryGetPlayerGameByGameId, new { gameId = joinSecondPlayerRequest.GameId });
            var gameField = await connection.QueryFirstOrDefaultAsync<GameField>(GameService_Queries.queryGetGameFieldByGameId, new { gameId = joinSecondPlayerRequest.GameId });

            await connection.ExecuteAsync("joinSecondPlayer",
                new
                {
                    playerId = player.Id,
                    gameId = playerGame.GameId
                }, commandType: CommandType.StoredProcedure);

            var field = await connection.QueryFirstOrDefaultAsync<Field>(GameService_Queries.queryGetFieldByPlayerId, new { playerId = player.Id });

            var numberOfShipsOnTheField = await connection.QueryFirstOrDefaultAsync<int>(GameService_Queries.queryGetNumberOfShipsOnTheField, new { fieldId = field.Id });
            if (numberOfShipsOnTheField == 0)
            {
                var defaultCells = _gameServiceHelper.SetDafaultCells();
                var defaultCellIds = new List<int>();

                foreach (var cell in defaultCells)
                {
                    defaultCellIds.Add(await connection.QuerySingleAsync<int>(GameService_Queries.queryInsertIntoCellWithOutput, new { x = cell.X, y = cell.Y, 
                        cellStateId = cell.CellStateId }));
                }

                var defaultShipWrapperId = await connection.QuerySingleAsync<int>(GameService_Queries.queryInsertDefaultShipWrapper, new { fieldId = field.Id });
                var defaultPositions = defaultCellIds.Select(cellId => new Position { ShipWrapperId = defaultShipWrapperId, CellId = cellId }).ToList();

                foreach (var position in defaultPositions)
                {
                    await connection.ExecuteAsync(GameService_Queries.queryInsertIntoPosition, new { shipWrapperId = position.ShipWrapperId, cellId = position.CellId });
                }
            }
        }

        public async Task<IEnumerable<CellListResponse>> GetAllCells(CellListRequest cellListRequest)
        {
            using var connection = new SqlConnection(DBConnectionConstants.DBConnectionString);
            var username = _gameServiceHelper.GetUsernameByDecodingJwtToken(cellListRequest.Token);
            var player = await connection.QueryFirstOrDefaultAsync<AppUser>(GameService_Queries.queryGetPlayerByUsername, new { username = username });
            var field = await connection.QueryFirstOrDefaultAsync<Field>(GameService_Queries.queryGetFieldByPlayerId, new { playerId = player.Id });
            var cellList = await connection.QueryAsync<Cell>(GameService_Queries.queryGetCellListByFieldId, new { fieldId = field.Id });
            return cellList.OrderBy(x => x.Id).Select(_mapper.Map<CellListResponse>);
        }

        public async Task<CreateShipResponse> CreateShipOnField(CreateShipRequest createShipRequest)
        {
            using var connection = new SqlConnection(DBConnectionConstants.DBConnectionString);
            await connection.OpenAsync();

            var username = _gameServiceHelper.GetUsernameByDecodingJwtToken(createShipRequest.Token);
            var player = await connection.QueryFirstOrDefaultAsync<AppUser>(GameService_Queries.queryGetPlayerByUsername, new { username = username });
            var field = await connection.QueryFirstOrDefaultAsync<Field>(GameService_Queries.queryGetFieldByPlayerId, new { playerId = player.Id });

            var shipWrappersList = await connection.QueryAsync<ShipWrapper>("SELECT * FROM ShipWrapper WHERE fieldId = @fieldId AND shipId != NULL", param: new { fieldId = field.Id });

            if (shipWrappersList.Count() == 10)
            {
                return new CreateShipResponse { Message = "There are already 10 ships on the field!" };
            }

            var ships = await connection.QueryAsync<Ship>("SELECT * FROM Ship WHERE Id IN (SELECT shipId FROM ShipWrapper WHERE fieldId = @fieldId)", param: new { fieldId = field.Id });

            switch (createShipRequest.ShipSize)
            {
                case 1:
                    var numberOfShipsWhereSizeOne = ships.Where(x => x.ShipSizeId == 1).Count();
                    if (numberOfShipsWhereSizeOne == 4)
                    {
                        return new CreateShipResponse { Message = "The maximum number of ships with the size 1 on the field is 4!" };
                    }
                    break;
                case 2:
                    var numberOfShipsWhereSizeTwo = ships.Where(x => x.ShipSizeId == 2).Count();
                    if (numberOfShipsWhereSizeTwo == 3)
                    {
                        return new CreateShipResponse { Message = "The maximum number of ships with the size 2 on the field is 3!" };
                    }
                    break;
                case 3:
                    var numberOfShipsWhereSizeThree = ships.Where(x => x.ShipSizeId == 3).Count();
                    if (numberOfShipsWhereSizeThree == 2)
                    {
                        return new CreateShipResponse { Message = "The maximum number of ships with the size 3 on the field is 2!" };
                    }
                    break;
                case 4:
                    var numberOfShipsWhereSizeFour = ships.Where(x => x.ShipSizeId == 4).Count();
                    if (numberOfShipsWhereSizeFour == 1)
                    {
                        return new CreateShipResponse { Message = "The maximum number of ships with the size 4 on the field is 1!" };
                    }
                    break;
            }

            using var transaction = connection.BeginTransaction();
            var cmd = connection.CreateCommand();
            cmd.Transaction = transaction;

            var newShipId = await connection.QueryAsync<int>("INSERT INTO Ship OUTPUT INSERTED.[Id] VALUES(@shipDirection, @shipStateId, @shipSizeId)",
                param: new { shipDirection = createShipRequest.ShipDirection, shipStateId = 1, shipSizeId = createShipRequest.ShipSize }, transaction);
            var shipWrapperId = await connection.QueryAsync<int>("INSERT INTO ShipWrapper OUTPUT INSERTED.[Id] VALUES(@shipId, @fieldId)",
                param: new { shipId = newShipId.FirstOrDefault(), fieldId = field.Id }, transaction);

            var direction = await connection.QueryFirstOrDefaultAsync<Direction>(GameService_Queries.queryGetDirectionById, new { shipDirectionId = createShipRequest.ShipDirection }, transaction);
            try
            {
                var cellListResult = _gameServiceHelper.GetAllCells(direction.DirectionName, createShipRequest.ShipSize, createShipRequest.X, createShipRequest.Y, field.Id).Result;
                if (!cellListResult.Any())
                {
                    await cmd.Transaction.RollbackAsync();
                }

                var cells = await connection.QueryAsync<Cell>("SELECT * FROM Cell WHERE Id IN (SELECT cellId FROM Position WHERE shipWrapperId IN " +
                                                             "(SELECT Id FROM ShipWrapper WHERE fieldId = @fieldId))",
                    param: new { fieldId = field.Id }, transaction);

                foreach (var cell in cellListResult)
                {
                    var defaultCell = cells.Where(x => x.X == cell.X && x.Y == cell.Y).FirstOrDefault();

                    if (defaultCell.CellStateId == 2)
                    {
                       throw new Exception("One of Cells is busy!");
                    }
                    else if(defaultCell.CellStateId == 5)
                    {
                        continue;
                    }
                    else
                    {
                        await connection.ExecuteAsync("UPDATE Cell SET cellStateId = @cellStateId WHERE Id = @Id",
                            param: new { cellStateId = cell.CellStateId, Id = defaultCell.Id }, transaction);


                        await connection.ExecuteAsync("UPDATE Position SET shipWrapperId = @shipWrapperId, cellId = @cellId WHERE cellId = @cellId", 
                            param: new { shipWrapperId = shipWrapperId.FirstOrDefault(), cellId = defaultCell.Id}, transaction);
                    }
                }
            }
            catch (Exception ex)
            {
                await cmd.Transaction.RollbackAsync();
                return new CreateShipResponse { Message = ex.Message };
            }

            await cmd.Transaction.CommitAsync();
            await connection.CloseAsync();
            return new CreateShipResponse { Message = "Create ship was successful!" };
        }
        
        public async Task<IsPlayerReadyResponse> SetPlayerReady(IsPlayerReadyRequest isPlayerReadyRequest)
        {
            using var connection = new SqlConnection(DBConnectionConstants.DBConnectionString);
            var username = _gameServiceHelper.GetUsernameByDecodingJwtToken(isPlayerReadyRequest.Token);
            var player = await connection.QueryFirstOrDefaultAsync<AppUser>(GameService_Queries.queryGetPlayerByUsername, new { username = username });

            var field = await connection.QueryFirstOrDefaultAsync<Field>(GameService_Queries.queryGetFieldByPlayerId, new { playerId = player.Id });
            var shipWrappers = await connection.QueryAsync<ShipWrapper>(GameService_Queries.queryGetShipWrappersByFieldId, new { fieldId = field.Id });

            if (shipWrappers.Count() < 11)
            {
                return new IsPlayerReadyResponse { Message = "Number of ships must be 10!" };
            }

            var playerGame = await connection.QueryFirstOrDefaultAsync<PlayerGame>(GameService_Queries.queryGetPlayerGameByPlayerId, new { playerId = player.Id });

            if (playerGame.IsReadyFirstPlayer != null)
            {
                await connection.ExecuteAsync(GameService_Queries.queryUpdatePlayerGameById, 
                    new
                    {
                        gameId = playerGame.GameId,
                        firstPlayerId = playerGame.FirstPlayerId,
                        secondPlayerId = playerGame.SecondPlayerId,
                        isReadyFirstPlayer = true,
                        isReadySecondPlayer = true,
                        playerGameId = playerGame.Id
                    });

                return new IsPlayerReadyResponse { Message = "The Player is ready!" };
            }
            else
            {
                await connection.ExecuteAsync(GameService_Queries.queryUpdatePlayerGameById,
                    new
                    {
                        gameId = playerGame.GameId,
                        firstPlayerId = playerGame.FirstPlayerId,
                        secondPlayerId = playerGame.SecondPlayerId,
                        isReadyFirstPlayer = true,
                        isReadySecondPlayer = playerGame.IsReadySecondPlayer,
                        playerGameId = playerGame.Id
                    });

                return new IsPlayerReadyResponse { Message = "The Player is ready!" };
            }
        }
        
        public async Task<IsTwoPlayersReadyResponse> IsTwoPlayersReady(IsTwoPlayersReadyRequest isTwoPlayersReadyRequest)
        {
            using var connection = new SqlConnection(DBConnectionConstants.DBConnectionString);
            var username = _gameServiceHelper.GetUsernameByDecodingJwtToken(isTwoPlayersReadyRequest.Token);
            var player = await connection.QueryFirstOrDefaultAsync<AppUser>(GameService_Queries.queryGetPlayerByUsername, new { username = username });

            var playerGame = await connection.QueryFirstOrDefaultAsync<PlayerGame>(GameService_Queries.queryGetPlayerGameByPlayerId, new { playerId = player.Id });

            var numberOfReadyPlayers = 0;
            if (playerGame.IsReadyFirstPlayer == null && playerGame.IsReadySecondPlayer == null)
            {
                numberOfReadyPlayers = 0;
            }
            else if (playerGame.IsReadyFirstPlayer != null && playerGame.IsReadySecondPlayer == null)
            {
                numberOfReadyPlayers = 1;
            }
            else
            {
                numberOfReadyPlayers = 2;
            }
            return new IsTwoPlayersReadyResponse
            {
                NumberOfReadyPlayers = numberOfReadyPlayers
            };
        }
        
        public async Task<IEnumerable<CellListResponseForSecondPlayer>> GetAllCellForSecondPlayer(CellListRequestForSecondPlayer cellListRequestForSecondPlayer)
        {
            using var connection = new SqlConnection(DBConnectionConstants.DBConnectionString);
            var username = _gameServiceHelper.GetUsernameByDecodingJwtToken(cellListRequestForSecondPlayer.Token);
            var player = await connection.QueryFirstOrDefaultAsync<AppUser>(GameService_Queries.queryGetPlayerByUsername, new { username = username });

            var secondPlayerId = await _gameServiceHelper.GetSecondPlayerId(player.Id);

            if (secondPlayerId == null)
            {
                return Enumerable.Empty<CellListResponseForSecondPlayer>();
            }

            var field = await connection.QueryFirstOrDefaultAsync<Field>(GameService_Queries.queryGetFieldByPlayerId, new { playerId = secondPlayerId });
            var cellList = await connection.QueryAsync<Cell>(GameService_Queries.queryGetCellListByFieldId, new { fieldId = field.Id });

            return cellList.Select(_mapper.Map<CellListResponseForSecondPlayer>);
        }
        
        public async Task<ShootResponse> Fire(ShootRequest shootRequest)
        {
            using var connection = new SqlConnection(DBConnectionConstants.DBConnectionString);
            var username = _gameServiceHelper.GetUsernameByDecodingJwtToken(shootRequest.Token);
            var player = await connection.QueryFirstOrDefaultAsync<AppUser>(GameService_Queries.queryGetPlayerByUsername, new { username = username });
            var secondPlayerId = await _gameServiceHelper.GetSecondPlayerId(player.Id);

            var field = await connection.QueryFirstOrDefaultAsync<Field>(GameService_Queries.queryGetFieldByPlayerId, new { playerId = secondPlayerId });
            var cellList = await connection.QueryAsync<Cell>(GameService_Queries.queryGetCellListByFieldId, new { fieldId = field.Id });
            var myCell = cellList.Where(x => x.X == shootRequest.X && x.Y == shootRequest.Y).FirstOrDefault();

            if (myCell.CellStateId == 1 || myCell.CellStateId == 5)
            {
                var newCell = _gameServiceHelper.CreateNewCell(myCell.Id, myCell.X, myCell.Y, myCell.CellStateId, false);
                await connection.ExecuteAsync("missedTheFire", 
                    new { cellStateId = newCell.CellStateId, cellId = newCell.Id, firstPlayerId = player.Id, secondPlayerId = secondPlayerId }, commandType: CommandType.StoredProcedure);

                return new ShootResponse { Message = "Missed the fire!" };
            }
            else
            {
                var cells = await connection.QueryAsync<Cell>(GameService_Queries.queryGetCellListByCellId, new { cellId = myCell.Id });

                var isDestroyed = cells.Count(x => x.CellStateId == 2) <= 1;

                var newCell = _gameServiceHelper.CreateNewCell(myCell.Id, myCell.X, myCell.Y, myCell.CellStateId, false);
                await connection.ExecuteAsync(GameService_Queries.queryUpdateCellStateById, param: new { cellStateId = newCell.CellStateId, Id = newCell.Id });

                if (isDestroyed)
                {
                    foreach (var cellByCellId in cells)
                    {
                        var newCellByCellId = _gameServiceHelper.CreateNewCell(cellByCellId.Id, cellByCellId.X, cellByCellId.Y, cellByCellId.CellStateId, isDestroyed);
                        await connection.ExecuteAsync(GameService_Queries.queryUpdateCellStateById, param: new { cellStateId = newCellByCellId.CellStateId, Id = newCellByCellId.Id });
                    }

                    return new ShootResponse { Message = "The ship is destroyed!" };
                }
                return new ShootResponse { Message = "The ship is hit!" };
            }
        }
        
        public async Task<HitResponse> GetPriority(HitRequest hitRequest)
        {
            using var connection = new SqlConnection(DBConnectionConstants.DBConnectionString);
            var username = _gameServiceHelper.GetUsernameByDecodingJwtToken(hitRequest.Token);
            var player = await connection.QueryFirstOrDefaultAsync<AppUser>(GameService_Queries.queryGetPlayerByUsername, new { username = username });

            return new HitResponse { IsHit = player.IsHit };
        }

        public async Task<IsEndOfTheGameResponse> IsEndOfTheGame(IsEndOfTheGameRequest isEndOfTheGameRequest)
        {
            using var connection = new SqlConnection(DBConnectionConstants.DBConnectionString);
            var username = _gameServiceHelper.GetUsernameByDecodingJwtToken(isEndOfTheGameRequest.Token);
            var firstPlayer = await connection.QueryFirstOrDefaultAsync<AppUser>(GameService_Queries.queryGetPlayerByUsername, new { username = username });
            var secondPlayerId = await _gameServiceHelper.GetSecondPlayerId(firstPlayer.Id);
            var secondPlayer = await connection.QueryFirstOrDefaultAsync<AppUser>(GameService_Queries.queryGetUserNameById, new { playerId = secondPlayerId });
            var playerGame = await connection.QueryFirstOrDefaultAsync<PlayerGame>(GameService_Queries.queryGetPlayerGameByPlayerId, new { playerId = firstPlayer.Id });

            if (secondPlayer == null || secondPlayer != null && playerGame.IsReadySecondPlayer == null)
            {
                return new IsEndOfTheGameResponse { IsEndOfTheGame = false, WinnerUserName = "" };
            }
            else if (playerGame.IsReadyFirstPlayer != null && playerGame.IsReadySecondPlayer != null)
            {
                var firstField = await connection.QueryFirstOrDefaultAsync<Field>(GameService_Queries.queryGetFieldByPlayerId, new { playerId = firstPlayer.Id }); 
                var firstCellList = await connection.QueryAsync<Cell>(GameService_Queries.queryGetCellListByFieldId, new { fieldId = firstField.Id });

                var secondField = await connection.QueryFirstOrDefaultAsync<Field>(GameService_Queries.queryGetFieldByPlayerId, new { playerId = secondPlayerId });
                var secondCellList = await connection.QueryAsync<Cell>(GameService_Queries.queryGetCellListByFieldId, new { fieldId = secondField.Id });

                var game = await connection.QueryFirstOrDefaultAsync<DAL.Models.Game>(GameService_Queries.queryGetGameById, new { gameId = playerGame.GameId });

                var firstCellsWithStateBusyOrHit = _gameServiceHelper.CheckIsCellsWithStateBusyOrHit(firstCellList, game.GameStateId);
                var secondCellsWithStateBusyOrHit = _gameServiceHelper.CheckIsCellsWithStateBusyOrHit(secondCellList, game.GameStateId);

                var gameHistory = await connection.QueryFirstOrDefaultAsync<GameHistory>(GameService_Queries.queryGetGameHistoryByGameId, new { gameId = game.Id });

                if (secondCellsWithStateBusyOrHit && firstCellsWithStateBusyOrHit && game.GameStateId != 3)
                {
                    return new IsEndOfTheGameResponse { IsEndOfTheGame = false, WinnerUserName = "" };
                }
                else if (!firstCellsWithStateBusyOrHit || !secondCellsWithStateBusyOrHit)
                {
                    await connection.ExecuteAsync(GameService_Queries.queryUpdateGameState, new { gameId = game.Id });

                    if (!firstCellsWithStateBusyOrHit)
                    {
                        if (gameHistory == null)
                        {
                            await connection.ExecuteAsync(GameService_Queries.queryInsertValuesToGameHistoryTable,
                                new
                                {
                                    gameId = game.Id,
                                    firstPlayerName = firstPlayer.UserName,
                                    secondPlayerName = secondPlayer.UserName,
                                    gameStateName = "Finished",
                                    winnerName = secondPlayer.UserName
                                });
                        }
                        return new IsEndOfTheGameResponse { IsEndOfTheGame = true, WinnerUserName = secondPlayer.UserName };
                    }
                    else
                    {
                        if (gameHistory == null)
                        {
                            await connection.ExecuteAsync(GameService_Queries.queryInsertValuesToGameHistoryTable,
                                new
                                {
                                    gameId = game.Id,
                                    firstPlayerName = firstPlayer.UserName,
                                    secondPlayerName = secondPlayer.UserName,
                                    gameStateName = "Finished",
                                    winnerName = firstPlayer.UserName
                                });
                        }

                        return new IsEndOfTheGameResponse { IsEndOfTheGame = true, WinnerUserName = firstPlayer.UserName };
                    }
                }
            }
            return new IsEndOfTheGameResponse { IsEndOfTheGame = false, WinnerUserName = "" };
        }

        public async Task<ClearingDBResponse> ClearingDB(ClearingDBRequest clearingDBRequest)
        {
            using var connection = new SqlConnection(DBConnectionConstants.DBConnectionString);
            var username = _gameServiceHelper.GetUsernameByDecodingJwtToken(clearingDBRequest.Token);

            var firstPlayer = await connection.QueryFirstOrDefaultAsync<AppUser>(GameService_Queries.queryGetPlayerByUsername, new { username = username });
            var secondPlayerId = await _gameServiceHelper.GetSecondPlayerId(firstPlayer.Id);

            var playerGame = await connection.QueryFirstOrDefaultAsync<PlayerGame>(GameService_Queries.queryGetPlayerGameTwoByPlayerIds,
                new { firstPlayerId = firstPlayer.Id, secondPlayerId = secondPlayerId });

            var firstField = await connection.QueryFirstOrDefaultAsync<Field>(GameService_Queries.queryGetFieldByPlayerId, new { playerId = firstPlayer.Id });
            var firstCellList = await connection.QueryAsync<Cell>(GameService_Queries.queryGetCellListByFieldId, param: new { fieldId = firstField.Id });

            var secondField = await connection.QueryFirstOrDefaultAsync<Field>(GameService_Queries.queryGetFieldByPlayerId, new { playerId = secondPlayerId });
            var secondCellList = await connection.QueryAsync<Cell>(GameService_Queries.queryGetCellListByFieldId, param: new { fieldId = secondField.Id });

            if (secondCellList.Any())
            {
                var result = await connection.ExecuteAsync("clearingDB_first_step", 
                    new { fieldId = firstField.Id, firstPlayerId = firstPlayer.Id, secondPlayerId = secondPlayerId }, commandType: CommandType.StoredProcedure);

                return new ClearingDBResponse { Message = $"The first step of clearing DB was successful! Affected rows = {result}" };
            }
            else
            {
                var result = await connection.ExecuteAsync("clearingDB_second_step", 
                    new { fieldId = firstField.Id, gameId = playerGame.GameId, secondFieldId = secondField.Id }, commandType: CommandType.StoredProcedure);

                return new ClearingDBResponse { Message = $"The second step of clearing DB was successful! Affected rows = {result}" };
            }
        }
    }
}