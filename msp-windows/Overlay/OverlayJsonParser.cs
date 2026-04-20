using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Json;
using System.Text;
using msp_windows.Overlay.Models;

namespace msp_windows.Overlay
{
    public class OverlayJsonParser
    {
        public OverlayDocument Parse(string overlayJson)
        {
            if (string.IsNullOrWhiteSpace(overlayJson)) {
                throw new ArgumentException("overlayJson is empty.", nameof(overlayJson));
            }

            var root = DeserializeToDictionary(overlayJson);

            var doc = new OverlayDocument
            {
                SchemaVersion = GetString(root, "schemaVersion"),
                OverlayId = GetString(root, "overlayId"),
                Name = GetString(root, "name"),
                Platform = GetString(root, "platform"),
                Canvas = ParseCanvas(GetDict(root, "canvas")),
                OverlaySettings = ParseOverlaySettings(GetDictOrNull(root, "overlaySettings")),
                Game = ParseGame(GetDictOrNull(root, "game")),
                Meta = ParseMeta(GetDictOrNull(root, "meta"))
            };

            var elementsObj = GetValue(root, "elements");
            var elementItems = AsObjectEnumerable(elementsObj);
            if (elementItems == null) {
                throw new InvalidDataException("elements is missing or invalid.");
            }

            foreach (var elObj in elementItems) {
                if (!(elObj is Dictionary<string, object> elDict)) {
                    throw new InvalidDataException("Invalid element object.");
                }

                string type = GetString(elDict, "type");
                OverlayElementBase element;
                switch (type) {
                    case "rect":
                        element = ParseRect(elDict);
                        break;
                    case "circle":
                        element = ParseCircle(elDict);
                        break;
                    case "line":
                        element = ParseLine(elDict);
                        break;
                    default:
                        throw new InvalidDataException($"Unsupported element type: {type}");
                }

                doc.Elements.Add(element);
            }

            Validate(doc);
            return doc;
        }

        private static IEnumerable<object> AsObjectEnumerable(object value)
        {
            if (value == null) return null;
            if (value is object[] array) return array;
            if (value is System.Collections.ArrayList arrayList) return arrayList.Cast<object>();
            if (value is List<object> list) return list;
            if (value is IEnumerable<object> enumerable) return enumerable;
            return null;
        }

        private static void Validate(OverlayDocument doc)
        {
            if (doc == null) throw new ArgumentNullException(nameof(doc));

            if (string.IsNullOrWhiteSpace(doc.SchemaVersion)) throw new InvalidDataException("schemaVersion is missing.");
            if (string.IsNullOrWhiteSpace(doc.OverlayId)) throw new InvalidDataException("overlayId is missing.");
            if (string.IsNullOrWhiteSpace(doc.Name)) throw new InvalidDataException("name is missing.");
            if (!string.Equals(doc.Platform, "windows", StringComparison.OrdinalIgnoreCase)) {
                throw new InvalidDataException("Unsupported overlay platform.");
            }

            if (doc.Canvas == null) throw new InvalidDataException("canvas is missing.");
            if (doc.Canvas.BaseWidth <= 0 || doc.Canvas.BaseHeight <= 0) throw new InvalidDataException("canvas size is invalid.");

            if (doc.OverlaySettings == null) doc.OverlaySettings = new OverlaySettings { Opacity = 1.0 };
            if (doc.OverlaySettings.Opacity < 0.0 || doc.OverlaySettings.Opacity > 1.0) throw new InvalidDataException("overlaySettings.opacity is invalid.");
        }

        private static OverlayCanvas ParseCanvas(Dictionary<string, object> canvas)
        {
            if (canvas == null) throw new InvalidDataException("canvas is missing.");

            return new OverlayCanvas
            {
                BaseWidth = GetDouble(canvas, "baseWidth"),
                BaseHeight = GetDouble(canvas, "baseHeight")
            };
        }

        private static OverlaySettings ParseOverlaySettings(Dictionary<string, object> settings)
        {
            if (settings == null) {
                return new OverlaySettings { Opacity = 1.0 };
            }

            return new OverlaySettings
            {
                Opacity = GetDoubleOrDefault(settings, "opacity", 1.0)
            };
        }

        private static OverlayGame ParseGame(Dictionary<string, object> game)
        {
            if (game == null) return null;

            return new OverlayGame
            {
                Id = GetLongOrDefault(game, "id", 0),
                Name = GetStringOrNull(game, "name")
            };
        }

        private static OverlayMeta ParseMeta(Dictionary<string, object> meta)
        {
            if (meta == null) return null;

            return new OverlayMeta
            {
                CreatedAt = GetStringOrNull(meta, "createdAt"),
                UpdatedAt = GetStringOrNull(meta, "updatedAt")
            };
        }

        private static RectElement ParseRect(Dictionary<string, object> el)
        {
            return new RectElement
            {
                Id = GetStringOrNull(el, "id"),
                Type = GetString(el, "type"),
                X = GetDouble(el, "x"),
                Y = GetDouble(el, "y"),
                Width = GetDouble(el, "width"),
                Height = GetDouble(el, "height"),
                Rotation = GetDoubleOrDefault(el, "rotation", 0),
                Opacity = GetDoubleOrDefault(el, "opacity", 1.0),
                ZIndex = GetIntOrDefault(el, "zIndex", 0),
                Visible = GetBoolOrDefault(el, "visible", true),
                Locked = GetBoolOrDefault(el, "locked", false),
                FillColor = GetStringOrNull(el, "fillColor"),
                StrokeColor = GetStringOrNull(el, "strokeColor"),
                StrokeWidth = GetDoubleOrDefault(el, "strokeWidth", 0),
                CornerRadius = GetDoubleOrDefault(el, "cornerRadius", 0)
            };
        }

