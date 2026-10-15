using System.Text;
using System.Text.Json;
using AstralParty.Toys.Services;
using AstralParty.Toys.Tests.Support;

namespace AstralParty.Toys.Tests;

public sealed class CharacterBundleCatalogTests
{
    [Theory]
    [InlineData("StreamingAssets/aa")]
    [InlineData("com.unity.addressables")]
    public void Resolve_FindsInstalledBundlesInBothUnityLayouts(string layout)
    {
        using var sandbox = new TestSandbox("character-bundle-layout");
        string game = Path.Combine(sandbox.Root, "game");
        string cache = Path.Combine(sandbox.Root, "cache");
        const string hash = "0123456789abcdef0123456789abcdef";
        string resourceRoot = Path.GetFullPath(Path.Combine(game, "AstralParty_CN_Data", layout));
        string bundle = Path.Combine(resourceRoot, "AssetBundles", hash, hash, "__data");
        Directory.CreateDirectory(Path.GetDirectoryName(bundle)!);
        Directory.CreateDirectory(cache);
        File.WriteAllText(bundle, "bundle bytes are only indexed, not decoded");
        WriteCatalog(Path.Combine(cache, "catalog_3.3.0.json"), "TestCharacter", hash);

        var catalog = new CharacterBundleCatalog(game, cache);

        Assert.Equal(new[] { bundle }, catalog.Resolve(new[] { "TestCharacter" }));
    }

    private static void WriteCatalog(string path, string key, string hash)
    {
        using var keyData = new MemoryStream();
        using (var writer = new BinaryWriter(keyData, Encoding.UTF8, true))
        {
            writer.Write((byte)0);
            writer.Write(Encoding.ASCII.GetByteCount(key));
            writer.Write(Encoding.ASCII.GetBytes(key));
        }
        static byte[] Ints(params int[] values)
        {
            using var stream = new MemoryStream();
            using (var writer = new BinaryWriter(stream, Encoding.UTF8, true))
                foreach (int value in values) writer.Write(value);
            return stream.ToArray();
        }
        File.WriteAllText(path, JsonSerializer.Serialize(new
        {
            m_KeyDataString = Convert.ToBase64String(keyData.ToArray()),
            m_BucketDataString = Convert.ToBase64String(Ints(1, 0, 1, 0)),
            m_EntryDataString = Convert.ToBase64String(Ints(1, 0, 0, -1, 0, 0, 0, 0)),
            m_InternalIds = new[] { "https://local.invalid/" + hash + ".bundle" }
        }));
    }
}
