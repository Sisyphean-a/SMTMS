using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Rougamo;
using Rougamo.Context;
using SMTMS.Core.Infrastructure;

namespace SMTMS.Core.Aspects;

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public class LogAttribute : MoAttribute
{
    public override void OnEntry(MethodContext context)
    {
        var logger = GetLogger(context);
        logger?.LogInformation("[{TargetType}.{MethodName}] Entry", context.TargetType.Name, context.Method.Name);
    }

    public override void OnSuccess(MethodContext context)
    {
        var logger = GetLogger(context);
        logger?.LogInformation("[{TargetType}.{MethodName}] Success", context.TargetType.Name, context.Method.Name);
    }

    public override void OnException(MethodContext context)
    {
        var logger = GetLogger(context);
        logger?.LogError(context.Exception, "[{TargetType}.{MethodName}] Exception", context.TargetType.Name, context.Method.Name);
    }

    private ILogger? GetLogger(MethodContext context)
    {
        // Try to get ILogger<T> where T is the target type
        // If that fails (generic construction issues), fall back to ILogger
        try
        {
            var loggerType = typeof(ILogger<>).MakeGenericType(context.TargetType);
            return ServiceLocator.Provider.GetService(loggerType) as ILogger;
        }
        catch (Exception)
        {
            // Fallback or eat exception if ServiceLocator isn't ready or generic type fails
            // In a real scenario, you might want a fallback non-generic logger
            var loggerFactory = ServiceLocator.Provider.GetService<ILoggerFactory>();
            return loggerFactory?.CreateLogger(context.TargetType);
        }
    }
}
