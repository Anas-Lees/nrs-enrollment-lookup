using FluentValidation;
using Nrs.Api.Features.Enrollments.Messaging;

namespace Nrs.Api.Features.Enrollments;

/// <summary>
/// Registers everything the enrollment vertical slices need: the FluentValidation
/// validators, the slice handlers, and the message publisher. The publisher defaults to
/// a no-op; commit 3 swaps in the RabbitMQ implementation when a broker is configured.
/// </summary>
public static class EnrollmentServiceCollectionExtensions
{
    public static IServiceCollection AddEnrollmentFeature(this IServiceCollection services, IConfiguration configuration)
    {
        // Discover the nested AbstractValidator<T> in each slice file.
        services.AddValidatorsFromAssemblyContaining<CreateEnrollment.Request>();

        services.AddScoped<CreateEnrollment.Handler>();
        services.AddScoped<UpdateEnrollment.Handler>();
        services.AddScoped<GetEnrollment.Handler>();
        services.AddScoped<ListEnrollments.Handler>();

        // Messaging: when a broker is configured, publish enrollment events to RabbitMQ and
        // run the review consumer; otherwise fall back to a no-op publisher so local dev and
        // the tests need no broker at all (mirrors the Redis cache-or-in-memory posture).
        var rabbitMq = configuration.GetConnectionString("RabbitMq");
        if (!string.IsNullOrWhiteSpace(rabbitMq))
        {
            services.AddSingleton<IEventPublisher>(sp =>
                new RabbitMqEventPublisher(rabbitMq, sp.GetRequiredService<ILogger<RabbitMqEventPublisher>>()));
            services.AddHostedService(sp =>
                new EnrollmentEventsConsumer(
                    rabbitMq,
                    sp.GetRequiredService<IServiceScopeFactory>(),
                    sp.GetRequiredService<ILogger<EnrollmentEventsConsumer>>()));
        }
        else
        {
            services.AddSingleton<IEventPublisher, NullEventPublisher>();
        }

        return services;
    }
}
