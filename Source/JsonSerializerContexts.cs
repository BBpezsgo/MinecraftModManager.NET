
using System.Text.Json.Serialization;
using MMM.Fabric;

namespace MMM;

[JsonSerializable(typeof(List<ModLock>))]
[JsonSourceGenerationOptions(WriteIndented = true)]
public partial class ModLockListJsonSerializerContext : JsonSerializerContext;

[JsonSerializable(typeof(ModList))]
[JsonSourceGenerationOptions(WriteIndented = true)]
public partial class ModListJsonSerializerContext : JsonSerializerContext;

[JsonSerializable(typeof(FabricMod))]
[JsonSourceGenerationOptions(WriteIndented = true)]
public partial class FabricModJsonSerializerContext : JsonSerializerContext;
