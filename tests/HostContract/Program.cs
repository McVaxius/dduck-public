using System.Buffers.Binary;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DDuck.PublicShell;
using XA.Access;
using Host = DDuck.PublicShell.Plugin;

var checks = 0;
void Check(bool condition, string message) { if (!condition) throw new Exception(message); checks++; }
void Reject(Action action, string message)
{
    try { action(); }
    catch { checks++; return; }
    throw new Exception("Accepted invalid input: " + message);
}
Host Create(State state)
{
    State.Current = state;
    return new Host(state.Interface, state.Commands, state.Log, state.Textures);
}

foreach (var fault in new[] { "", "draw", "main", "config", "directory", "validate", "refresh", "load" })
{
    var state = new State { Failure = fault };
    Host? host = null;
    Exception? caught = null;
    try { host = Create(state); } catch (Exception error) { caught = error; }
    if (fault.Length == 0)
    {
        Check(host != null && caught == null, "Host constructs");
        Check(state.ProvidersBeforeLoad, "All three IPC endpoints exist before loading access");
        Check(state.Interface.GetIpcProvider<string>("DDuck.Access.Directory.v1").Callback!() == Path.Combine("synthetic-config", "tasks"), "Directory endpoint");
        Check(!state.Interface.GetIpcProvider<byte[], bool>("DDuck.Access.Validate.v1").Callback!([]), "Validation rejects malformed packages");
        state.Interface.UiBuilder.DrawNow();
        state.Interface.UiBuilder.MainNow();
        state.Interface.UiBuilder.ConfigNow();
        state.Commands.Handlers["/dduck"]("/dduck", "hello");
        state.Commands.Handlers["/dd"]("/dd", "status");
        Check(state.Draws == 1 && state.PrivateDraws == 1, "Draw forwards exactly once");
        Check(state.Opens == 1 && state.CommandsSeen.SequenceEqual(new[] { "/dduck config", "/dduck hello", "/dd status" }), "Both aliases and distinct config action forward");
        Check(state.Interface.GetIpcProvider<bool>("DDuck.Access.Refresh.v1").Callback!(), "Refresh reports loaded module");
        host!.Dispose();
        host.Dispose();
    }
    else Check(ReferenceEquals(caught, state.Original), "Original constructor error: " + fault);
    Check(state.LoaderDisposals == 1, "Loader disposed once: " + fault);
    Check(state.Interface.UiBuilder.Count == 0 && state.Interface.ProviderCount == 0, "Events and IPC removed: " + fault);
    Check(state.Windows == 0 && state.Commands.Handlers.Count == 0, "Windows and both commands removed: " + fault);
}

foreach (var collision in new[] { "/dduck", "/dd" })
{
    var state = new State();
    Action<string, string> other = (_, _) => { };
    state.Commands.Handlers[collision] = other;
    Reject(() => Create(state), "existing command " + collision);
    Check(state.Commands.Handlers.Count == 1 && state.Commands.Handlers[collision] == other, "Other plugin's command preserved");
    Check(!state.Commands.Removals.Contains(collision), "Unowned alias never removed");
    Check(state.LoaderDisposals == 1 && state.Windows == 0, "Command collision rolls back acquired resources");
}

foreach (var removal in new[] { "refresh", "validate", "directory", "config", "main", "draw", "/dduck", "/dd", "windows", "loader" })
{
    var state = new State { CleanupFailure = removal, LogFailure = true };
    var host = Create(state);
    host.Dispose(); host.Dispose();
    Check(state.LoaderDisposals == 1, "Cleanup exception still disposes loader once: " + removal);
    Check(state.Interface.UiBuilder.Count == 0 && state.Interface.ProviderCount == 0 && state.Commands.Handlers.Count == 0 && state.Windows == 0, "Cleanup continues: " + removal);
    Check(state.Warnings == 1, "Logging failure contained: " + removal);
}

