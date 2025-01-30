using System.Reflection;
using System.Text;
using System.Collections.Generic;

namespace CsvSerializer
{
    [AttributeUsage(AttributeTargets.Property)]
    public class CsvColumnAttribute(string name) : Attribute
    {
        public string Name { get; } = name;
    }

    public class CsvSerializerOptions
    {
        public string Delimiter { get; set; } = ",";
        public bool IncludeHeader { get; set; } = true;
        public string DateTimeFormat { get; set; } = "yyyy-MM-dd HH:mm:ss";
    }

    public class CsvSerializationException : Exception
    {
        public CsvSerializationException(string message) : base(message) { }
        public CsvSerializationException(string message, Exception innerException) : base(message, innerException) { }
    }

    public static class CsvSerializer
    {
        private static readonly CsvSerializerOptions DefaultOptions = new();
        private static readonly Type[] SupportedPrimitiveTypes = 
        {
            typeof(string),
            typeof(int),
            typeof(long),
            typeof(float),
            typeof(double),
            typeof(decimal),
            typeof(bool),
            typeof(DateTime),
            typeof(char)
        };

        public static string Serialize<T>(
            IEnumerable<T> objects,
            CsvSerializerOptions? options = null) where T : class
        {
            options ??= DefaultOptions;
            var properties = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance);
            var objectsList = objects.ToList(); // キャッシュして長さを取得

            // プロパティの型をチェック
            foreach (var property in properties)
            {
                ValidatePropertyType(property);
            }

            // Dictionary型のプロパティからすべてのキーを収集
            var dictionaryKeys = new Dictionary<PropertyInfo, HashSet<object>>();
            foreach (var property in properties)
            {
                if (IsDictionaryType(property.PropertyType))
                {
                    var keys = new HashSet<object>();
                    foreach (var obj in objectsList)
                    {
                        var dict = obj.GetType().GetProperty(property.Name)?.GetValue(obj) as System.Collections.IDictionary;
                        if (dict != null)
                        {
                            foreach (System.Collections.DictionaryEntry entry in dict)
                            {
                                keys.Add(entry.Key);
                            }
                        }
                    }
                    dictionaryKeys[property] = keys;
                }
            }

            // 予想される文字数に基づいて初期容量を設定
            var totalColumns = properties.Where(p => !IsDictionaryType(p.PropertyType)).Count() +
                             dictionaryKeys.Sum(kvp => kvp.Value.Count);
            var estimatedCapacity = (objectsList.Count + 1) * totalColumns * 20;
            var stringBuilder = new StringBuilder(estimatedCapacity);

            // ヘッダーの書き込み
            if (options.IncludeHeader)
            {
                var headers = new List<string>();
                foreach (var property in properties)
                {
                    if (!IsDictionaryType(property.PropertyType))
                    {
                        headers.Add(EscapeField(GetPropertyColumnName(property), options.Delimiter));
                    }
                    else if (dictionaryKeys.TryGetValue(property, out var keys))
                    {
                        headers.AddRange(keys.Select(k => EscapeField(k.ToString() ?? "", options.Delimiter)));
                    }
                }
                stringBuilder.AppendLine(string.Join(options.Delimiter, headers));
            }

            // データの書き込み
            foreach (var obj in objectsList)
            {
                var fields = new List<string>();
                foreach (var property in properties)
                {
                    var value = property.GetValue(obj);
                    if (!IsDictionaryType(property.PropertyType))
                    {
                        fields.Add(SerializeField(value, options));
                    }
                    else if (value is System.Collections.IDictionary dict && dictionaryKeys.TryGetValue(property, out var keys))
                    {
                        foreach (var key in keys)
                        {
                            var dictValue = dict[key];
                            fields.Add(SerializeField(dictValue, options));
                        }
                    }
                }
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
            var headers = ParseLine(headerLine, options.Delimiter);

            // 必須ヘッダーの検証
            var requiredHeaders = properties
                .Where(p => !IsDictionaryType(p.PropertyType))
                .Select(GetPropertyColumnName)
                .ToArray();
            var missingHeaders = requiredHeaders.Except(headers).ToArray();
            if (missingHeaders.Length > 0)
            {
                throw new InvalidOperationException($"Missing CSV headers: {string.Join(", ", missingHeaders)}");
            }

            // Dictionary型のプロパティとそのヘッダーの対応を作成
            var dictionaryMappings = new Dictionary<PropertyInfo, (Type KeyType, HashSet<string> Headers)>();
            foreach (var property in properties)
            {
                if (IsDictionaryType(property.PropertyType))
                {
                    var keyType = property.PropertyType.GetGenericArguments()[0];
                    var dictHeaders = headers.Where(h => !properties.Any(p => 
                        !IsDictionaryType(p.PropertyType) && GetPropertyColumnName(p) == h))
                        .ToHashSet();
                    dictionaryMappings[property] = (keyType, dictHeaders);
                }
            }

            // データの読み込み
            string? line;
            while ((line = reader.ReadLine()) != null)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                var fields = ParseLine(line, options.Delimiter);
                yield return CreateObject<T>(fields, headers, properties, dictionaryMappings);
            }
        }

