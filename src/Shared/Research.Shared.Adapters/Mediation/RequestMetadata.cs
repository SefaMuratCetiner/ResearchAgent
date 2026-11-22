using Research.Shared.Core.Mediation;
using Research.Shared.Messaging;

namespace Research.Shared.Adapters.Mediation;

/// <summary>Computes stable display names and kinds once per closed generic behavior type.</summary>
internal static class RequestMetadata
{
    public static string GetDisplayName<TRequest>()
    {
        var type = typeof(TRequest);
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IncomingMessageEnvelope<>))
        {
            return type.GetGenericArguments()[0].Name;
        }

        return type.Name;
    }

    public static string GetKind<TRequest, TResponse>()
        where TRequest : IRequest<TResponse>
    {
        var type = typeof(TRequest);
        if (typeof(IIncomingMessageEnvelope).IsAssignableFrom(type))
        {
            return "message";
        }

        if (typeof(IQuery<TResponse>).IsAssignableFrom(type))
        {
            return "query";
        }

        if (typeof(ICommand<TResponse>).IsAssignableFrom(type))
        {
            return "command";
        }

        return "request";
    }
}
