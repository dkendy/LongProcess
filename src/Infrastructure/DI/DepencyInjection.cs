using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Infrastructure.Database;
using Infrastructure.ServiceBus;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure.DI;
public static class DependencyInjection
{
    public static IServiceCollection AddInfraestrutura(this IServiceCollection services)
    {
        services.AddSingleton<ServiceBusConnection>();
        services.AddSingleton<IServiceBusServices, ServiceBusServices>();
        services.AddSingleton<IDatabaseServices, DatabaseServices>();
        services.AddLogging();
        return services;
    }
}