        private static string SerializeField(object? value, CsvSerializerOptions options)
        {
            if (value == null)
                return "";

            var type = value.GetType();
            var underlyingType = Nullable.GetUnderlyingType(type);
            
            if (underlyingType != null)
            {
                type = underlyingType;
            }

            string stringValue = type switch
            {
                Type t when t == typeof(DateTime) => ((DateTime)value).ToString(options.DateTimeFormat),
                Type t when t.IsEnum => value.ToString() ?? "",
                Type when value is IFormattable formattable => formattable.ToString(null, System.Globalization.CultureInfo.InvariantCulture),
                _ => value.ToString() ?? ""
            };

            return EscapeField(stringValue, options.Delimiter);
        }

        private static object? DeserializeField(string field, Type targetType)
        {
            if (string.IsNullOrEmpty(field))
                return null;

            return DeserializeValue(field, targetType);
        }

        private static object DeserializeValue(string value, Type targetType)
        {
            var underlyingType = Nullable.GetUnderlyingType(targetType) ?? targetType;

            if (underlyingType.IsEnum)
            {
                var enumValue = Enum.Parse(underlyingType, value);
                if (IsNullableType(targetType))
                {
                    return Activator.CreateInstance(targetType, enumValue)!;
                }
                return enumValue;
            }

            return Convert.ChangeType(value, underlyingType, System.Globalization.CultureInfo.InvariantCulture);
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
            var fields = new List<string>(Math.Max(1, line.Count(c => c == delimiter[0]) + 1));
            var currentField = new StringBuilder(Math.Min(line.Length, 100));
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
            if (missingHeaders.Length != 0)
            {
                throw new InvalidOperationException($"Missing CSV headers: {string.Join(", ", missingHeaders)}");
            }
        }

        private static string GetPropertyColumnName(PropertyInfo property)
        {
            var attribute = property.GetCustomAttribute<CsvColumnAttribute>();
            return attribute?.Name ?? property.Name;
        }

        private static T CreateObject<T>(
            string[] fields, 
            string[] headers, 
            PropertyInfo[] properties,
            Dictionary<PropertyInfo, (Type KeyType, HashSet<string> Headers)> dictionaryMappings) where T : class, new()
        {
            var obj = new T();

            for (int i = 0; i < headers.Length; i++)
            {
                var header = headers[i];
                var field = i < fields.Length ? fields[i] : "";

                // 通常のプロパティの処理
                var normalProperty = properties.FirstOrDefault(p => 
                    !IsDictionaryType(p.PropertyType) && GetPropertyColumnName(p) == header);
                
                if (normalProperty != null)
                {
                    if (string.IsNullOrEmpty(field))
                    {
                        if (IsNullableType(normalProperty.PropertyType))
                        {
                            normalProperty.SetValue(obj, null);
                        }
                        continue;
                    }

                    try
                    {
                        var value = DeserializeField(field, normalProperty.PropertyType);
                        normalProperty.SetValue(obj, value);
                    }
                    catch (Exception ex)
                    {
                        throw new CsvSerializationException(
                            $"Failed to convert field '{field}' to type {normalProperty.PropertyType} for property {normalProperty.Name}", ex);
                    }
                }
                else
                {
                    // Dictionary型プロパティの処理
                    foreach (var dictMapping in dictionaryMappings)
                    {
                        var (keyType, dictHeaders) = dictMapping.Value;
                        if (dictHeaders.Contains(header))
                        {
                            var dictProperty = dictMapping.Key;
                            var dict = (System.Collections.IDictionary?)dictProperty.GetValue(obj);
                            if (dict == null)
                            {
                                dict = (System.Collections.IDictionary)Activator.CreateInstance(dictProperty.PropertyType)!;
                                dictProperty.SetValue(obj, dict);
                            }

                            if (!string.IsNullOrEmpty(field))
                            {
                                var valueType = dictProperty.PropertyType.GetGenericArguments()[1];
                                try
                                {
                                    var key = DeserializeValue(header, keyType);
                                    var value = DeserializeValue(field, valueType);
                                    dict[key] = value;
                                }
                                catch (Exception ex)
                                {
                                    throw new CsvSerializationException(
                                        $"Failed to convert field '{field}' to type {valueType} for dictionary key {header}", ex);
                                }
                            }
                        }
                    }
                }
            }

            return obj;
        }

        private static bool IsNullableType(Type type)
        {
            return type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>);
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
            if (type.IsPrimitive || SupportedPrimitiveTypes.Contains(type))
                return false;

            // Nullable<T>の場合、内部の型をチェック
            if (IsNullableType(type))
            {
                var underlyingType = Nullable.GetUnderlyingType(type)!;
                return IsComplexType(underlyingType);
            }

            if (IsDictionaryType(type))
            {
                var genericArgs = type.GetGenericArguments();
                return genericArgs.Any(arg => !IsSupportedType(arg));
            }

            if (type.IsGenericType)
                return true;

            if (type.IsClass && type != typeof(string))
                return true;

            // enum型はサポート対象
            if (type.IsEnum)
                return false;

            return false;
        }

        private static bool IsDictionaryType(Type type)
        {
            return type.IsGenericType && 
                   (type.GetGenericTypeDefinition() == typeof(Dictionary<,>) ||
                    type.GetGenericTypeDefinition() == typeof(IDictionary<,>));
        }

        private static bool IsSupportedType(Type type)
        {
            if (IsNullableType(type))
            {
                var underlyingType = Nullable.GetUnderlyingType(type)!;
                return IsSupportedType(underlyingType);
            }

            return type.IsPrimitive || 
                   SupportedPrimitiveTypes.Contains(type) || 
                   type.IsEnum;
        }
    }
}
