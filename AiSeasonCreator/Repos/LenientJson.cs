using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AiSeasonCreator.Repos
{
    /// <summary>
    /// iRacing occasionally sends null (or a float) where our models expect a plain number/bool.
    /// With live data we can't ship a fix every time that happens, so reading is lenient:
    /// nulls become the default value and numbers are coerced to the target type.
    /// </summary>
    public static class LenientJson
    {
        public static readonly JsonSerializerOptions Options = Create();

        private static JsonSerializerOptions Create()
        {
            var options = new JsonSerializerOptions
            {
                NumberHandling = JsonNumberHandling.AllowReadingFromString,
                ReadCommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true,
            };
            options.Converters.Add(new LenientIntConverter());
            options.Converters.Add(new LenientLongConverter());
            options.Converters.Add(new LenientBoolConverter());
            options.Converters.Add(new LenientDecimalConverter());
            options.Converters.Add(new LenientDoubleConverter());
            options.Converters.Add(new LenientDateTimeConverter());
            return options;
        }

        private sealed class LenientIntConverter : JsonConverter<int>
        {
            public override bool HandleNull => true;

            public override int Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                switch (reader.TokenType)
                {
                    case JsonTokenType.Number:
                        if (reader.TryGetInt32(out var i)) return i;
                        return (int)Math.Round(reader.GetDouble());
                    case JsonTokenType.String:
                        return double.TryParse(reader.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var d) ? (int)Math.Round(d) : 0;
                    case JsonTokenType.True: return 1;
                    case JsonTokenType.False: return 0;
                    default:
                        reader.Skip();
                        return 0;
                }
            }

            public override void Write(Utf8JsonWriter writer, int value, JsonSerializerOptions options) => writer.WriteNumberValue(value);
        }

        private sealed class LenientLongConverter : JsonConverter<long>
        {
            public override bool HandleNull => true;

            public override long Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                switch (reader.TokenType)
                {
                    case JsonTokenType.Number:
                        if (reader.TryGetInt64(out var l)) return l;
                        return (long)Math.Round(reader.GetDouble());
                    case JsonTokenType.String:
                        return long.TryParse(reader.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ? v : 0;
                    default:
                        reader.Skip();
                        return 0;
                }
            }

            public override void Write(Utf8JsonWriter writer, long value, JsonSerializerOptions options) => writer.WriteNumberValue(value);
        }

        private sealed class LenientBoolConverter : JsonConverter<bool>
        {
            public override bool HandleNull => true;

            public override bool Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                switch (reader.TokenType)
                {
                    case JsonTokenType.True: return true;
                    case JsonTokenType.False: return false;
                    case JsonTokenType.Number: return reader.GetDouble() != 0;
                    case JsonTokenType.String:
                        var s = reader.GetString();
                        return string.Equals(s, "true", StringComparison.OrdinalIgnoreCase) || s == "1";
                    default:
                        reader.Skip();
                        return false;
                }
            }

            public override void Write(Utf8JsonWriter writer, bool value, JsonSerializerOptions options) => writer.WriteBooleanValue(value);
        }

        private sealed class LenientDecimalConverter : JsonConverter<decimal>
        {
            public override bool HandleNull => true;

            public override decimal Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                switch (reader.TokenType)
                {
                    case JsonTokenType.Number:
                        return reader.TryGetDecimal(out var m) ? m : (decimal)reader.GetDouble();
                    case JsonTokenType.String:
                        return decimal.TryParse(reader.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ? v : 0;
                    default:
                        reader.Skip();
                        return 0;
                }
            }

            public override void Write(Utf8JsonWriter writer, decimal value, JsonSerializerOptions options) => writer.WriteNumberValue(value);
        }

        private sealed class LenientDoubleConverter : JsonConverter<double>
        {
            public override bool HandleNull => true;

            public override double Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                switch (reader.TokenType)
                {
                    case JsonTokenType.Number:
                        return reader.GetDouble();
                    case JsonTokenType.String:
                        return double.TryParse(reader.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ? v : 0;
                    default:
                        reader.Skip();
                        return 0;
                }
            }

            public override void Write(Utf8JsonWriter writer, double value, JsonSerializerOptions options) => writer.WriteNumberValue(value);
        }

        private sealed class LenientDateTimeConverter : JsonConverter<DateTime>
        {
            public override bool HandleNull => true;

            public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                if (reader.TokenType == JsonTokenType.String)
                {
                    if (reader.TryGetDateTime(out var dt)) return dt;
                    if (DateTime.TryParse(reader.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out dt)) return dt;
                }
                else
                {
                    reader.Skip();
                }
                return default;
            }

            public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options) => writer.WriteStringValue(value);
        }
    }
}
