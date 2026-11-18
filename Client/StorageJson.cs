using System.Text.Json.Serialization;
using Kernport;

namespace Client;

[JsonSerializable(typeof(Session))]
[JsonSerializable(typeof(List<App.Result>))]
internal partial class StorageJson : JsonSerializerContext;
