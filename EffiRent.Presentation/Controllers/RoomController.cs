// EffiRent.Presentation/Controllers/RoomController.cs
using EffiRent.Application.Commands.ContractCommand;
using EffiRent.Application.Commands.RoomCommand;
using EffiRent.Application.Queries.IQueries;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace EffiRent.Presentation.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class RoomController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly IRoomQuery _roomQuery;

        public RoomController(IMediator mediator, IRoomQuery roomQuery)
        {
            _mediator = mediator;
            _roomQuery = roomQuery;
        }

        [HttpPost]
        public async Task<IActionResult> CreateRoom([FromBody] CreateRoomCommand command)
        {
            var roomId = await _mediator.Send(command);
            return Ok(new { RoomId = roomId });
        }

        [HttpGet("branch/{branchId}")]
        public async Task<IActionResult> GetRoomsByBranch(Guid branchId)
        {
            var rooms = await _roomQuery.GetRoomsByBranchAsync(branchId);
            return Ok(rooms);
        }

        
    }
}