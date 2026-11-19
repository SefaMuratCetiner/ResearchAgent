using System.Data;
using Dapper;

namespace Research.Providers.Postgres.Dapper;

/// <summary>
/// Npgsql reads <c>timestamptz</c> as a UTC <see cref="DateTime"/>; entities use <see cref="DateTimeOffset"/>.
/// Registered once at composition time by every Host that uses Postgres.
/// </summary>
public static class DapperTypeHandlers
{
    private static int _registered;

    public static void Register()
    {
        if (Interlocked.Exchange(ref _registered, 1) == 1)
        {
            return;
        }

        // snake_case columns map to PascalCase row properties without per-column aliases.
        DefaultTypeMap.MatchNamesWithUnderscores = true;
        SqlMapper.AddTypeHandler(new DateTimeOffsetHandler());
        SqlMapper.AddTypeHandler(new NullableDateTimeOffsetHandler());
    }

    private sealed class DateTimeOffsetHandler : SqlMapper.TypeHandler<DateTimeOffset>
    {
        public override void SetValue(IDbDataParameter parameter, DateTimeOffset value) =>
            parameter.Value = value.ToUniversalTime();

        public override DateTimeOffset Parse(object value) => ToOffset(value);
    }

    private sealed class NullableDateTimeOffsetHandler : SqlMapper.TypeHandler<DateTimeOffset?>
    {
        public override void SetValue(IDbDataParameter parameter, DateTimeOffset? value) =>
            parameter.Value = value is { } v ? v.ToUniversalTime() : DBNull.Value;

        public override DateTimeOffset? Parse(object value) =>
            value is null or DBNull ? null : ToOffset(value);
    }

    private static DateTimeOffset ToOffset(object value) => value switch
    {
        DateTimeOffset offset => offset,
        DateTime dateTime => new DateTimeOffset(DateTime.SpecifyKind(dateTime, DateTimeKind.Utc)),
        _ => throw new DataException($"Cannot convert {value.GetType().Name} to DateTimeOffset."),
    };
}
