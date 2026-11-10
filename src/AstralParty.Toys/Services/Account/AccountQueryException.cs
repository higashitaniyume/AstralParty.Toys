namespace AstralParty.Toys.Services.Account;

/// <summary>Only protocol numbers and fixed reason codes; never server text or credential contents.</summary>
public sealed class AccountQueryException(string reason, short? command = null, short? serverError = null) : IOException(reason)
{
    public string Reason { get; } = reason;
    public short? Command { get; } = command;
    public short? ServerError { get; } = serverError;
}
