using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using MyCompany.Observability.Configuration;

namespace MyCompany.Observability.Services
{
    public interface IRedactionService
    {
        string RedactSensitiveData(string content, string contentType = "application/json");
        Dictionary<string, string> RedactHeaders(Dictionary<string, string> headers);
        string RedactQueryString(string queryString);
    }

    public class RedactionService : IRedactionService
    {
        private readonly RedactionOptions _options;
        private readonly Regex _jsonPropertyRegex;

        public RedactionService(RedactionOptions options)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            var pattern = string.Join("|", _options.SensitiveKeys.Select(k => $@"""{Regex.Escape(k)}"""));
            _jsonPropertyRegex = new Regex($@"({pattern})\s*:\s*""[^""]*""", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        }

        public string RedactSensitiveData(string content, string contentType = "application/json")
        {
            if (string.IsNullOrEmpty(content) || !_options.RedactRequestBody && !_options.RedactResponseBody)
                return content;

            try
            {
                if (contentType?.ToLowerInvariant().Contains("json") == true)
                {
                    return RedactJsonContent(content);
                }
                else if (contentType?.ToLowerInvariant().Contains("xml") == true)
                {
                    return RedactXmlContent(content);
                }
                else
                {
                    return RedactPlainTextContent(content);
                }
            }
            catch
            {
                return content;
            }
        }

        public Dictionary<string, string> RedactHeaders(Dictionary<string, string> headers)
        {
            if (headers == null || !_options.RedactHeaders)
                return headers;

            var redactedHeaders = new Dictionary<string, string>();
            foreach (var header in headers)
            {
                var key = header.Key.ToLowerInvariant();
                if (_options.SensitiveKeys.Any(sk => key.Contains(sk.ToLowerInvariant())))
                {
                    redactedHeaders[header.Key] = _options.RedactionText;
                }
                else
                {
                    redactedHeaders[header.Key] = header.Value;
                }
            }
            return redactedHeaders;
        }

        public string RedactQueryString(string queryString)
        {
            if (string.IsNullOrEmpty(queryString) || !_options.RedactQueryParams)
                return queryString;

            var queryParams = queryString.TrimStart('?').Split('&');
            var redactedParams = new List<string>();

            foreach (var param in queryParams)
            {
                var parts = param.Split('=');
                if (parts.Length == 2)
                {
                    var key = parts[0].ToLowerInvariant();
                    if (_options.SensitiveKeys.Any(sk => key.Contains(sk.ToLowerInvariant())))
                    {
                        redactedParams.Add($"{parts[0]}={_options.RedactionText}");
                    }
                    else
                    {
                        redactedParams.Add(param);
                    }
                }
                else
                {
                    redactedParams.Add(param);
                }
            }

            return string.Join("&", redactedParams);
        }

        private string RedactJsonContent(string jsonContent)
        {
            try
            {
                using var document = JsonDocument.Parse(jsonContent);
                var redactedJson = RedactJsonElement(document.RootElement);
                return JsonSerializer.Serialize(redactedJson, new JsonSerializerOptions { WriteIndented = false });
            }
            catch
            {
                return _jsonPropertyRegex.Replace(jsonContent, match => 
                {
                    var propertyName = match.Groups[1].Value;
                    return $"{propertyName}:\"{_options.RedactionText}\"";
                });
            }
        }

        private object RedactJsonElement(JsonElement element)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.Object:
                    var dictionary = new Dictionary<string, object>();
                    foreach (var property in element.EnumerateObject())
                    {
                        var propertyName = property.Name;
                        if (_options.SensitiveKeys.Any(sk => 
                            propertyName.IndexOf(sk, StringComparison.OrdinalIgnoreCase) >= 0))
                        {
                            dictionary[propertyName] = _options.RedactionText;
                        }
                        else
                        {
                            dictionary[propertyName] = RedactJsonElement(property.Value);
                        }
                    }
                    return dictionary;

                case JsonValueKind.Array:
                    var list = new List<object>();
                    foreach (var item in element.EnumerateArray())
                    {
                        list.Add(RedactJsonElement(item));
                    }
                    return list;

                case JsonValueKind.String:
                    return element.GetString();
                case JsonValueKind.Number:
                    if (element.TryGetInt32(out int intValue))
                        return intValue;
                    if (element.TryGetInt64(out long longValue))
                        return longValue;
                    if (element.TryGetDecimal(out decimal decimalValue))
                        return decimalValue;
                    return element.GetDouble();
                case JsonValueKind.True:
                    return true;
                case JsonValueKind.False:
                    return false;
                case JsonValueKind.Null:
                    return null;
                default:
                    return element.ToString();
            }
        }

        private string RedactXmlContent(string xmlContent)
        {
            foreach (var sensitiveKey in _options.SensitiveKeys)
            {
                var pattern = $@"(<{Regex.Escape(sensitiveKey)}[^>]*>)[^<]*(</{Regex.Escape(sensitiveKey)}>)";
                xmlContent = Regex.Replace(xmlContent, pattern, $"$1{_options.RedactionText}$2", RegexOptions.IgnoreCase);
            }
            return xmlContent;
        }

        private string RedactPlainTextContent(string content)
        {
            foreach (var sensitiveKey in _options.SensitiveKeys)
            {
                var pattern = $@"{Regex.Escape(sensitiveKey)}\s*[=:]\s*\S+";
                content = Regex.Replace(content, pattern, $"{sensitiveKey}={_options.RedactionText}", RegexOptions.IgnoreCase);
            }
            return content;
        }
    }
}