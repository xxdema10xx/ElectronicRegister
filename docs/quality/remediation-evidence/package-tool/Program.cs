using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text.Json;
using var stream = File.OpenRead(args[0]);
using var pe = new PEReader(stream);
var metadata = pe.GetMetadataReader();
var resources = metadata.ManifestResources.Select(handle => metadata.GetString(metadata.GetManifestResource(handle).Name)).Order().ToArray();
var expected = new[] { "mt1", "mt3" }.SelectMany(dir => Directory.GetFiles(Path.Combine("migrations",dir),"*.sql").Select(file => dir.ToUpperInvariant()+"."+Path.GetFileName(file))).Order().ToArray();
if (!resources.SequenceEqual(expected)) throw new InvalidOperationException("Embedded SQL packaging mismatch.");
foreach (var handle in metadata.ManifestResources)
{
    var resource = metadata.GetManifestResource(handle);
    var name = metadata.GetString(resource.Name);
    var location = pe.PEHeaders.CorHeader!.ResourcesDirectory.RelativeVirtualAddress + (int)resource.Offset;
    var data = pe.GetSectionData(location).GetContent();
    var size = System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan());
    var actual = System.Security.Cryptography.SHA256.HashData(data.AsSpan().Slice(4,size));
    var original = File.ReadAllBytes(Path.Combine("migrations",name[..3].ToLowerInvariant(),name[4..]));
    if (!actual.SequenceEqual(System.Security.Cryptography.SHA256.HashData(original)))
        throw new InvalidOperationException("Embedded SQL byte mismatch: " + name);
}
var deps = JsonDocument.Parse(File.ReadAllText(Directory.GetFiles(Path.GetDirectoryName(args[0])!,"*.deps.json").Single()));
if (!deps.RootElement.GetProperty("libraries").EnumerateObject().Any(p => p.Name.Equals("MySqlConnector/2.6.1",StringComparison.OrdinalIgnoreCase)))
    throw new InvalidOperationException("Published driver is not 2.6.1.");
Console.WriteLine(JsonSerializer.Serialize(new { Verified=true, ByteContentsMatch=true, PublishedMySqlConnector="2.6.1", ResourceCount=resources.Length, Resources=resources },new JsonSerializerOptions{WriteIndented=true}));
