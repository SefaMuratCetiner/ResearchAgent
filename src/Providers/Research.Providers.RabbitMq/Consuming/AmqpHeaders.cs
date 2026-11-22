using System.Globalization;
using System.Text;

namespace Research.Providers.RabbitMq;

/// <summary>Reads AMQP header values, which arrive as UTF-8 byte arrays.</summary>
internal static class AmqpHeaders
{
    public static Dictionary<string, string> ToStrings(IDictionary<string, object?>? headers)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        if (headers is null)
        {
            return result;
        }

        foreach (var (key, value) in headers)
        {
            var text = value switch
            {
                byte[] bytes => Encoding.UTF8.GetString(bytes),
                string s => s,
                null => null,
                IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
                _ => null,
            };

            if (text is not null)
            {
                result[key] = text;
            }
        }

        return result;
    }
}
