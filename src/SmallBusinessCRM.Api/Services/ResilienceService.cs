using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;

namespace SmallBusinessCRM.Api.Services;

public interface IResiliencePolicyService
{
    Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken ct = default);
    Task ExecuteAsync(Func<CancellationToken, Task> action, CancellationToken ct = default);
}

public class ResiliencePolicyService : IResiliencePolicyService
{
    private readonly ResiliencePipeline _pipeline;
    private readonly ILogger<ResiliencePolicyService> _logger;

    public ResiliencePolicyService(ILogger<ResiliencePolicyService> logger)
    {
        _logger = logger;

        _pipeline = new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = 3,
                Delay = TimeSpan.FromMilliseconds(200),
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
                ShouldHandle = new PredicateBuilder()
                    .Handle<DbUpdateConcurrencyException>()
                    .Handle<TimeoutException>()
                    .Handle<DbException>(),
                OnRetry = args =>
                {
                    _logger.LogWarning(args.Outcome.Exception, "Transient database error encountered. Retrying attempt {AttemptNumber} after {RetryDelay}ms...", args.AttemptNumber, args.RetryDelay.TotalMilliseconds);
                    return ValueTask.CompletedTask;
                }
            })
            .AddCircuitBreaker(new CircuitBreakerStrategyOptions
            {
                FailureRatio = 0.5,
                SamplingDuration = TimeSpan.FromSeconds(30),
                MinimumThroughput = 5,
                BreakDuration = TimeSpan.FromSeconds(15),
                ShouldHandle = new PredicateBuilder()
                    .Handle<DbException>()
                    .Handle<TimeoutException>(),
                OnOpened = args =>
                {
                    _logger.LogError(args.Outcome.Exception, "Database Circuit Breaker OPENED for {BreakDuration}s due to consecutive failures.", args.BreakDuration.TotalSeconds);
                    return ValueTask.CompletedTask;
                },
                OnClosed = _ =>
                {
                    _logger.LogInformation("Database Circuit Breaker CLOSED. Normal operations resumed.");
                    return ValueTask.CompletedTask;
                }
            })
            .Build();
    }

    public async Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken ct = default) =>
        await _pipeline.ExecuteAsync(async token => await action(token), ct);

    public async Task ExecuteAsync(Func<CancellationToken, Task> action, CancellationToken ct = default) =>
        await _pipeline.ExecuteAsync(async token => await action(token), ct);
}
