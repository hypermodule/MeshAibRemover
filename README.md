# MeshAibRemover

Small script that can make a cooked UE4.26 mesh asset compatible with the game
_Grand Theft Auto: The Trilogy – The Definitive Edition_ (Grove Street Games).

Specifically, the script modifies the `.uexp` file by removing any AdjacencyIndexBuffers in
the mesh's LODs, and then the script updates the export sizes/offsets accordingly in the
`.uasset` file.

## Requirements

MeshAibRemover requires you to have **.NET 8.0 or later installed**. If you don't already
have it, you can [download it here](https://dotnet.microsoft.com/en-us/download/dotnet/8.0/runtime)
(select the **Windows Desktop x64** version).

## Usage

```
MeshAibRemover.exe /Path/To/Mesh.uasset
```

## Example

Follow these steps to replace the vase mesh, which is located at:

```
Gameface/Content/SanAndreas/Environment/INT/SM_int2vase.uasset
```

1. Use Unreal Editor to create a blank UE4.26 project named `Gameface`.

2. In the content browser, create the directory structure of the target asset:

```
SanAndreas/Environment/INT
```

3. Import your static mesh into the newly created `INT` directory and name
it `SM_int2vase`.

4. Place the material at the path of one of the game's built-in materials
(e.g. I will place mine at `Gameface/Content/Common/Materials/MI_Neon_Base`).
(It may be possible to use custom materials instead of doing this; I haven't
tried that.)

5. Cook your mesh by going to clicking on File > Cook Content for Windows.

6. Once the cooking is finished, find the cooked mesh files; they will be located at e.g.

```
C:\Users\user\Documents\Unreal Projects\Gameface\Saved\Cooked\WindowsNoEditor\Gameface\Content\SanAndreas\Environment\INT\SM_int2vase.{uasset,uexp}
```

7. Copy the two asset files (uasset and uexp) to a new directory somewhere, recreating the directory path starting from `Gameface`.
For example, I will copy mine to:

```
C:\Users\user\Desktop\MyMod_P\Gameface\Content\SanAndreas\Environment\INT\SM_int2vase.{uasset,uexp}
```

8. Run MeshAibRemover on the uasset file:

```
MeshAibRemover.exe C:\Users\user\Desktop\MyMod_P\Gameface\Content\SanAndreas\Environment\INT\SM_int2vase.uasset
```

(Alternatively, you can drag and drop the `.uasset` onto `MeshAibRemover.exe`.)

9. Package your mod with e.g. [repak](https://github.com/trumank/repak):

```
cd C:\Users\user\Desktop
repak pack MyMod_P
```

This will produce `MyMod_P.pak`. Copy this file to the game's `Paks` directory.

![example](/example.jpg)

## License and credits

MeshAibRemover is licensed under Apache License 2.0. It uses the third-party library CUE4Parse, which is also
licensed under Apache License 2.0. Many thanks to the CUE4Parse for figuring out the asset serialization tweaks
specific to these games.