var retained = new State { RetainDraw = true };
var retainedHost = Create(retained);
var retainedCommand = retained.Commands.Handlers["/dd"];
var retainedRefresh = retained.Interface.GetIpcProvider<bool>("DDuck.Access.Refresh.v1").Callback!;
retainedHost.Dispose();
retained.Interface.UiBuilder.DrawNow(); retainedCommand("/dd", "start");
Check(!retainedRefresh() && retained.Draws == 0 && retained.PrivateDraws == 0 && retained.CommandsSeen.Count == 0, "Retained callbacks inert after disposal");

var partial = new State { Failure = "refresh", CleanupFailure = "validate", LogFailure = true };
Exception? partialError = null;
try { Create(partial); } catch (Exception error) { partialError = error; }
Check(ReferenceEquals(partialError, partial.Original) && partial.LoaderDisposals == 1 && partial.Interface.ProviderCount == 0 && partial.Interface.UiBuilder.Count == 0, "Rollback failures preserve original error");

var empty = new State { NoModule = true };
using (var emptyHost = Create(empty))
{
    empty.Commands.Handlers["/dd"]("/dd", "status");
    Check(empty.IntroductionOpen, "No-module command opens introduction");
    empty.IntroductionOpen = false; empty.Interface.UiBuilder.ConfigNow();
    Check(empty.IntroductionOpen && !empty.Interface.GetIpcProvider<bool>("DDuck.Access.Refresh.v1").Callback!(), "No-module config and refresh");
}

// Real envelope and manifest code. Only Dalamud/UI/module lifecycle are stubbed.
// An optional owner-supplied key also exercises the host's installed trust anchor.
using var rsa = RSA.Create(3072);
if (args.Length == 1) rsa.ImportFromPem(File.ReadAllText(args[0]));
var publicKey = rsa.ExportSubjectPublicKeyInfoPem();
var plaintext = Encoding.UTF8.GetBytes("Synthetic DDuck contract fixture; no automation code.");
var hostVersion = Version.Parse(BuildInfo.Version);
var json = JsonSerializer.SerializeToUtf8Bytes(new { SchemaVersion = 1, InternalName = "DDuck", AssemblyVersion = "9.8.7.6", PublicFacingVersion = "0.0.0.3", DalamudApiLevel = 15 });

byte[] Package(int schema = 2, string identity = "DDuck", byte[]? manifest = null, int abi = 1, bool includeManifest = true, Version? carrierVersion = null)
{
    var metadata = new byte[32];
    var values = schema == 1 ? new[] { 1, abi, 42, 99, 15, 101, 105, 0 } : new[] { schema, abi, 0, 0, 0, 0, 0, 0 };
    for (var i = 0; i < 8; i++) BinaryPrimitives.WriteInt32LittleEndian(metadata.AsSpan(i * 4), values[i]);
    var key = RandomNumberGenerator.GetBytes(32);
    var mask = RandomNumberGenerator.GetBytes(32);
    var nonce = RandomNumberGenerator.GetBytes(12);
    var payload = new byte[100 + plaintext.Length];
    "XAZENC01"u8.CopyTo(payload); nonce.CopyTo(payload, 8); mask.CopyTo(payload, 20);
    for (var i = 0; i < 32; i++) payload[52 + i] = (byte)(key[i] ^ mask[i]);
    using (var aes = new AesGcm(key, 16)) aes.Encrypt(nonce, plaintext, payload.AsSpan(84, plaintext.Length), payload.AsSpan(payload.Length - 16), metadata);
    CryptographicOperations.ZeroMemory(key);
    var md = new MetadataBuilder();
    md.AddModule(0, md.GetOrAddString(identity + ".Access.dll"), md.GetOrAddGuid(Guid.NewGuid()), default, default);
    md.AddAssembly(md.GetOrAddString(identity + ".Access"), carrierVersion ?? new Version(9, 8, 7, 6), default, default, (AssemblyFlags)0, AssemblyHashAlgorithm.None);
    md.AddTypeDefinition(TypeAttributes.NotPublic, default, md.GetOrAddString("<Module>"), default, MetadataTokens.FieldDefinitionHandle(1), MetadataTokens.MethodDefinitionHandle(1));
    var resources = new BlobBuilder();
    void Resource(string name, byte[] bytes)
    {
        resources.Align(8);
        md.AddManifestResource(ManifestResourceAttributes.Public, md.GetOrAddString(name), default, (uint)resources.Count);
        resources.WriteInt32(bytes.Length); resources.WriteBytes(bytes);
    }
    Resource(identity + ".Payload", payload);
    if (includeManifest) Resource(identity + ".AccessManifest", manifest ?? json);
    var pe = new ManagedPEBuilder(new PEHeaderBuilder(imageCharacteristics: Characteristics.ExecutableImage | Characteristics.Dll), new MetadataRootBuilder(md), new BlobBuilder(), managedResources: resources);
    var carrier = new BlobBuilder(); pe.Serialize(carrier);
    var signed = carrier.ToArray().Concat(metadata).ToArray();
    return signed.Concat(rsa.SignData(signed, HashAlgorithmName.SHA256, RSASignaturePadding.Pss)).Concat("XAZSIG01"u8.ToArray()).ToArray();
}

