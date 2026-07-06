using FluentValidation;
using Nrs.Api.Features.Enrollments.Messaging;
using Nrs.Api.Features.Enrollments.Workflow;

namespace Nrs.Api.Features.Enrollments;

/// <summary>
/// Registers everything the enrollment vertical slices need: the FluentValidation validators,
/// the slice handlers, the message publisher, and the review workflow. The workflow is Camunda
/// when <c>Camunda:RestAddress</c> is set, otherwise a direct-to-database fallback — so the
/// feature runs with or without an engine (the same cache-or-in-memory posture used elsewhere).
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
        services.AddScoped<DecideEnrollment.Handler>();
        services.AddScoped<ReviewTasks.ListHandler>();
        services.AddScoped<ReviewTasks.ClaimHandler>();
        services.AddScoped<ReviewTasks.ReleaseHandler>();
        services.AddScoped<Notifications.NotificationsFeature.ListHandler>();
        services.AddScoped<Notifications.NotificationsFeature.MarkReadHandler>();
        services.AddScoped<Reports.ReportsFeature.Handler>();

        var rabbitMq = configuration.GetConnectionString("RabbitMq");
        var camundaAddress = configuration["Camunda:RestAddress"];
        var camundaEnabled = !string.IsNullOrWhiteSpace(camundaAddress);

        // Event publisher: RabbitMQ when a broker is configured, else a no-op. Events are
        // published regardless of who owns the review lifecycle.
        if (!string.IsNullOrWhiteSpace(rabbitMq))
        {
            services.AddSingleton<IEventPublisher>(sp =>
                new RabbitMqEventPublisher(rabbitMq, sp.GetRequiredService<ILogger<RabbitMqEventPublisher>>()));
        }
        else
        {
            services.AddSingleton<IEventPublisher, NullEventPublisher>();
        }

        // Review lifecycle owner.
        if (camundaEnabled)
        {
            // Camunda 8 orchestrates SUBMITTED -> UNDER_REVIEW -> APPROVED/REJECTED via BPMN.
            services.AddHttpClient<ICamundaClient, CamundaClient>(client =>
            {
                client.BaseAddress = new Uri(camundaAddress!.TrimEnd('/') + "/");
                // Comfortably above the job-activation long-poll window (10s).
                client.Timeout = TimeSpan.FromSeconds(30);
            });
            services.AddScoped<IEnrollmentWorkflow, CamundaEnrollmentWorkflow>();
            services.AddHostedService<EnrollmentProcessWorker>();
        }
        else
        {
            // No engine: decisions are applied straight to the database, and (when a broker is
            // present) the RabbitMQ consumer advances submitted enrollments to under-review.
            services.AddScoped<IEnrollmentWorkflow, DbEnrollmentWorkflow>();
            if (!string.IsNullOrWhiteSpace(rabbitMq))
            {
                services.AddHostedService(sp =>
                    new EnrollmentEventsConsumer(
                        rabbitMq,
                        sp.GetRequiredService<IServiceScopeFactory>(),
                        sp.GetRequiredService<ILogger<EnrollmentEventsConsumer>>()));
            }
        }

        return services;
    }
}
