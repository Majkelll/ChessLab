using Microsoft.AspNetCore.SignalR;

namespace ChessLab.Web.Hubs;

public sealed class HubErrorFilter : IHubFilter
{
    private const string ParameterSuffix = " (Parameter '";

    public async ValueTask<object?> InvokeMethodAsync(
        HubInvocationContext invocationContext,
        Func<HubInvocationContext, ValueTask<object?>> next)
    {
        try
        {
            return await next(invocationContext);
        }
        catch (InvalidOperationException ex)
        {
            throw new HubException(ex.Message);
        }
        catch (ArgumentException ex)
        {
            throw new HubException(WithoutParameterSuffix(ex.Message));
        }
    }

    private static string WithoutParameterSuffix(string message)
    {
        var suffixStart = message.IndexOf(ParameterSuffix, StringComparison.Ordinal);
        return suffixStart < 0 ? message : message[..suffixStart];
    }
}
