using System.Reflection;
using System.Text;

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
        public System.Globalization.CultureInfo Culture { get; set; } = System.Globalization.CultureInfo.InvariantCulture;
    }

    public class CsvSerializationException : Exception
    {
        public CsvSerializationException(string message) : base(message) { }
        public CsvSerializationException(string message, Exception innerException) : base(message, innerException) { }
    }

    public static class CsvSerializer
    {
        private static readonly CsvSerializerOptions DefaultOptions = new();
        private static readonly HashSet<Type> SupportedPrimitiveTypes =
        [
            typeof(string),
            typeof(int),
            typeof(long),
            typeof(float),
            typeof(double),
            typeof(decimal),
            typeof(bool),
            typeof(DateTime),
            typeof(char)
        ];

        #region Public Methods

        public static string Serialize<T>(
            IEnumerable<T> objects,
            CsvSerializerOptions? options = null) where T : class
        {
            options ??= DefaultOptions;
            var properties = GetValidatedProperties<T>();
            var objectsList = objects.ToList();

            var dictionaryKeys = CollectDictionaryKeys(properties, objectsList);
            var stringBuilder = InitializeStringBuilder(properties, dictionaryKeys, objectsList.Count);

            WriteHeader(stringBuilder, properties, dictionaryKeys, options);
            WriteData(stringBuilder, objectsList, properties, dictionaryKeys, options);

            return stringBuilder.ToString();
        }

        public static IEnumerable<T> Deserialize<T>(
            string csvData,
            CsvSerializerOptions? options = null) where T : class, new()
        {
            options ??= DefaultOptions;
            var properties = GetValidatedProperties<T>();

            using (var reader = new StringReader(csvData))
            {
                var headers = ProcessHeaders(reader, properties, options);
                var dictionaryMappings = CreateDictionaryMappings(properties, headers);

                string? line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    var fields = ParseLine(line, options.Delimiter);
                    yield return CreateObject<T>(fields, headers, properties, dictionaryMappings, options);
                }
            }
        }

        #endregion

        #region Private Helper Methods

        private static PropertyInfo[] GetValidatedProperties<T>() where T : class
        {
            var properties = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance);
            foreach (var property in properties)
            {
                ValidatePropertyType(property);
            }
            return properties;
        }

        private static Dictionary<PropertyInfo, HashSet<object>> CollectDictionaryKeys<T>(
            PropertyInfo[] properties,
            IList<T> objects) where T : class
        {
            var dictionaryKeys = new Dictionary<PropertyInfo, HashSet<object>>();
            foreach (var property in properties.Where(p => IsDictionaryType(p.PropertyType)))
            {
                var keys = new HashSet<object>();
                foreach (var obj in objects)
                {
                    if (obj.GetType().GetProperty(property.Name)?.GetValue(obj) is System.Collections.IDictionary dict)
                    {
                        foreach (System.Collections.DictionaryEntry entry in dict)
                        {
                            keys.Add(entry.Key);
                        }
                    }
                }
                dictionaryKeys[property] = keys;
            }
            return dictionaryKeys;
        }

        private static StringBuilder InitializeStringBuilder(
            PropertyInfo[] properties,
            Dictionary<PropertyInfo, HashSet<object>> dictionaryKeys,
            int objectCount)
        {
            var totalColumns = properties.Count(p => !IsDictionaryType(p.PropertyType)) +
                             dictionaryKeys.Sum(kvp => kvp.Value.Count);
            var estimatedCapacity = (objectCount + 1) * totalColumns * 20;
            return new StringBuilder(estimatedCapacity);
        }

        private static void WriteHeader(
            StringBuilder sb,
            PropertyInfo[] properties,
            Dictionary<PropertyInfo, HashSet<object>> dictionaryKeys,
            CsvSerializerOptions options)
        {
            if (!options.IncludeHeader) return;

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
            sb.AppendLine(string.Join(options.Delimiter, headers));
        }

        private static void WriteData<T>(
            StringBuilder sb,
            IList<T> objects,
            PropertyInfo[] properties,
            Dictionary<PropertyInfo, HashSet<object>> dictionaryKeys,
            CsvSerializerOptions options) where T : class
        {
            foreach (var obj in objects)
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
                            fields.Add(SerializeField(dict[key], options));
                        }
                    }
                }
                sb.AppendLine(string.Join(options.Delimiter, fields));
            }
        }

        private static string[] ProcessHeaders(
            StringReader reader,
            PropertyInfo[] properties,
            CsvSerializerOptions options)
        {
            var headerLine = reader.ReadLine() ?? throw new CsvSerializationException("CSV data is empty.");
            var headers = ParseLine(headerLine, options.Delimiter);
            ValidateHeaders(headers, properties);
            return headers;
        }

        private static Dictionary<PropertyInfo, (Type KeyType, HashSet<string> Headers)> CreateDictionaryMappings(
            PropertyInfo[] properties,
            string[] headers)
        {
            var dictionaryMappings = new Dictionary<PropertyInfo, (Type KeyType, HashSet<string> Headers)>();
            foreach (var property in properties.Where(p => IsDictionaryType(p.PropertyType)))
            {
                var keyType = property.PropertyType.GetGenericArguments()[0];
                var dictHeaders = headers.Where(h => !properties.Any(p =>
                    !IsDictionaryType(p.PropertyType) && GetPropertyColumnName(p) == h))
                    .ToHashSet();
                dictionaryMappings[property] = (keyType, dictHeaders);
            }
            return dictionaryMappings;
        }

        private static string SerializeField(object? value, CsvSerializerOptions options)
        {
            if (value == null) return "";

            var type = value.GetType();
            var underlyingType = Nullable.GetUnderlyingType(type) ?? type;

            string stringValue = underlyingType switch
            {
                Type t when t == typeof(DateTime) => ((DateTime)value).ToString(options.DateTimeFormat, options.Culture),
                Type t when t.IsEnum => value.ToString() ?? "",
                Type _ when value is IFormattable formattable => formattable.ToString(null, options.Culture),
                _ => value.ToString() ?? ""
            };

            return EscapeField(stringValue, options.Delimiter);
        }

        private static object? DeserializeField(string field, Type targetType, CsvSerializerOptions options)
        {
            if (string.IsNullOrEmpty(field)) return null;
            return DeserializeValue(field, targetType, options);
        }

        private static object DeserializeValue(string value, Type targetType, CsvSerializerOptions options)
        {
            var underlyingType = Nullable.GetUnderlyingType(targetType) ?? targetType;

            if (underlyingType.IsEnum)
            {
                var enumValue = Enum.Parse(underlyingType, value);
                return IsNullableType(targetType)
                    ? Activator.CreateInstance(targetType, enumValue)!
                    : enumValue;
            }

            return Convert.ChangeType(value, underlyingType, options.Culture);
        }

        private static string EscapeField(string field, string delimiter)
        {
            if (string.IsNullOrEmpty(field)) return "";
            return field.Contains(delimiter) || field.Contains('"') || field.Contains('\n')
                ? $"\"{field.Replace("\"", "\"\"")}\"" 
                : field;
        }

        private static string[] ParseLine(string line, string delimiter)
        {
            var fields = new List<string>(Math.Max(1, line.Count(c => c == delimiter[0]) + 1));
            var currentField = new StringBuilder(Math.Min(line.Length, 100));
            var inQuotes = false;

            for (var i = 0; i < line.Length; i++)
            {
                if (line[i] == '"')
                {
                    if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                    {
                        currentField.Append('"');
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

        private static T CreateObject<T>(
            string[] fields,
            string[] headers,
            PropertyInfo[] properties,
            Dictionary<PropertyInfo, (Type KeyType, HashSet<string> Headers)> dictionaryMappings,
            CsvSerializerOptions options) where T : class, new()
        {
            var obj = new T();

            for (var i = 0; i < headers.Length; i++)
            {
                var header = headers[i];
                var field = i < fields.Length ? fields[i] : "";

                var normalProperty = properties.FirstOrDefault(p =>
                    !IsDictionaryType(p.PropertyType) && GetPropertyColumnName(p) == header);

                if (normalProperty != null)
                {
                    ProcessNormalProperty(obj, normalProperty, field, options);
                }
                else
                {
                    ProcessDictionaryProperty(obj, header, field, dictionaryMappings, options);
                }
            }

            return obj;
        }

        private static void ProcessNormalProperty<T>(
            T obj,
            PropertyInfo property,
            string field,
            CsvSerializerOptions options) where T : class
        {
            if (string.IsNullOrEmpty(field) && IsNullableType(property.PropertyType))
            {
                property.SetValue(obj, null);
                return;
            }

            try
            {
                var value = DeserializeField(field, property.PropertyType, options);
                property.SetValue(obj, value);
            }
            catch (Exception ex)
            {
                throw new CsvSerializationException(
                    $"Failed to convert field '{field}' to type {property.PropertyType} for property {property.Name}", ex);
            }
        }

        private static void ProcessDictionaryProperty<T>(
            T obj,
            string header,
            string field,
            Dictionary<PropertyInfo, (Type KeyType, HashSet<string> Headers)> dictionaryMappings,
            CsvSerializerOptions options) where T : class
        {
            foreach (var dictMapping in dictionaryMappings)
            {
                var (keyType, dictHeaders) = dictMapping.Value;
                if (!dictHeaders.Contains(header)) continue;

                var dictProperty = dictMapping.Key;
                var dict = (System.Collections.IDictionary?)dictProperty.GetValue(obj);
                if (dict == null)
                {
                    dict = (System.Collections.IDictionary)Activator.CreateInstance(dictProperty.PropertyType)!;
                    dictProperty.SetValue(obj, dict);
                }

                if (string.IsNullOrEmpty(field)) continue;

                try
                {
                    var valueType = dictProperty.PropertyType.GetGenericArguments()[1];
                    var key = DeserializeValue(header, keyType, options);
                    var value = DeserializeValue(field, valueType, options);
                    dict[key] = value;
                }
                catch (Exception ex)
                {
                    throw new CsvSerializationException(
                        $"Failed to convert field '{field}' to type {dictProperty.PropertyType.GetGenericArguments()[1]} for dictionary key {header}", ex);
                }
            }
        }

        private static void ValidateHeaders(string[] headers, PropertyInfo[] properties)
        {
            var requiredHeaders = properties
                .Where(p => !IsDictionaryType(p.PropertyType))
                .Select(GetPropertyColumnName)
                .ToArray();

            var missingHeaders = requiredHeaders.Except(headers).ToArray();
            if (missingHeaders.Length > 0)
            {
                throw new CsvSerializationException($"Missing CSV headers: {string.Join(", ", missingHeaders)}");
            }
        }

        private static string GetPropertyColumnName(PropertyInfo property)
        {
            var attribute = property.GetCustomAttribute<CsvColumnAttribute>();
            return attribute?.Name ?? property.Name;
        }

        private static void ValidatePropertyType(PropertyInfo property)
        {
            var type = property.PropertyType;
            if (IsComplexType(type))
            {
                throw new CsvSerializationException(
                    $"Property '{property.Name}' of type '{type.Name}' is not supported. Only primitive types, string, and DateTime are supported.");
            }
        }

        private static bool IsComplexType(Type type)
        {
            if (type.IsPrimitive || SupportedPrimitiveTypes.Contains(type))
                return false;

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

            return !type.IsEnum;
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

        private static bool IsNullableType(Type type)
        {
            return type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>);
        }

        #endregion
    }
}
