namespace FactoryGame.Contracts.Machines;

public sealed record MachinePortDto(string Name, string Direction);

public sealed record MachineStoreItemDto(string Type, string DisplayName, decimal Price, bool Purchasable, IReadOnlyList<MachinePortDto> Ports);

public sealed record PlayerMachineStockDto(Guid Id, string MachineType, DateTimeOffset CreatedAt);

public sealed record PurchaseMachineRequest(string MachineType);

public sealed record PlaceMachineFromStockRequest(Guid StockId, string MachineId);

public sealed record ReturnMachineToStockRequest(string MachineId);
