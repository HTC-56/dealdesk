using System.Data;
using Dapper;
using DealDesk.Domain;

namespace DealDesk.Data;

/// Maps Money to a SQLite INTEGER column of whole cents, both directions.
/// Registered once by Db.RegisterTypeHandlers.
public sealed class MoneyTypeHandler : SqlMapper.TypeHandler<Money>
{
    public override void SetValue(IDbDataParameter parameter, Money value)
    {
        ArgumentNullException.ThrowIfNull(parameter);
        parameter.DbType = DbType.Int64;
        parameter.Value = value.Cents;
    }

    public override Money Parse(object value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new Money(Convert.ToInt64(value, System.Globalization.CultureInfo.InvariantCulture));
    }
}
