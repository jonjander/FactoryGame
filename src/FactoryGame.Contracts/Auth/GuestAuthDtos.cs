namespace FactoryGame.Contracts.Auth;

public sealed record GuestAuthRequest(string DeviceKey);

public sealed record GuestAuthResponse(Guid PlayerId, string SessionToken);
