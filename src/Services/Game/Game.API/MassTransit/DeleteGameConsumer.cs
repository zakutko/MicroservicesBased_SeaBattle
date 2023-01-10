using Game.BLL.Interfaces;
using MassTransit;
using SeaBattle.Contracts.Dtos;

namespace Game.API.MassTransit
{
    public class DeleteGameConsumer : IConsumer<DeleteGameRequest>
    {
        private readonly IGameService _gameService;

        public DeleteGameConsumer(IGameService gameService)
        {
            _gameService = gameService;
        }

        public async Task Consume(ConsumeContext<DeleteGameRequest> context)
        {
            var result = await _gameService.DeleteGame(context.Message);
            await context.RespondAsync(result);
        }
    }
}
