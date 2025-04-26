// EffiRent.WebAPI/Jobs/CheckExpiringContractsJob.cs
using EffiRent.Application.Commands.ContractCommand;
using MediatR;
using Microsoft.Extensions.Logging;
using Quartz;

namespace EffiRent.WebAPI.Jobs
{
    public class CheckExpiringContractsJob : IJob
    {
        private readonly IMediator _mediator;
        private readonly ILogger<CheckExpiringContractsJob> _logger;

        public CheckExpiringContractsJob(IMediator mediator, ILogger<CheckExpiringContractsJob> logger)
        {
            _mediator = mediator;
            _logger = logger;
        }

        public async Task Execute(IJobExecutionContext context)
        {
            try
            {
                _logger.LogInformation("Checking expiring contracts...");
                var command = new CheckExpiringContractsCommand { CheckDate = DateTime.UtcNow };
                await _mediator.Send(command);
                _logger.LogInformation("Expiring contracts checked successfully.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking expiring contracts.");
                throw;
            }
        }
    }
}