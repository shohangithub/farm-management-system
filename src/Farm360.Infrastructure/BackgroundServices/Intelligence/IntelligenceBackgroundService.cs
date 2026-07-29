using Farm360.Application.Intelligence.Interfaces;
using Farm360.Domain.Common;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Farm360.Infrastructure.BackgroundServices.Intelligence;

public sealed class IntelligenceBackgroundService : BackgroundService
{
    private readonly IIntelligenceEventChannel _channel;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<IntelligenceBackgroundService> _logger;

    public IntelligenceBackgroundService(
        IIntelligenceEventChannel channel,
        IServiceProvider serviceProvider,
        ILogger<IntelligenceBackgroundService> logger)
    {
        _channel = channel;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Intelligence Background Service is starting.");

        try
        {
            await foreach (var domainEvent in _channel.ReadAllAsync(stoppingToken))
            {
                try
                {
                    if (_logger.IsEnabled(LogLevel.Information))
                    {
                        _logger.LogInformation("Processing intelligence event: {EventName}", domainEvent.GetType().Name);
                    }
                    
                    using var scope = _serviceProvider.CreateScope();
                    var publisher = scope.ServiceProvider.GetRequiredService<IPublisher>();
                    
                    // We publish the event so that intelligence-specific handlers can pick it up.
                    // This allows us to keep the main transactional handlers fast.
                    await publisher.Publish(domainEvent, stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing intelligence event: {EventName}", domainEvent.GetType().Name);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when stopping
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "Intelligence Background Service encountered a fatal error.");
        }

        _logger.LogInformation("Intelligence Background Service is stopping.");
    }
}