var valid = Package();
Check(ModulePackage.VerifyAndDecrypt(valid, publicKey, hostVersion).SequenceEqual(plaintext), "Schema 2 authenticated decryption");
Check(PrivateManifest.Validate(valid, json, "DDuck")!.Version == new Version(9, 8, 7, 6), "Arbitrary independent private version");
var hostState = new State();
using (var host = Create(hostState))
    Check(hostState.Interface.GetIpcProvider<byte[], bool>("DDuck.Access.Validate.v1").Callback!(valid) == (args.Length == 1), "Host enforces its own publisher identity");
Reject(() => PrivateManifest.Validate(valid, null, "DDuck"), "missing JSON");
Reject(() => PrivateManifest.Validate(valid, json.Concat(new byte[] { 32 }).ToArray(), "DDuck"), "altered JSON bytes");
Reject(() => PrivateManifest.Validate(valid, Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(json).Replace("9.8.7.6", "9.8.7.5")), "DDuck"), "mismatched JSON");
Reject(() => PrivateManifest.Validate(Package(identity: "Other"), json, "DDuck"), "wrong carrier identity");
Reject(() => PrivateManifest.Validate(Package(carrierVersion: new Version(9, 8, 7, 5)), json, "DDuck"), "carrier version mismatch");
var wrongIdentity = Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(json).Replace("DDuck", "Other"));
Reject(() => PrivateManifest.Validate(Package(manifest: wrongIdentity), wrongIdentity, "DDuck"), "wrong manifest identity");
foreach (var index in new[] { 128, valid.Length - 9, valid.Length - 1 })
{
    var altered = valid.ToArray(); altered[index] ^= 1;
    Reject(() => ModulePackage.VerifyAndDecrypt(altered, publicKey, hostVersion), "modified carrier/signature/footer");
}
using (var otherKey = RSA.Create(3072)) Reject(() => ModulePackage.VerifyAndDecrypt(valid, otherKey.ExportSubjectPublicKeyInfoPem(), hostVersion), "wrong publisher key");
Reject(() => ModulePackage.VerifyAndDecrypt(Package(abi: 2), publicKey, hostVersion), "invalid ABI");
Reject(() => ModulePackage.VerifyAndDecrypt(Package(schema: 3), publicKey, hostVersion), "invalid schema");
Reject(() => ModulePackage.VerifyAndDecrypt(new byte[ModulePackage.MaximumBytes + 1], publicKey, hostVersion), "oversized package");
var legacy = Package(schema: 1, includeManifest: false);
Check(ModulePackage.VerifyAndDecrypt(legacy, publicKey, hostVersion).SequenceEqual(plaintext), "Schema 1 ignores numeric host restrictions");
Check(PrivateManifest.Validate(legacy, null, "DDuck") == null, "Legacy package without sidecar");
Reject(() => PrivateManifest.Validate(legacy, json, "DDuck"), "unbound legacy sidecar");
var duplicate = Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(json).Replace("{", "{\"SchemaVersion\":1,"));
Reject(() => PrivateManifest.Parse(duplicate, "DDuck"), "duplicate JSON fields");
Console.WriteLine($"PASS: {checks} host lifecycle, command ownership, IPC, signature and JSON contract checks. Dalamud/UI/module lifecycle are stubbed; package decoding is real.");