        private static CircleElement ParseCircle(Dictionary<string, object> el)
        {
            return new CircleElement
            {
                Id = GetStringOrNull(el, "id"),
                Type = GetString(el, "type"),
                X = GetDouble(el, "x"),
                Y = GetDouble(el, "y"),
                Width = GetDouble(el, "width"),
                Height = GetDouble(el, "height"),
                Rotation = GetDoubleOrDefault(el, "rotation", 0),
                Opacity = GetDoubleOrDefault(el, "opacity", 1.0),
                ZIndex = GetIntOrDefault(el, "zIndex", 0),
                Visible = GetBoolOrDefault(el, "visible", true),
                Locked = GetBoolOrDefault(el, "locked", false),
                FillColor = GetStringOrNull(el, "fillColor"),
                StrokeColor = GetStringOrNull(el, "strokeColor"),
                StrokeWidth = GetDoubleOrDefault(el, "strokeWidth", 0)
            };
        }

        private static LineElement ParseLine(Dictionary<string, object> el)
        {
            return new LineElement
            {
                Id = GetStringOrNull(el, "id"),
                Type = GetString(el, "type"),
                X1 = GetDouble(el, "x1"),
                Y1 = GetDouble(el, "y1"),
                X2 = GetDouble(el, "x2"),
                Y2 = GetDouble(el, "y2"),
                Opacity = GetDoubleOrDefault(el, "opacity", 1.0),
                ZIndex = GetIntOrDefault(el, "zIndex", 0),
                Visible = GetBoolOrDefault(el, "visible", true),
                Locked = GetBoolOrDefault(el, "locked", false),
                StrokeColor = GetStringOrNull(el, "strokeColor"),
                StrokeWidth = GetDoubleOrDefault(el, "strokeWidth", 1.0),
                DashStyle = GetStringOrNull(el, "dashStyle")
            };
        }

        private static Dictionary<string, object> DeserializeToDictionary(string json)
        {
            var serializer = new DataContractJsonSerializer(typeof(Dictionary<string, object>), new DataContractJsonSerializerSettings
            {
                UseSimpleDictionaryFormat = true
            });

            using (var ms = new MemoryStream(Encoding.UTF8.GetBytes(json)))
            {
                var obj = serializer.ReadObject(ms);
                if (!(obj is Dictionary<string, object> dict)) {
                    throw new InvalidDataException("Invalid overlay JSON root.");
                }
                return dict;
            }
        }

        private static object GetValue(Dictionary<string, object> dict, string key)
        {
            if (dict == null) throw new ArgumentNullException(nameof(dict));
            if (!dict.TryGetValue(key, out var v)) {
                throw new InvalidDataException($"Missing field: {key}");
            }
            return v;
        }

        private static Dictionary<string, object> GetDict(Dictionary<string, object> dict, string key)
        {
            var v = GetValue(dict, key);
            if (v is Dictionary<string, object> d) return d;
            throw new InvalidDataException($"Invalid object field: {key}");
        }

        private static Dictionary<string, object> GetDictOrNull(Dictionary<string, object> dict, string key)
        {
            if (dict == null) return null;
            if (!dict.TryGetValue(key, out var v)) return null;
            return v as Dictionary<string, object>;
        }

        private static string GetString(Dictionary<string, object> dict, string key)
        {
            var v = GetValue(dict, key);
            return v?.ToString();
        }

        private static string GetStringOrNull(Dictionary<string, object> dict, string key)
        {
            if (dict == null) return null;
            if (!dict.TryGetValue(key, out var v)) return null;
            return v?.ToString();
        }

        private static double GetDouble(Dictionary<string, object> dict, string key)
        {
            var v = GetValue(dict, key);
            return ToDouble(v, key);
        }

        private static double GetDoubleOrDefault(Dictionary<string, object> dict, string key, double defaultValue)
        {
            if (dict == null) return defaultValue;
            if (!dict.TryGetValue(key, out var v) || v == null) return defaultValue;
            return ToDouble(v, key);
        }

        private static int GetIntOrDefault(Dictionary<string, object> dict, string key, int defaultValue)
        {
            if (dict == null) return defaultValue;
            if (!dict.TryGetValue(key, out var v) || v == null) return defaultValue;

            if (v is int i) return i;
            if (v is long l) return (int)l;
            if (v is double d) return (int)d;
            if (int.TryParse(v.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)) return parsed;
            return defaultValue;
        }

        private static long GetLongOrDefault(Dictionary<string, object> dict, string key, long defaultValue)
        {
            if (dict == null) return defaultValue;
            if (!dict.TryGetValue(key, out var v) || v == null) return defaultValue;

            if (v is long l) return l;
            if (v is int i) return i;
            if (v is double d) return (long)d;
            if (long.TryParse(v.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)) return parsed;
            return defaultValue;
        }

        private static bool GetBoolOrDefault(Dictionary<string, object> dict, string key, bool defaultValue)
        {
            if (dict == null) return defaultValue;
            if (!dict.TryGetValue(key, out var v) || v == null) return defaultValue;

            if (v is bool b) return b;
            if (bool.TryParse(v.ToString(), out var parsed)) return parsed;
            return defaultValue;
        }

        private static double ToDouble(object v, string key)
        {
            if (v is double d) return d;
            if (v is float f) return f;
            if (v is int i) return i;
            if (v is long l) return l;
            if (v is decimal m) return (double)m;

            if (double.TryParse(v.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)) {
                return parsed;
            }

            throw new InvalidDataException($"Invalid number field: {key}");
        }
    }
}
