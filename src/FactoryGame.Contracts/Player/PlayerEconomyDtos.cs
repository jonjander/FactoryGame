namespace FactoryGame.Contracts.Player;

public sealed record PlayerEconomyOverviewDto(
    decimal Cash,
    decimal PoolValue,
    decimal MachineValue,
    decimal TotalValue,
    IReadOnlyList<PlayerEconomyHistoryPointDto> History,
    PlayerEconomyPeriodChangesDto PeriodChanges);

public sealed record PlayerEconomyHistoryPointDto(
    DateTimeOffset At,
    decimal TotalValue,
    decimal Cash,
    decimal AssetsValue);

public sealed record PlayerEconomyPeriodChangesDto(
    decimal? DayPercent,
    decimal? WeekPercent,
    decimal? MonthPercent,
    decimal? YearPercent,
    decimal? MaxPercent);
