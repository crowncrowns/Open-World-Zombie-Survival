# Developer reproduction files

These files are ignored by Unity in the distributed Assets folder.

`GrassHDRPChecks.cs` was run from `Assets/Editor` in the isolated `work/AdvancedGrassPainter/HDRPProject`, with HDRP and URP 17.6.0 installed and a copy of the package/demo. Its path guard intentionally refuses the main checkout. Entry point: `GrassHDRPChecks.Run`.

The prepared HDRP shader graph is already included under `Shaders/HDRP~`; normal installation uses `GrassHDRPInstaller` and needs no generator. For reconstructing that graph from C#, place `GrassGraphAuthoring.cs` together with `GraphAuthoring.asmref` under a separate `Assets/GraphAuthoring` in the isolated HDRP project. The assembly reference intentionally targets Shader Graph's assembly, as the authoring API is internal. Entry point: `UnityEditor.ShaderGraph.GrassGraphAuthoring.Generate`. Do not import these developer-only authoring files into the game project.
