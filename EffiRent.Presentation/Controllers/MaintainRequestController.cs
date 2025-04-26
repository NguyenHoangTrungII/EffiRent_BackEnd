using EffiAP.Application.Commands.MaintainRequestCommand;
using EffiAP.Application.Queries.IQueries;
using EffiAP.Application.Services.Messaging;
using EffiAP.Application.Wrappers;
using EffiAP.Domain.ViewModels.MaintainRequest;
using EffiAP.Presentation.Abstractions;
using EffiRent.Application.Commands.MaintainRequestCommand;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;


namespace EffiAP.Presentation.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class MaintainRequestController : ApiController
    {
        private readonly ISender _sender;
        //private readonly IMaintenanceQueryQueries _maintenanceQueryQueries;
        private readonly IRabbitMQProducerService _rabbitmq;

        public MaintainRequestController(ISender sender, IRabbitMQProducerService rabbitmq) : base(sender)
        {
            _sender = sender;
            _rabbitmq = rabbitmq;
            //_maintenanceQueryQueries = maintenanceQueryQueries;
        }

        /// <summary>
        /// Create a new maintenance request.
        /// </summary>
        /// <param name="requestDto">Maintenance request details.</param>
        /// <param name="images">Optional list of image files.</param>
        /// <returns>ApiResponse indicating success or failure.</returns>
        [HttpPost]
        [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> CreateMaintenanceRequest(
            //[FromForm] MaintenanceRequestCommandDTO requestDto,
            [FromForm] List<IFormFile> images)
        {
            var requestDto = new MaintenanceRequestCommandDTO
            {
                //requestId = Guid.NewGuid(),
                CustomerId = "CACD3209-2F5C-46CF-B7FE-F3E344E28D4B",
                Status = "Pending",
                PriorityLevel = 1,
                //CreatedAt = DateTime.Parse("2022-09-26T08:08:57.954Z"),
                CategoryId = new Guid("FFB7B23D-28C6-494E-B197-19A1E0242537"),
                RoomId = new Guid("127A8EBB-6D96-4B16-913E-656E5F4B0DCB"),
            };
            var command = new CreateMaintenanceRequestCommand(requestDto, images);
            var response = await _sender.Send(command);

            if (response.Succeeded)
            {
                return Ok(response);
            }

            return BadRequest(response);
        }

        // Endpoint để gán kỹ thuật viên từ hàng đợi
        [HttpPost("assign-technician-from-queue")]
                [Consumes("multipart/form-data")]
        public async Task<IActionResult> AssignTechnicianFromQueue([FromForm] List<IFormFile> images)
        {
            try
            {
                //[FromForm] MaintenanceRequestCommandDTO requestDto,
                var requestDto = new MaintenanceRequestCommandDTO
                {
                    CustomerId = "CACD3209-2F5C-46CF-B7FE-F3E344E28D4B",
                    Status = "Pending",
                    PriorityLevel = 1,
                    //CreatedAt = DateTime.Parse("2022-09-26T08:08:57.954Z"),
                    CategoryId = new Guid("FFB7B23D-28C6-494E-B197-19A1E0242537"),
                    RoomId = new Guid("127A8EBB-6D96-4B16-913E-656E5F4B0DCB"),
                };

               await _rabbitmq.SendToMaintenanceQueueAsync(requestDto, images);

                return Ok(new ApiResponse<MaintenanceRequestCommandDTO>("Request was sent success"));
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"An error occurred while assigning the technician: {ex.Message}");
            }
        }

        // Endpoint để xử lý hoàn thành yêu cầu bảo trì
        [HttpPost("complete-maintenance-v1")]
        public async Task<IActionResult> CompleteMaintenanceV1([FromBody] CompleteMaintenanceRequestDTO completeRequestDto)
        {
            if (completeRequestDto == null)
            {
                return BadRequest("Invalid completion request.");
            }

            try
            {
                // Gửi command qua MediatR
                var command = new CompleteMaintenanceCommand(completeRequestDto);
                var result = await _sender.Send(command);

                if (result.Succeeded)
                {
                    return Ok(result);
                }
                else
                {
                    return BadRequest(result.Errors);
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"An error occurred while completing the maintenance: {ex.Message}");
            }
        }

        [HttpPost("complete-maintenance-by-tech")]
        public async Task<IActionResult> CompleteMaintenance([FromBody] CompleteMaintenanceRequestDTO completeRequestDto)
        {
            if (completeRequestDto == null)
            {
                return BadRequest("Invalid completion request.");
            }

            try
            {
                var command = new CompleteMaintenanceRequestByTechnicianCommand(completeRequestDto.RequestId, completeRequestDto.TechnicianId);
                var result = await _sender.Send(command);

                if (result)
                {
                    return Ok(result);
                }
                else
                {
                    return BadRequest("Something go wrong ! Try again");
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"An error occurred while completing the maintenance: {ex.Message}");
            }
        }

        // Endpoint để xác nhận hoàn thành yêu cầu bảo trì bởi khách hàng
        [HttpPost("confirm-completion-by-customer")]
        public async Task<IActionResult> ConfirmMaintenanceCompletion([FromBody] ConfirmMaintenanceCompletionByCustomerDTO confirmDto)
        {
            if (confirmDto == null)
            {
                return BadRequest("Invalid confirmation request.");
            }

            try
            {
                var command = new ConfirmMaintenanceCompletionByCustomerCommand(confirmDto.MaintenanceRequestId, confirmDto.CustomerFeedback);
                var response = await _sender.Send(command);

                if (response.Succeeded)
                {
                    return Ok(response);
                }
                else
                {
                    return BadRequest(response.Message);
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"An error occurred while confirming maintenance completion: {ex.Message}");
            }
        }

        // Endpoint để lấy yêu cầu bảo trì theo ID
        //[HttpGet("{id}")]
        //public async Task<IActionResult> GetMaintenanceRequestById(Guid id)
        //{
        //    try
        //    {
        //        var query = new GetMaintenanceRequestByIdQuery(id);
        //        var response = await _sender.Send(query);

        //        if (response == null)
        //        {
        //            return NotFound("Maintenance request not found.");
        //        }

        //        return Ok(new ApiResponse<MaintenanceRequestDTO>(response, "Maintenance request retrieved successfully."));
        //    }
        //    catch (Exception ex)
        //    {
        //        return StatusCode(500, $"An error occurred while retrieving the maintenance request: {ex.Message}");
        //    }
        //}
    }

}

