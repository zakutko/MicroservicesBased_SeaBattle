namespace SeaBattle.Constants.GameServiceConstants
{
    public static class GameService_Queries
    {
        //Cell
        public const string queryInsertIntoCellWithOutput = "INSERT INTO Cell OUTPUT Inserted.Id VALUES (@x, @y, @cellStateId)";
        public const string queryUpdateCellStateById = "UPDATE Cell SET cellStateId = @cellStateId WHERE Id = @Id";
        public const string queryGetCellListByCellId = "SELECT * FROM Cell WHERE Id IN (SELECT cellId FROM Position WHERE shipWrapperId IN (SELECT shipWrapperId FROM Position WHERE cellId = @cellId))";
        public const string queryGetCellListByFieldId = "SELECT * FROM Cell WHERE Id IN (SELECT cellId FROM Position WHERE shipWrapperId IN (SELECT Id FROM ShipWrapper WHERE fieldId = @fieldId))";
        //AspNetUsers
        public const string queryGetPlayerByUsername = "SELECT * FROM AspNetUsers WHERE UserName = @username";
        public const string queryGetUserNameById = "SELECT UserName FROM AspNetUsers WHERE Id = @playerId";
        public const string queryGetPlayerByPlayerId = "SELECT * FROM AspNetUsers WHERE Id = @playerId";
        //Field
        public const string queryGetFieldByPlayerId = "SELECT * FROM Field WHERE appUserId = @playerId";
        //PlayerGame
        public const string queryGetAllPlayerGames = "SELECT * FROM PlayerGame";
        public const string queryGetPlayerGameTwoByPlayerIds = "SELECT * FROM PlayerGame WHERE (firstPlayerId = @firstPlayerId AND secondPlayerId = @secondPlayerId) OR " +
            "(firstPlayerId = @secondPlayerId AND secondPlayerId = @firstPlayerId)";
        public const string queryGetPlayerGameByPlayerId = "SELECT * FROM PlayerGame WHERE firstPlayerId = @playerId OR secondPlayerId = @playerId";
        public const string queryUpdatePlayerGameById = "UPDATE PlayerGame SET gameId = @gameId, firstPlayerId = @firstPlayerId, secondPlayerId = @secondPlayerId, " +
            "isReadyFirstPlayer = @isReadyFirstPlayer, isReadySecondPlayer = @isReadySecondPlayer WHERE Id = @playerGameId";
        public const string queryGetPlayerGameByGameId = "SELECT * FROM PlayerGame WHERE gameId = @gameId";
        //ShipWrapper
        public const string queryGetShipWrappersByFieldIdWhereShipIdIsNotNull = "SELECT * FROM ShipWrapper WHERE fieldId = @fieldId AND shipId != NULL";
        public const string queryGetShipWrappersByFieldId = "SELECT * FROM ShipWrapper WHERE fieldId = @fieldId";
        public const string queryGetNumberOfShipsOnTheField = "SELECT COUNT(*) as count_ships FROM ShipWrapper WHERE fieldId = @fieldId AND shipId IS NOT NULL";
        public const string queryInsertDefaultShipWrapper = "INSERT INTO ShipWrapper OUTPUT Inserted.Id VALUES (NULL, @fieldId)";
        //Game
        public const string queryGetGameById = "SELECT * FROM Game WHERE Id = @gameId";
        public const string queryUpdateGameState = "UPDATE Game SET gameStateId = 3 WHERE Id = @gameId";
        //GameState
        public const string queryGetGameStateNameByGameStateId = "SELECT gameStateName FROM GameState WHERE Id = @gameStateId";
        public const string queryGetGameStateById = "SELECT * FROM GameState WHERE Id = @gameStateId";
        //GameHistory
        public const string queryInsertValuesToGameHistoryTable = "INSERT INTO GameHistory VALUES (@gameId, @firstPlayerName, @secondPlayerName, @gameStateName, @winnerName)";
        public const string queryGetGameHistoryByGameId = "SELECT * FROM GameHistory WHERE gameId = @gameId";
        //GameField
        public const string queryGetGameFieldByGameId = "SELECT * FROM GameField WHERE gameId = @gameId";
        //Position
        public const string queryInsertIntoPosition = "INSERT INTO Position VALUES (@shipWrapperId, (SELECT Id FROM Cell WHERE Id = @cellId))";
        //Direction
        public const string queryGetDirectionById = "SELECT * FROM Direction WHERE Id = @shipDirectionId";
    }
}
