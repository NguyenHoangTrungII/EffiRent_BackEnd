// EffiRent.Presentation/Controllers/ContractController.cs
using EffiRent.Application.Commands.ContractCommand;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace EffiRent.Presentation.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ContractController : ControllerBase
    {
        private readonly IMediator _mediator;

        public ContractController(IMediator mediator)
        {
            _mediator = mediator;
        }

        [HttpPost]
        public async Task<IActionResult> CreateContract([FromBody] CreateContractCommand command)
        {
            var contractId = await _mediator.Send(command);
            return Ok(new { ContractId = contractId });
        }

        [HttpPost("extend")]
        public async Task<IActionResult> ExtendContract([FromBody] ExtendContractCommand command)
        {
            var contractId = await _mediator.Send(command);
            return Ok(new { ContractId = contractId });
        }

        [HttpPost("transfer")]
        public async Task<IActionResult> TransferRoom([FromBody] TransferRoomCommand command)
        {
            var newContractId = await _mediator.Send(command);
            return Ok(new { NewContractId = newContractId });
        }

    }
}