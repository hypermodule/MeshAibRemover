using CUE4Parse.UE4.Assets;
using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Assets.Exports.SkeletalMesh;
using CUE4Parse.UE4.Assets.Exports.StaticMesh;
using CUE4Parse.UE4.Readers;
using CUE4Parse.UE4.Versions;

namespace MeshAibRemover;

public record ByteAsset(string Name, byte[] Bytes)
{
    public static ByteAsset Read(string path) => new(path, File.ReadAllBytes(path));
}

public class Program
{
    private static void WriteLong(List<byte> bytes, int offset, long value)
    {
        var longBytes = BitConverter.GetBytes(value);

        if (!BitConverter.IsLittleEndian)
        {
            Array.Reverse(longBytes);
        }

        for (var i = 0; i < longBytes.Length; i++)
        {
            bytes[offset + i] = longBytes[i];
        }
    }

    private static Package ParseVanillaAsset(ByteAsset uasset, ByteAsset uexp)
    {
        var versions = new VersionContainer(EGame.GAME_UE4_26);
        var uassetArchive = new FByteArchive(uasset.Name, uasset.Bytes, versions);
        var uexpArchive = new FByteArchive(uexp.Name, uexp.Bytes, versions);
        return new Package(uassetArchive, uexpArchive, (FArchive?)null);
    }

    public static (ByteAsset, ByteAsset) RemoveAdjacencyIndexBuffer(ByteAsset uasset, ByteAsset uexp)
    {
        var package = ParseVanillaAsset(uasset, uexp);

        // Find the mesh export
        int meshExportIndex = -1;
        UObject? meshExport = null;

        for (var i = 0; i < package.ExportMapLength; i++)
        {
            var export = package.ExportsLazy[i].Value;

            if (export is UStaticMesh or USkeletalMesh)
            {
                meshExportIndex = i;
                meshExport = export;
            }
        }

        if (meshExportIndex == -1 || meshExport == null)
        {
            throw new Exception("Could not find mesh export");
        }

        Console.WriteLine("Found mesh export: " + meshExport.Name);

        // Find AdjacencyIndexBuffers
        var aibOffsets = new List<(int startOffset, int endOffset)>();

        if (meshExport is UStaticMesh sm)
        {
            var lodResources = sm.RenderData?.LODs ?? [];

            foreach (var lodResource in lodResources)
            {
                var aibStartOffset = (int)lodResource.AdjacencyIndexBufferStartOffset;
                var aibEndOffset = (int)lodResource.AdjacencyIndexBufferEndOffset;

                if (aibStartOffset > 0)
                {
                    aibOffsets.Add((aibStartOffset, aibEndOffset));
                }
            }
        }
        else if (meshExport is USkeletalMesh skm)
        {
            var lodModels = skm.LODModels ?? [];

            foreach (var lodModel in lodModels)
            {
                var aibStartOffset = (int)lodModel.AdjacencyIndexBufferStartOffset;
                var aibEndOffset = (int)lodModel.AdjacencyIndexBufferEndOffset;

                if (aibStartOffset > 0)
                {
                    aibOffsets.Add((aibStartOffset, aibEndOffset));
                }
            }
        }

        Console.WriteLine($"Found {aibOffsets.Count} AdjacencyIndexBuffer(s) to remove");

        // Filter out AdjacencyIndexBuffers 
        var uexpBytes = new List<byte>(uexp.Bytes.Length);
        for (var i = 0; i < uexp.Bytes.Length; i++)
        {
            foreach (var (startOffset, endOffset) in aibOffsets)
            {
                if (startOffset <= i && i < endOffset)
                {
                    goto Outer;
                }
            }

            uexpBytes.Add(uexp.Bytes[i]);

        Outer:
            continue;
        }
        
        var sizeDecrease = uexp.Bytes.Length - uexpBytes.Count;

        Console.WriteLine($"Stripped {sizeDecrease} AdjacencyIndexBuffer bytes from mesh export");

        // Update export table with new size of main export
        var mainExport = package.ExportMap[meshExportIndex];
        var newSerialSize = mainExport.SerialSize - sizeDecrease;
        var uassetBytes = uasset.Bytes.ToList();
        WriteLong(uassetBytes, (int)mainExport.OffsetOfSerialSize, newSerialSize);

        Console.WriteLine($"Updated mesh export's SerialSize from {mainExport.SerialSize} to {newSerialSize}");

        // Update export table with new offsets for any subsequent exports
        for (var i = meshExportIndex + 1; i < package.ExportMapLength; i++)
        {
            var objectExport = package.ExportMap[i];
            var serialOffset = objectExport.SerialOffset;
            // SerialOffset comes immediately after SerialSize (which is a long, i.e. 8 bytes)
            var offsetOfSerialOffset = (int)(objectExport.OffsetOfSerialSize + 8);
            var newSerialOffset = serialOffset - sizeDecrease;
            WriteLong(uassetBytes, offsetOfSerialOffset, newSerialOffset);

            Console.WriteLine($"Updated SerialOffset of export {i} from {serialOffset} to {newSerialOffset}");
        }

        // Update bulk data start offset
        if (package.Summary.BulkDataStartOffset > 0)
        {
            var newBulkDataStartOffset = package.Summary.BulkDataStartOffset - sizeDecrease;
            WriteLong(uassetBytes, (int)package.Summary.OffsetOfBulkDataStartOffset, newBulkDataStartOffset);

            Console.WriteLine($"Updated BulkDataStartOffset from {package.Summary.BulkDataStartOffset} to {newBulkDataStartOffset}");
        }

        return (new ByteAsset(uasset.Name, uassetBytes.ToArray()), new ByteAsset(uexp.Name, uexpBytes.ToArray()));
    }

    public static void Main(string[] args)
    {
        if (args.Length != 1)
        {
            Console.Error.WriteLine("Usage: MeshAibRemover UASSET_FILE");
            Environment.Exit(1);
        }

        var uassetPath = args[0];
        var uexpPath = Path.ChangeExtension(uassetPath, ".uexp");

        Console.WriteLine("Parsing asset ...");

        var uasset = ByteAsset.Read(uassetPath);
        var uexp = ByteAsset.Read(uexpPath);

        var (newUasset, newUexp) = RemoveAdjacencyIndexBuffer(uasset, uexp);

        try
        {
            Console.WriteLine("Renaming original files to .bak");

            File.Move(uassetPath, Path.ChangeExtension(uassetPath, ".uasset.bak"));
            File.Move(uexpPath, Path.ChangeExtension(uexpPath, ".uexp.bak"));
        }
        catch (IOException e)
        {
            Console.Error.WriteLine("Failed to rename to .bak: " + e.Message);
        }

        File.WriteAllBytes(uassetPath, newUasset.Bytes);
        File.WriteAllBytes(uexpPath, newUexp.Bytes);

        Console.WriteLine("Wrote " + Path.GetFileName(uassetPath));
        Console.WriteLine("Wrote " + Path.GetFileName(uexpPath));

        Console.WriteLine("Press any key to exit...");
        Console.ReadKey();
    }
}