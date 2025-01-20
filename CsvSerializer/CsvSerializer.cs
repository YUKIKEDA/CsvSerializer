using System.Reflection;
using System.Text;

namespace CsvSerializer
{
    [AttributeUsage(AttributeTargets.Property)]
    public class CsvColumnAttribute : Attribute
    {
        public string Name { get; }

        public CsvColumnAttribute(string name)
        {
            Name = name;
        }
    }

    public class CsvSerializerOptions
    {
        public string Delimiter { get; set; } = ",";
        public bool IncludeHeader { get; set; } = true;
        public string DateTimeFormat { get; set; } = "yyyy-MM-dd HH:mm:ss";
    }

    public static class CsvSerializer
    {
        private static readonly CsvSerializerOptions DefaultOptions = new();

        public static string Serialize<T>(
            IEnumerable<T> objects,
            CsvSerializerOptions? options = null) where T : class
        {
            options ??= DefaultOptions;
            var properties = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance);

            // プロパティの型をチェック
            foreach (var property in properties)
            {
                ValidatePropertyType(property);
            }

            var stringBuilder = new StringBuilder();

            // ヘッダーの書き込み
            if (options.IncludeHeader)
            {
                stringBuilder.AppendLine(string.Join(options.Delimiter,
                    properties.Select(p => EscapeField(GetPropertyColumnName(p), options.Delimiter))));
            }

            // データの書き込み
            foreach (var obj in objects)
            {
                var fields = properties.Select(p => SerializeField(p.GetValue(obj), options));
                stringBuilder.AppendLine(string.Join(options.Delimiter, fields));
            }

            return stringBuilder.ToString();
        }

        public static IEnumerable<T> Deserialize<T>(
            string csvData,
            CsvSerializerOptions? options = null) where T : class, new()
        {
            options ??= DefaultOptions;
            var properties = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance);

            // プロパティの型をチェック
            foreach (var property in properties)
            {
                ValidatePropertyType(property);
            }

            using var reader = new StringReader(csvData);

            // ヘッダー行の処理
            string? headerLine = reader.ReadLine() ?? throw new InvalidOperationException("CSV data is empty.");
            if (options.IncludeHeader)
            {
                var headers = ParseLine(headerLine, options.Delimiter);
                ValidateHeaders(headers, properties);
            }

            // データの読み込み
            string? line;
            while ((line = reader.ReadLine()) != null)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                yield return CreateObject<T>(ParseLine(line, options.Delimiter), properties);
            }
        }

        private static string SerializeField(object? value, CsvSerializerOptions options)
        {
            if (value == null)
                return "";

            string stringValue = value switch
            {
                DateTime dateTime => dateTime.ToString(options.DateTimeFormat),
                _ => value.ToString() ?? ""
            };

            return EscapeField(stringValue, options.Delimiter);
        }

        private static string EscapeField(string field, string delimiter)
        {
            if (string.IsNullOrEmpty(field))
                return "";

            if (field.Contains(delimiter) || field.Contains('"') || field.Contains('\n'))
            {
                return $"\"{field.Replace("\"", "\"\"")}\"";
            }

            return field;
        }

        private static string[] ParseLine(string line, string delimiter)
        {
            var fields = new List<string>();
            var currentField = new StringBuilder();
            bool inQuotes = false;

            for (int i = 0; i < line.Length; i++)
            {
                if (line[i] == '\"')
                {
                    if (inQuotes && i + 1 < line.Length && line[i + 1] == '\"')
                    {
                        currentField.Append('\"');
                        i++;
                    }
                    else
                    {
                        inQuotes = !inQuotes;
                    }
                }
                else if (line[i] == delimiter[0] && !inQuotes)
                {
                    fields.Add(currentField.ToString());
                    currentField.Clear();
                }
                else
                {
                    currentField.Append(line[i]);
                }
            }

            fields.Add(currentField.ToString());
            return [.. fields];
        }

        private static void ValidateHeaders(string[] headers, PropertyInfo[] properties)
        {
            var propertyNames = properties.Select(GetPropertyColumnName).ToArray();
            var missingHeaders = propertyNames.Except(headers).ToArray();
            if (missingHeaders.Any())
            {
                throw new InvalidOperationException($"Missing CSV headers: {string.Join(", ", missingHeaders)}");
            }
        }

        private static string GetPropertyColumnName(PropertyInfo property)
        {
            var attribute = property.GetCustomAttribute<CsvColumnAttribute>();
            return attribute?.Name ?? property.Name;
        }

        private static T CreateObject<T>(string[] fields, PropertyInfo[] properties) where T : class, new()
        {
            var obj = new T();
            var headerToProperty = properties.ToDictionary(GetPropertyColumnName, p => p);

            for (int i = 0; i < fields.Length; i++)
            {
                var headerName = GetPropertyColumnName(properties[i]);
                if (!headerToProperty.TryGetValue(headerName, out var property))
                    continue;

                var field = fields[i];
                if (string.IsNullOrEmpty(field))
                    continue;

                try
                {
                    object value = Convert.ChangeType(field, property.PropertyType);
                    property.SetValue(obj, value);
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException(
                        $"Failed to convert field '{field}' to type {property.PropertyType} for property {property.Name}", ex);
                }
            }
            return obj;
        }

        private static void ValidatePropertyType(PropertyInfo property)
        {
            var type = property.PropertyType;
            if (IsComplexType(type))
            {
                throw new InvalidOperationException(
                    $"Property '{property.Name}' of type '{type.Name}' is not supported. Only primitive types, string, and DateTime are supported.");
            }
        }

        private static bool IsComplexType(Type type)
        {
            if (type.IsPrimitive || type == typeof(string) || type == typeof(DateTime))
                return false;

            if (type.IsGenericType)
                return true;

            if (type.IsClass && type != typeof(string))
                return true;

            return false;
        }
    }
}
