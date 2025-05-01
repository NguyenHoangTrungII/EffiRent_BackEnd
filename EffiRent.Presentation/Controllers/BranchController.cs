using EffiAP.Application.Commands.BranchCommand;
using EffiAP.Application.Queries.IQueries;
using EffiRent.Domain.Entities;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using EffiAP.Presentation.Abstractions;
using EffiAP.Application.Queries;
using EffiAP.Domain.ViewModels.Branch;
using Microsoft.AspNetCore.Authorization;

namespace EffiAP.Presentation.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class BranchController : ApiController
    {
        private readonly ISender _sender;
        private readonly IBranchQuery _branchQuery;

        public BranchController(ISender sender, IBranchQuery branchQuery) : base(sender)
        {
            _sender = sender;
            _branchQuery = branchQuery;
        }

        [HttpPost]
        public async Task<IActionResult> CreateBranch([FromBody] CreateBranchCommand command)
        {
            if (command == null)
            {
                return BadRequest("Invalid command.");
            }

            var result = await _sender.Send(command);
            return Ok(result); 
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateBranch(Guid id, [FromBody] UpdateBranchCommand command)
        {
            if (command == null)
            {
                return BadRequest("Invalid command.");
            }

            command.BranchID = id;
            await _sender.Send(command);
            return NoContent();
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "ADMIN, OWNER")]
        public async Task<IActionResult> DeleteBranch(Guid id)
        {
            var command = new DeleteBranchCommand (id);
            await _sender.Send(command);
            return NoContent();
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<Branch>> GetBranchById(Guid id)
        {
            try
            {
                var branch = await _branchQuery.GetBranchById(id);

                if (branch == null)
                {
                    return NotFound();
                }
                return Ok(branch);
            }
            catch (Exception)
            {
                return StatusCode(500, "An error occurred while processing your request.");
            }
        }

        [HttpGet]
        [Authorize(Roles = "ADMIN, OWNER")]
        public async Task<ActionResult<List<BranchViewModel>>> GetAllBranches()
        {
            try
            {
                var branches = await _branchQuery.GetAllBranches();
                return Ok(branches);
            }
            catch (Exception ex)
            {
                return StatusCode(500, "An error occurred while processing your request.");
            }
        }



    }
}
