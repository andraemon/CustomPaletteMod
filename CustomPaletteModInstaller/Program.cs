using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using UndertaleModLib;
using UndertaleModLib.Decompiler;
using UndertaleModLib.Models;

namespace CustomPaletteMod
{
    class Program
    {
        const string DefaultIni = @"[palette]
name='MODERNUM'
palette00='96'
palette01='236'
palette02='198'
palette10='202'
palette11='88'
palette12='90'
palette20='25'
palette21='32'
palette22='54'
palette30='20'
palette31='20'
palette32='20'
normal='0'
bright='2'
background='3'
water='4'
";

        static UndertaleData Data;

        private static void Main()
        {
            Console.WriteLine("==Downwell Custom Palette Mod Installer==");
            Console.WriteLine("Open the game folder, what do you see?");
            Console.WriteLine("1 - One file named Downwell.exe and no file named data.win.");
            Console.WriteLine("2 - Many files, one of which is named data.win.");
            Console.Write("Input: ");
            var key = Console.ReadKey(false);
            Console.WriteLine();
            bool singleexe = false;
            string datawinpath;
            if (key.Key == ConsoleKey.D2)
            {
                Console.WriteLine("Please drag data.win onto this window and press enter:");
                datawinpath = Console.ReadLine();
            }
            else if (key.Key == ConsoleKey.D1)
            {
                Console.WriteLine("Please start the game, and press any key.");
                Console.ReadKey(true);
                Console.WriteLine("Copying IXP000.TMP...");
                if (!CopyIXPFolder())
                {
                    Console.WriteLine("Cannot detect or copy the game's folder, contact the mod creator.");
                    Console.ReadKey(true);
                    return;
                }

                // We copied the folder without any issues.
                datawinpath = Environment.GetEnvironmentVariable("TEMP") + Path.DirectorySeparatorChar + "ForPatchTemp" + Path.DirectorySeparatorChar + "data.win";

                singleexe = true;
            }
            else
            {
                Console.WriteLine("Unknown input, relaunch this program and try again.");
                Console.ReadKey(true);
                return;
            }

            if (!File.Exists(datawinpath))
            {
                Console.WriteLine("What? Somehow data.win is missing, contact the mod creator.");
                Console.ReadKey(true);
                return;
            }

            Console.WriteLine("Close the game and press any key.");
            Console.ReadKey(true);

            // Ok, we can finally patch!!!
            Console.WriteLine("Loading data.win using UndertaleModLib...");
            bool quit = false;
            try
            {
                using var stream = new FileStream(datawinpath, FileMode.Open, FileAccess.Read);
                Data = UndertaleIO.Read(stream, warning =>
                {
                    Console.WriteLine("[MODLIB|WARN]: " + warning);
                    quit = true;
                });
            }
            catch (Exception e)
            {
                Console.WriteLine("[MODLIB|ERR]: " + e.Message);
                quit = true;
            }

            if (quit)
            {
                Console.WriteLine("Warnings or errors occured when loading data.win!");
                Console.WriteLine("Press any key to exit...");
                Console.ReadKey(true);
                Environment.Exit(-1);
            }

            Console.WriteLine("Patching data.win...");
            PatchThing();
            Console.WriteLine();
            Console.WriteLine("Writing new data.win...");
            File.Delete(datawinpath);
            using (var stream = new FileStream(datawinpath, FileMode.Create, FileAccess.Write))
            {
                UndertaleIO.Write(stream, Data);
            }
            Console.WriteLine("Writing palette.ini...");
            File.WriteAllText(Path.GetDirectoryName(datawinpath) + Path.DirectorySeparatorChar + "palette.ini", DefaultIni);
            if (singleexe)
            {
                FindDownwellExe(new DirectoryInfo(Path.GetDirectoryName(datawinpath)));
                Process.Start("explorer.exe", Path.GetDirectoryName(datawinpath));
                Console.WriteLine("Copy all files from this folder to your game folder.");
                Console.WriteLine("If it asks if you want to replace files, agree, then press any key.");
                Console.ReadKey(true);
            }
            Console.WriteLine("Done! Launch the game and press any key to exit.");
            Console.ReadKey(true);
            Console.WriteLine("Cleaning up...");
            if (singleexe) Directory.Delete(Path.GetDirectoryName(datawinpath), true);
        }

        static bool CopyIXPFolder()
        {
            string tempPathToIXP = Environment.GetEnvironmentVariable("TEMP") + Path.DirectorySeparatorChar + "IXP000.TMP" + Path.DirectorySeparatorChar;
            string newPath = Environment.GetEnvironmentVariable("TEMP") + Path.DirectorySeparatorChar + "ForPatchTemp" + Path.DirectorySeparatorChar;
            if (!Directory.Exists(tempPathToIXP)) return false;
            if (Directory.Exists(newPath))
            {
                try
                {
                    Directory.Delete(newPath);
                }
                catch { }
            }

            bool failed = false;
            try
            {
                DirectoryCopy(tempPathToIXP, newPath, false);
            }
            catch
            {
                failed = true;
            }
            if (failed || !Directory.Exists(newPath)) return false;

            return true;
        }

        static void ScriptError(string body, string title)
        {
            Console.WriteLine(string.Format("[{0}]: {1}", title, body));
            Console.ReadKey(true);
            Environment.Exit(-1);
        }

        // Basically a copied-over .csx file...
        static void PatchThing()
        {
            if (Data.GeneralInfo.DisplayName.Content == "Downwell [Custom Palette Mod]")
                ScriptError("The mod is already applied! Please verify the integrity of your game files through Steam and try again.", "SCRIPTMSG|ERR");
            else
                Data.GeneralInfo.DisplayName.Content = "Downwell [Custom Palette Mod]";

            //Create custom shader
            UndertaleSimpleList<UndertaleShader.VertexShaderAttribute> vsa = new UndertaleSimpleList<UndertaleShader.VertexShaderAttribute>
            {
                new UndertaleShader.VertexShaderAttribute() { Name = Data.Strings.MakeString("in_Position") },
                new UndertaleShader.VertexShaderAttribute() { Name = Data.Strings.MakeString("in_Colour") },
                new UndertaleShader.VertexShaderAttribute() { Name = Data.Strings.MakeString("in_TextureCoord") }
            };
            UndertaleShader.ShaderType type = UndertaleShader.ShaderType.GLSL_ES;
            string name = "shaderCustom";
            string glslesfragment = "precision mediump float;\n#define LOWPREC lowp\nuniform vec3 colorL;\r\nuniform vec3 colorM;\r\nuniform vec3 colorD;\r\nuniform vec3 colorS;\r\nuniform sampler2D gm_BaseTexture;\r\nuniform bool gm_PS_FogEnabled;\r\nuniform vec4 gm_FogColour;\r\nuniform bool gm_AlphaTestEnabled;\r\nuniform float gm_AlphaRefValue;\r\nvoid DoAlphaTest(vec4 SrcColour)\r\n{\r\n	if (gm_AlphaTestEnabled)\r\n	{\r\n		if (SrcColour.a <= gm_AlphaRefValue)\r\n		{\r\n			discard;\r\n		}\r\n	}\r\n}\r\n\r\nvoid DoFog(inout vec4 SrcColour, float fogval)\r\n{\r\n	if (gm_PS_FogEnabled)\r\n	{\r\n		SrcColour = mix(SrcColour, gm_FogColour, clamp(fogval, 0.0, 1.0)); \r\n	}\r\n}\r\n\r\n#define _YY_GLSLES_ 1\nvarying vec2 v_vTexcoord;\r\nvarying vec4 v_vColour;\r\n\r\nvoid main()\r\n{\r\n\r\n  vec2 texCoord = v_vTexcoord;\r\n  vec4 colour;\r\n  vec3 red = vec3(1.0,0.0,0.0);\r\n  vec3 white = vec3(1.0,1.0,1.0);\r\n  vec3 black = vec3(0.0,0.0,0.0);\r\n  vec3 green = vec3(0.0,1.0,0.0);\r\n  vec3 blue = vec3(0.0/256.0,126.0/256.0,256.0/256.0);\r\n  \r\n\r\n  \r\n  colour = texture2D(gm_BaseTexture, texCoord );\r\n    \r\n  colour.r = step(0.5,colour.r);\r\n  //colour.g = step(0.5,colour.g);\r\n  colour.b = step(0.5,colour.b);\r\n  colour.a = step(0.5,colour.a);\r\n  \r\n  \r\n\r\n  //\r\n\r\n\r\n  \r\n  \r\n\r\n  \r\n  float whiteOrNot = step(0.8,colour.g);\r\n  float blueOrNot = step(0.8,colour.b)-whiteOrNot;  \r\n  float redOrNot = step(0.5,colour.r - colour.g);\r\n  float blackOrNot = step(0.5,1.0 - colour.r - colour.g - colour.b);\r\n  \r\n  colour.rgb = mix(colour.rgb,colorD,step(0.5,blackOrNot));\r\n  colour.rgb = mix(colour.rgb,colorM,step(0.5,redOrNot));\r\n  colour.rgb = mix(colour.rgb,colorS,step(0.5,blueOrNot));\r\n  colour.rgb = mix(colour.rgb,colorL,step(0.5,whiteOrNot));\r\n  \r\n  \r\n  \r\n  \r\n\r\n\r\n  gl_FragColor = colour;\r\n\r\n}\r\n";
            string glslfragment = "#version 120\n#define LOWPREC \nuniform vec3 colorL;\r\nuniform vec3 colorM;\r\nuniform vec3 colorD;\r\nuniform vec3 colorS;\r\nuniform sampler2D gm_BaseTexture;\r\nuniform bool gm_PS_FogEnabled;\r\nuniform vec4 gm_FogColour;\r\nuniform bool gm_AlphaTestEnabled;\r\nuniform float gm_AlphaRefValue;\r\nvoid DoAlphaTest(vec4 SrcColour)\r\n{\r\n	if (gm_AlphaTestEnabled)\r\n	{\r\n		if (SrcColour.a <= gm_AlphaRefValue)\r\n		{\r\n			discard;\r\n		}\r\n	}\r\n}\r\n\r\nvoid DoFog(inout vec4 SrcColour, float fogval)\r\n{\r\n	if (gm_PS_FogEnabled)\r\n	{\r\n		SrcColour = mix(SrcColour, gm_FogColour, clamp(fogval, 0.0, 1.0)); \r\n	}\r\n}\r\n\r\n#define _YY_GLSL_ 1\nvarying vec2 v_vTexcoord;\r\nvarying vec4 v_vColour;\r\n\r\nvoid main()\r\n{\r\n\r\n  vec2 texCoord = v_vTexcoord;\r\n  vec4 colour;\r\n  vec3 red = vec3(1.0,0.0,0.0);\r\n  vec3 white = vec3(1.0,1.0,1.0);\r\n  vec3 black = vec3(0.0,0.0,0.0);\r\n  vec3 green = vec3(0.0,1.0,0.0);\r\n  vec3 blue = vec3(0.0/256.0,126.0/256.0,256.0/256.0);\r\n  \r\n\r\n  \r\n  colour = texture2D(gm_BaseTexture, texCoord );\r\n    \r\n  colour.r = step(0.5,colour.r);\r\n  //colour.g = step(0.5,colour.g);\r\n  colour.b = step(0.5,colour.b);\r\n  colour.a = step(0.5,colour.a);\r\n  \r\n  \r\n\r\n  //\r\n\r\n\r\n  \r\n  \r\n\r\n  \r\n  float whiteOrNot = step(0.8,colour.g);\r\n  float blueOrNot = step(0.8,colour.b)-whiteOrNot;  \r\n  float redOrNot = step(0.5,colour.r - colour.g);\r\n  float blackOrNot = step(0.5,1.0 - colour.r - colour.g - colour.b);\r\n  \r\n  colour.rgb = mix(colour.rgb,colorD,step(0.5,blackOrNot));\r\n  colour.rgb = mix(colour.rgb,colorM,step(0.5,redOrNot));\r\n  colour.rgb = mix(colour.rgb,colorS,step(0.5,blueOrNot));\r\n  colour.rgb = mix(colour.rgb,colorL,step(0.5,whiteOrNot));\r\n  \r\n  \r\n  \r\n  \r\n\r\n\r\n  gl_FragColor = colour;\r\n\r\n}\r\n";
            string hlsl9fragment = "// GameMaker reserved and common types/inputs\r\n\r\nsampler2D gm_BaseTexture : register(S0);\r\n\r\nbool 	gm_PS_FogEnabled;\r\nfloat4 	gm_FogColour;\r\nbool 	gm_AlphaTestEnabled;\r\nfloat4	gm_AlphaRefValue;\r\n// Varyings\nstatic float2 _v_vTexcoord = {0, 0};\nstatic float4 gl_Color[1] =\n{\n    float4(0, 0, 0, 0)\n};\nuniform float3 colorL;\nuniform float3 colorM;\nuniform float3 colorD;\nuniform float3 colorS;\nuniform float _gm_AlphaRefValue : register(c3);\nuniform bool _gm_AlphaTestEnabled : register(c4);\nuniform sampler2D _gm_BaseTexture : register(s0);\nuniform float4 _gm_FogColour : register(c5);\nuniform bool _gm_PS_FogEnabled : register(c6);\nfloat4 gl_texture2D(sampler2D s, float2 t)\n{\n    return tex2D(s, t);\n}\n\n#define GL_USES_FRAG_COLOR\n;\n;\n;\n;\n;\nvoid _DoAlphaTest(in float4 _SrcColour)\n{\n{\nif(_gm_AlphaTestEnabled)\n{\n{\nif((_SrcColour.w <= _gm_AlphaRefValue))\n{\n{\ndiscard;\n;\n}\n;\n}\n;\n}\n;\n}\n;\n}\n}\n;\nvoid _DoFog(inout float4 _SrcColour, in float _fogval)\n{\n{\nif(_gm_PS_FogEnabled)\n{\n{\n(_SrcColour = lerp(_SrcColour, _gm_FogColour, clamp(_fogval, 0.0, 1.0)));\n}\n;\n}\n;\n}\n}\n;\n;\n;\nvoid gl_main()\n{\n{\nfloat2 _texCoord = _v_vTexcoord;\nfloat4 _colour = {0, 0, 0, 0};\nfloat3 _red = float3(1.0, 0.0, 0.0);\nfloat3 _white = float3(1.0, 1.0, 1.0);\nfloat3 _black = float3(0.0, 0.0, 0.0);\nfloat3 _green = float3(0.0, 1.0, 0.0);\nfloat3 _blue = float3(0.0, 0.4921875, 1.0);\n(_colour = gl_texture2D(_gm_BaseTexture, _texCoord));\n(_colour.x = step(0.5, _colour.x));\n(_colour.z = step(0.5, _colour.z));\n(_colour.w = step(0.5, _colour.w));\nfloat _whiteOrNot = step(0.80000001, _colour.y);\nfloat _blueOrNot = (step(0.80000001, _colour.z) - _whiteOrNot);\nfloat _redOrNot = step(0.5, (_colour.x - _colour.y));\nfloat _blackOrNot = step(0.5, (((1.0 - _colour.x) - _colour.y) - _colour.z));\n(_colour.xyz = lerp(_colour.xyz, colorD, step(0.5, _blackOrNot)));\n(_colour.xyz = lerp(_colour.xyz, colorM, step(0.5, _redOrNot)));\n(_colour.xyz = lerp(_colour.xyz, colorS, step(0.5, _blueOrNot)));\n(_colour.xyz = lerp(_colour.xyz, colorL, step(0.5, _whiteOrNot)));\n(gl_Color[0] = _colour);\n}\n}\n;\nstruct PS_INPUT\n{\n    float2 v0 : TEXCOORD0;\n};\n\nstruct PS_OUTPUT\n{\n    float4 gl_Color0 : COLOR0;\n};\n\nPS_OUTPUT main(PS_INPUT input)\n{\n    _v_vTexcoord = input.v0.xy;\n\n    gl_main();\n\n    PS_OUTPUT output;\n    output.gl_Color0 = gl_Color[0];\n\n    return output;\n}\n";
            string glslesvertex = "#define LOWPREC lowp\n#define	MATRIX_VIEW 					0\r\n#define	MATRIX_PROJECTION 				1\r\n#define	MATRIX_WORLD 					2\r\n#define	MATRIX_WORLD_VIEW 				3\r\n#define	MATRIX_WORLD_VIEW_PROJECTION 	4\r\n#define	MATRICES_MAX					5\r\n\r\nuniform mat4 gm_Matrices[MATRICES_MAX]; \r\n\r\nuniform bool gm_LightingEnabled;\r\nuniform bool gm_VS_FogEnabled;\r\nuniform float gm_FogStart;\r\nuniform float gm_RcpFogRange;\r\n\r\n#define MAX_VS_LIGHTS	8\r\n#define MIRROR_WIN32_LIGHTING_EQUATION\r\n\r\n\r\n//#define	MAX_VS_LIGHTS					8\r\nuniform vec4   gm_AmbientColour;							// rgb=colour, a=1\r\nuniform vec4   gm_Lights_Direction[MAX_VS_LIGHTS];		// normalised direction\r\nuniform vec4   gm_Lights_PosRange[MAX_VS_LIGHTS];			// X,Y,Z position,  W range\r\nuniform vec4   gm_Lights_Colour[MAX_VS_LIGHTS];			// rgb=colour, a=1\r\n\r\nfloat CalcFogFactor(vec4 pos)\r\n{\r\n	if (gm_VS_FogEnabled)\r\n	{\r\n		vec4 viewpos = gm_Matrices[MATRIX_WORLD_VIEW] * pos;\r\n		float fogfactor = ((viewpos.z - gm_FogStart) * gm_RcpFogRange);\r\n		return fogfactor;\r\n	}\r\n	else\r\n	{\r\n		return 0.0;\r\n	}\r\n}\r\n\r\nvec4 DoDirLight(vec3 ws_normal, vec4 dir, vec4 diffusecol)\r\n{\r\n	float dotresult = dot(ws_normal, dir.xyz);\r\n	dotresult = max(0.0, dotresult);\r\n\r\n	return dotresult * diffusecol;\r\n}\r\n\r\nvec4 DoPointLight(vec3 ws_pos, vec3 ws_normal, vec4 posrange, vec4 diffusecol)\r\n{\r\n	vec3 diffvec = ws_pos - posrange.xyz;\r\n	float veclen = length(diffvec);\r\n	diffvec /= veclen;	// normalise\r\n#ifdef MIRROR_WIN32_LIGHTING_EQUATION\r\n	// This is based on the Win32 D3D and OpenGL falloff model, where:\r\n	// Attenuation = 1.0f / (factor0 + (d * factor1) + (d*d * factor2))\r\n	// For some reason, factor0 is set to 0.0f while factor1 is set to 1.0f/lightrange (on both D3D and OpenGL)\r\n	// This'll result in no visible falloff as 1.0f / (d / lightrange) will always be larger than 1.0f (if the vertex is within range)\r\n	float atten = 1.0 / (veclen / posrange.w);\r\n	if (veclen > posrange.w)\r\n	{\r\n		atten = 0.0;\r\n	}\r\n#else\r\n	float atten = clamp( (1.0 - (veclen / posrange.w)), 0.0, 1.0);		// storing 1.0f/range instead would save a rcp\r\n#endif\r\n	float dotresult = dot(ws_normal, diffvec);\r\n	dotresult = max(0.0, dotresult);\r\n\r\n	return dotresult * atten * diffusecol;\r\n}\r\n\r\nvec4 DoLighting(vec4 vertexcolour, vec4 objectspacepos, vec3 objectspacenormal)\r\n{\r\n	if (gm_LightingEnabled)\r\n	{\r\n		// Normally we'd have the light positions\\\\directions back-transformed from world to object space\r\n		// But to keep things simple for the moment we'll just transform the normal to world space\r\n		vec4 objectspacenormal4 = vec4(objectspacenormal, 0.0);\r\n		vec3 ws_normal;\r\n		ws_normal = (gm_Matrices[MATRIX_WORLD_VIEW] * objectspacenormal4).xyz;\r\n		ws_normal = -normalize(ws_normal);\r\n\r\n		vec3 ws_pos;\r\n		ws_pos = (gm_Matrices[MATRIX_WORLD] * objectspacepos).xyz;\r\n\r\n		// Accumulate lighting from different light types\r\n		vec4 accumcol = vec4(0.0, 0.0, 0.0, 0.0);		\r\n		for(int i = 0; i < MAX_VS_LIGHTS; i++)\r\n		{\r\n			accumcol += DoDirLight(ws_normal, gm_Lights_Direction[i], gm_Lights_Colour[i]);\r\n		}\r\n\r\n		for(int i = 0; i < MAX_VS_LIGHTS; i++)\r\n		{\r\n			accumcol += DoPointLight(ws_pos, ws_normal, gm_Lights_PosRange[i], gm_Lights_Colour[i]);\r\n		}\r\n\r\n		accumcol *= vertexcolour;\r\n		accumcol += gm_AmbientColour;\r\n		accumcol = min(vec4(1.0, 1.0, 1.0, 1.0), accumcol);\r\n		accumcol.a = vertexcolour.a;\r\n		return accumcol;\r\n	}\r\n	else\r\n	{\r\n		return vertexcolour;\r\n	}\r\n}\r\n\r\n#define _YY_GLSLES_ 1\nattribute vec3 in_Position;                  // (x,y,z)\r\nattribute vec4 in_Colour;                    // (r,g,b,a)\r\nattribute vec2 in_TextureCoord;              // (u,v)\r\n\r\nvarying vec2 v_vTexcoord;\r\nvarying vec4 v_vColour;\r\n\r\nvoid main()\r\n{\r\n    vec4 object_space_pos = vec4( in_Position.x, in_Position.y, in_Position.z, 1.0);\r\n    gl_Position = gm_Matrices[MATRIX_WORLD_VIEW_PROJECTION] * object_space_pos;\r\n    \r\n    v_vColour = in_Colour;\r\n    v_vTexcoord = in_TextureCoord;\r\n}\r\n";
            string glslvertex = "#version 120\n#define LOWPREC \n#define	MATRIX_VIEW 					0\r\n#define	MATRIX_PROJECTION 				1\r\n#define	MATRIX_WORLD 					2\r\n#define	MATRIX_WORLD_VIEW 				3\r\n#define	MATRIX_WORLD_VIEW_PROJECTION 	4\r\n#define	MATRICES_MAX					5\r\n\r\nuniform mat4 gm_Matrices[MATRICES_MAX]; \r\n\r\nuniform bool gm_LightingEnabled;\r\nuniform bool gm_VS_FogEnabled;\r\nuniform float gm_FogStart;\r\nuniform float gm_RcpFogRange;\r\n\r\n#define MAX_VS_LIGHTS	8\r\n#define MIRROR_WIN32_LIGHTING_EQUATION\r\n\r\n\r\n//#define	MAX_VS_LIGHTS					8\r\nuniform vec4   gm_AmbientColour;							// rgb=colour, a=1\r\nuniform vec4   gm_Lights_Direction[MAX_VS_LIGHTS];		// normalised direction\r\nuniform vec4   gm_Lights_PosRange[MAX_VS_LIGHTS];			// X,Y,Z position,  W range\r\nuniform vec4   gm_Lights_Colour[MAX_VS_LIGHTS];			// rgb=colour, a=1\r\n\r\nfloat CalcFogFactor(vec4 pos)\r\n{\r\n	if (gm_VS_FogEnabled)\r\n	{\r\n		vec4 viewpos = gm_Matrices[MATRIX_WORLD_VIEW] * pos;\r\n		float fogfactor = ((viewpos.z - gm_FogStart) * gm_RcpFogRange);\r\n		return fogfactor;\r\n	}\r\n	else\r\n	{\r\n		return 0.0;\r\n	}\r\n}\r\n\r\nvec4 DoDirLight(vec3 ws_normal, vec4 dir, vec4 diffusecol)\r\n{\r\n	float dotresult = dot(ws_normal, dir.xyz);\r\n	dotresult = max(0.0, dotresult);\r\n\r\n	return dotresult * diffusecol;\r\n}\r\n\r\nvec4 DoPointLight(vec3 ws_pos, vec3 ws_normal, vec4 posrange, vec4 diffusecol)\r\n{\r\n	vec3 diffvec = ws_pos - posrange.xyz;\r\n	float veclen = length(diffvec);\r\n	diffvec /= veclen;	// normalise\r\n#ifdef MIRROR_WIN32_LIGHTING_EQUATION\r\n	// This is based on the Win32 D3D and OpenGL falloff model, where:\r\n	// Attenuation = 1.0f / (factor0 + (d * factor1) + (d*d * factor2))\r\n	// For some reason, factor0 is set to 0.0f while factor1 is set to 1.0f/lightrange (on both D3D and OpenGL)\r\n	// This'll result in no visible falloff as 1.0f / (d / lightrange) will always be larger than 1.0f (if the vertex is within range)\r\n	float atten = 1.0 / (veclen / posrange.w);\r\n	if (veclen > posrange.w)\r\n	{\r\n		atten = 0.0;\r\n	}\r\n#else\r\n	float atten = clamp( (1.0 - (veclen / posrange.w)), 0.0, 1.0);		// storing 1.0f/range instead would save a rcp\r\n#endif\r\n	float dotresult = dot(ws_normal, diffvec);\r\n	dotresult = max(0.0, dotresult);\r\n\r\n	return dotresult * atten * diffusecol;\r\n}\r\n\r\nvec4 DoLighting(vec4 vertexcolour, vec4 objectspacepos, vec3 objectspacenormal)\r\n{\r\n	if (gm_LightingEnabled)\r\n	{\r\n		// Normally we'd have the light positions\\\\directions back-transformed from world to object space\r\n		// But to keep things simple for the moment we'll just transform the normal to world space\r\n		vec4 objectspacenormal4 = vec4(objectspacenormal, 0.0);\r\n		vec3 ws_normal;\r\n		ws_normal = (gm_Matrices[MATRIX_WORLD_VIEW] * objectspacenormal4).xyz;\r\n		ws_normal = -normalize(ws_normal);\r\n\r\n		vec3 ws_pos;\r\n		ws_pos = (gm_Matrices[MATRIX_WORLD] * objectspacepos).xyz;\r\n\r\n		// Accumulate lighting from different light types\r\n		vec4 accumcol = vec4(0.0, 0.0, 0.0, 0.0);		\r\n		for(int i = 0; i < MAX_VS_LIGHTS; i++)\r\n		{\r\n			accumcol += DoDirLight(ws_normal, gm_Lights_Direction[i], gm_Lights_Colour[i]);\r\n		}\r\n\r\n		for(int i = 0; i < MAX_VS_LIGHTS; i++)\r\n		{\r\n			accumcol += DoPointLight(ws_pos, ws_normal, gm_Lights_PosRange[i], gm_Lights_Colour[i]);\r\n		}\r\n\r\n		accumcol *= vertexcolour;\r\n		accumcol += gm_AmbientColour;\r\n		accumcol = min(vec4(1.0, 1.0, 1.0, 1.0), accumcol);\r\n		accumcol.a = vertexcolour.a;\r\n		return accumcol;\r\n	}\r\n	else\r\n	{\r\n		return vertexcolour;\r\n	}\r\n}\r\n\r\n#define _YY_GLSL_ 1\nattribute vec3 in_Position;                  // (x,y,z)\r\nattribute vec4 in_Colour;                    // (r,g,b,a)\r\nattribute vec2 in_TextureCoord;              // (u,v)\r\n\r\nvarying vec2 v_vTexcoord;\r\nvarying vec4 v_vColour;\r\n\r\nvoid main()\r\n{\r\n    vec4 object_space_pos = vec4( in_Position.x, in_Position.y, in_Position.z, 1.0);\r\n    gl_Position = gm_Matrices[MATRIX_WORLD_VIEW_PROJECTION] * object_space_pos;\r\n    \r\n    v_vColour = in_Colour;\r\n    v_vTexcoord = in_TextureCoord;\r\n}\r\n";
            string hlsl9vertex = "#define	MATRIX_VIEW 					0\r\n#define	MATRIX_PROJECTION 				1\r\n#define	MATRIX_WORLD 					2\r\n#define	MATRIX_WORLD_VIEW 				3\r\n#define	MATRIX_WORLD_VIEW_PROJECTION 	4\r\n#define	MATRICES_MAX					5\r\n\r\nfloat4x4 	gm_Matrices[MATRICES_MAX] : register(c0);\r\n\r\nbool 	gm_LightingEnabled;\r\nbool 	gm_VS_FogEnabled;\r\nfloat 	gm_FogStart;\r\nfloat 	gm_RcpFogRange;\r\n\r\n#define	MAX_VS_LIGHTS					8\r\nfloat4 gm_AmbientColour;							// rgb=colour, a=1\r\nfloat3 gm_Lights_Direction[MAX_VS_LIGHTS];			// normalised direction\r\nfloat4 gm_Lights_PosRange[MAX_VS_LIGHTS];			// X,Y,Z position,  W range\r\nfloat4 gm_Lights_Colour[MAX_VS_LIGHTS];				// rgb=colour, a=1\r\n\r\nfloat4 vec4(float x0, float x1, float x2, float x3)\n{\n    return float4(x0, x1, x2, x3);\n}\nfloat4 vec4(float3 x0, float x1)\n{\n    return float4(x0, x1);\n}\n// Attributes\nstatic float4 _in_Colour = {0, 0, 0, 0};\nstatic float3 _in_Position = {0, 0, 0};\nstatic float2 _in_TextureCoord = {0, 0};\n\nstatic float4 gl_Position = float4(0, 0, 0, 0);\n\n// Varyings\nstatic float4 _v_vColour = {0, 0, 0, 0};\nstatic float2 _v_vTexcoord = {0, 0};\n\nuniform float4 dx_ViewAdjust : register(c1);\n\nuniform float4 _gm_AmbientColour : register(c2);\nuniform float _gm_FogStart : register(c3);\nuniform bool _gm_LightingEnabled : register(c4);\nuniform float4 _gm_Lights_Colour[8] : register(c5);\nuniform float4 _gm_Lights_Direction[8] : register(c13);\nuniform float4 _gm_Lights_PosRange[8] : register(c21);\nuniform float4x4 _gm_Matrices[5] : register(c29);\nuniform float _gm_RcpFogRange : register(c49);\nuniform bool _gm_VS_FogEnabled : register(c50);\n\n;\n;\n;\n;\n;\n;\n;\n;\n;\nfloat _CalcFogFactor(in float4 _pos)\n{\n{\nif(_gm_VS_FogEnabled)\n{\n{\nfloat4 _viewpos = mul(transpose(_gm_Matrices[3]), _pos);\nfloat _fogfactor = ((_viewpos.z - _gm_FogStart) * _gm_RcpFogRange);\nreturn _fogfactor;\n;\n}\n;\n}\nelse\n{\n{\nreturn 0.0;\n;\n}\n;\n}\n;\n}\n}\n;\nfloat4 _DoDirLight(in float3 _ws_normal, in float4 _dir, in float4 _diffusecol)\n{\n{\nfloat _dotresult = dot(_ws_normal, _dir.xyz);\n(_dotresult = max(0.0, _dotresult));\nreturn (_dotresult * _diffusecol);\n;\n}\n}\n;\nfloat4 _DoPointLight(in float3 _ws_pos, in float3 _ws_normal, in float4 _posrange, in float4 _diffusecol)\n{\n{\nfloat3 _diffvec = (_ws_pos - _posrange.xyz);\nfloat _veclen = length(_diffvec);\n(_diffvec /= _veclen);\nfloat _atten = (1.0 / (_veclen / _posrange.w));\nif((_veclen > _posrange.w))\n{\n{\n(_atten = 0.0);\n}\n;\n}\n;\nfloat _dotresult = dot(_ws_normal, _diffvec);\n(_dotresult = max(0.0, _dotresult));\nreturn ((_dotresult * _atten) * _diffusecol);\n;\n}\n}\n;\nfloat4 _DoLighting(in float4 _vertexcolour, in float4 _objectspacepos, in float3 _objectspacenormal)\n{\n{\nif(_gm_LightingEnabled)\n{\n{\nfloat4 _objectspacenormal4 = vec4(_objectspacenormal, 0.0);\nfloat3 _ws_normal = {0, 0, 0};\n(_ws_normal = mul(transpose(_gm_Matrices[3]), _objectspacenormal4).xyz);\n(_ws_normal = (-normalize(_ws_normal)));\nfloat3 _ws_pos = {0, 0, 0};\n(_ws_pos = mul(transpose(_gm_Matrices[2]), _objectspacepos).xyz);\nfloat4 _accumcol = float4(0.0, 0.0, 0.0, 0.0);\n{for(int _i = 0; (_i < 8); (_i++))\n{\n{\n(_accumcol += _DoDirLight(_ws_normal, _gm_Lights_Direction[_i], _gm_Lights_Colour[_i]));\n}\n;}\n}\n;\n{for(int _i = 0; (_i < 8); (_i++))\n{\n{\n(_accumcol += _DoPointLight(_ws_pos, _ws_normal, _gm_Lights_PosRange[_i], _gm_Lights_Colour[_i]));\n}\n;}\n}\n;\n(_accumcol *= _vertexcolour);\n(_accumcol += _gm_AmbientColour);\n(_accumcol = min(float4(1.0, 1.0, 1.0, 1.0), _accumcol));\n(_accumcol.w = _vertexcolour.w);\nreturn _accumcol;\n;\n}\n;\n}\nelse\n{\n{\nreturn _vertexcolour;\n;\n}\n;\n}\n;\n}\n}\n;\n;\n;\n;\n;\n;\nvoid gl_main()\n{\n{\nfloat4 _object_space_pos = vec4(_in_Position.x, _in_Position.y, _in_Position.z, 1.0);\n(gl_Position = mul(transpose(_gm_Matrices[4]), _object_space_pos));\n(_v_vColour = _in_Colour);\n(_v_vTexcoord = _in_TextureCoord);\n}\n}\n;\nstruct VS_INPUT\n{\n    float4 _in_Colour : COLOR0;\n    float3 _in_Position : POSITION;\n    float2 _in_TextureCoord : TEXCOORD0;\n};\n\nstruct VS_OUTPUT\n{\n    float4 gl_Position : POSITION;\n    float2 v0 : TEXCOORD0;\n};\n\nVS_OUTPUT main(VS_INPUT input)\n{\n    _in_Colour = (input._in_Colour);\n    _in_Position = (input._in_Position);\n    _in_TextureCoord = (input._in_TextureCoord);\n\n    gl_main();\n\n    VS_OUTPUT output;\n    output.gl_Position.x = gl_Position.x;\n    output.gl_Position.y = gl_Position.y;\n    output.gl_Position.z = gl_Position.z;\n    output.gl_Position.w = gl_Position.w;\n    output.v0 = _v_vTexcoord;\n\n    return output;\n}\n";

            UndertaleShader customShader = new UndertaleShader
            {
                Name = Data.Strings.MakeString(name),
                Type = type,
                VertexShaderAttributes = vsa,
                GLSL_ES_Fragment = Data.Strings.MakeString(glslesfragment),
                GLSL_ES_Vertex = Data.Strings.MakeString(glslesvertex),
                GLSL_Fragment = Data.Strings.MakeString(glslfragment),
                GLSL_Vertex = Data.Strings.MakeString(glslvertex),
                HLSL9_Fragment = Data.Strings.MakeString(hlsl9fragment),
                HLSL9_Vertex = Data.Strings.MakeString(hlsl9vertex)
            };

            Data.Shaders.Add(customShader);

            //Add global variables
            UndertaleCode globalStuff = Data.Code.ByName("gml_Script_scrGlobalStuff");
            string globalStuffString = "global.paletteName = 'CUSTOM'\r\nglobal.palette[0, 0] = 0\r\nglobal.palette[0, 1] = 0\r\nglobal.palette[0, 2] = 0\r\nglobal.palette[1, 0] = 0\r\nglobal.palette[1, 1] = 0\r\nglobal.palette[1, 2] = 0\r\nglobal.palette[2, 0] = 0\r\nglobal.palette[2, 1] = 0\r\nglobal.palette[2, 2] = 0\r\nglobal.palette[3, 0] = 0\r\nglobal.palette[3, 1] = 0\r\nglobal.palette[3, 2] = 0\r\nglobal.unlockGoalMax = 0";

            globalStuff.AppendGML(globalStuffString, Data);

            //Load palette variables when save loaded, create random save entries if they don't exist
            UndertaleCode loadSave = Data.Code.ByName("gml_Script_scrLoadSave");

            string loadSaveString = @"
                ini_open('palette.ini')
                if (!ini_section_exists('palette'))
                {
                    ini_write_string('palette', 'name', 'CUSTOM')
                    ini_write_real('palette', 'normal', 0)
                    ini_write_real('palette', 'bright', 1)
                    ini_write_real('palette', 'background', 2)
                    ini_write_real('palette', 'water', 3)
                    for (i = 0; i <= 3; i += 1)
                    {
                        for (j = 0; j <= 2; j += 1)
                            global.palette[i, j] = ini_write_real('palette', (('palette' + string(i)) + string(j)), irandom(255))
                    }
                }
                global.paletteName = ini_read_string('palette', 'name', 'CUSTOM')
                for (i = 0; i <= 3; i += 1)
                {
                    for (j = 0; j <= 2; j += 1)
                        global.palette[i, j] = ini_read_real('palette', (('palette' + string(i)) + string(j)), 0)
                }
                ini_close()";

            loadSave.AppendGML(loadSaveString, Data);

            //Add custom shader to shader list
            UndertaleCode shaderList = Data.Code.ByName("gml_Script_scrShaderListInit");
            string shaderListString = Decompiler.Decompile(shaderList, new DecompileContext(Data, false));
            int shaderListIndex = shaderListString.IndexOf("\"HELL\"") + 6;

            shaderListString = shaderListString.Insert(shaderListIndex, @"
                i += 1
                global.shaderAr[i, 0] = shaderCustom
                global.shaderAr[i, 1] = global.paletteName");
            shaderListString = shaderListString.Replace('\u0022', '\u0027');

            shaderList.ReplaceGML(shaderListString, Data);

            //Improve palette selection screen
            UndertaleCode shaderMenu = Data.Code.ByName("gml_Object_ShaderMenu_Draw_0");
            string shaderMenuString = Decompiler.Decompile(shaderMenu, new DecompileContext(Data, false));

            shaderMenuString = shaderMenuString.Replace("draw_sprite(sprPlayerRunExg, runFrame, ((vx + 80) - 8), ((((vy + 72) + 16) + wholePowan) + 8))", "draw_sprite(sprPlayerRunExg, runFrame, ((vx + 80) - 16), ((((vy + 72) + 16) + wholePowan) + 8))");
            shaderMenuString = shaderMenuString.Replace("draw_sprite(sprGemM, runFrame, ((vx + 80) + 8), (((((vy + 72) + 16) + wholePowan) + 8) + 2))", @"draw_sprite(sprGemM, runFrame, (vx + 80), (((((vy + 72) + 16) + wholePowan) + 8) + 2))
                draw_sprite(sprBubbleSmall, runFrame, ((vx + 80) + 15), (((((vy + 72) + 16) + wholePowan) + 8) + 2))");
            shaderMenuString = shaderMenuString.Replace('\u0022', '\u0027');

            shaderMenu.ReplaceGML(shaderMenuString, Data);

            //Edit draw function
            UndertaleCode draw64 = Data.Code.ByName("gml_Object_objControlerN_Draw_64");
            string draw64String = Decompiler.Decompile(draw64, new DecompileContext(Data, false));
            int draw64Index = draw64String.IndexOf("shader_set(global.shaderAr[global.shaderType, 0])") + 49;

            draw64String = draw64String.Insert(draw64Index, @"
                if (global.shaderAr[global.shaderType, 1] == global.paletteName)
                {
                    shaderL = shader_get_uniform(shaderCustom, 'colorL')
                    shaderM = shader_get_uniform(shaderCustom, 'colorM')
                    shaderD = shader_get_uniform(shaderCustom, 'colorD')
                    shaderS = shader_get_uniform(shaderCustom, 'colorS')
                    shader_set_uniform_f(shaderL, (global.palette[0, 0] / 256), (global.palette[0, 1] / 256), (global.palette[0, 2] / 256))
                    shader_set_uniform_f(shaderM, (global.palette[1, 0] / 256), (global.palette[1, 1] / 256), (global.palette[1, 2] / 256))
                    shader_set_uniform_f(shaderD, (global.palette[2, 0] / 256), (global.palette[2, 1] / 256), (global.palette[2, 2] / 256))
                    shader_set_uniform_f(shaderS, (global.palette[3, 0] / 256), (global.palette[3, 1] / 256), (global.palette[3, 2] / 256))
                }");
            draw64String = draw64String.Replace('\u0022', '\u0027');

            draw64.ReplaceGML(draw64String, Data);

            //Press Ctrl + R to randomize custom palette colors
            UndertaleCode controllerStep = Data.Code.ByName("gml_Object_objControlerN_Step_1");

            controllerStep.AppendGML(@"
                if (keyboard_check(vk_control))
                {
                    if (keyboard_check_pressed(ord('R')))
	                {
                        ini_open('palette.ini')
	                    for (i = 0; i <= 3; i += 1)
		                {
			                for (j = 0; j <= 2; j += 1)
                            {
				                global.palette[i, j] = ini_write_real('palette', (('palette' + string(i)) + string(j)), irandom(255))
				                global.palette[i, j] = ini_read_real('palette', (('palette' + string(i)) + string(j)), 0)
                            }
		                }
                        ini_close()
	                }
                    if (keyboard_check_pressed(ord('P')))
	                {
                        ini_open('palette.ini')
                        global.paletteName = ini_read_string('palette', 'name', 'CUSTOM')
                        global.shaderAr[38, 1] = global.paletteName
	                    for (i = 0; i <= 3; i += 1)
		                {
			                for (j = 0; j <= 2; j += 1)
                            {
				                global.palette[i, j] = ini_read_real('palette', (('palette' + string(i)) + string(j)), 0)
                            }
		                }
                        ini_close()
	                }
                }", Data);

            //Fix up unlock checker to be more extensible
            UndertaleCode unlockInit = Data.Code.ByName("gml_Script_unlockInit");
            string unlockInitString = Decompiler.Decompile(unlockInit, new DecompileContext(Data, false));

            unlockInitString = unlockInitString.Replace("41", "global.unlockGoalMax");
            unlockInitString = unlockInitString.Replace('\u0022', '\u0027');

            unlockInit.ReplaceGML(unlockInitString, Data);

            //Fix up unlock list to be more extensible
            UndertaleCode unlockGoal = Data.Code.ByName("gml_Script_unlockGoalNum");
            unlockGoal.ReplaceGML(@"i = 0
                tng[i] = 1000
                i += 1
                tng[i] = 2000
                i += 1
                tng[i] = 3000
                i += 1
                tng[i] = 4000
                i += 1
                tng[i] = 5000
                i += 1
                tng[i] = 7500
                i += 1
                tng[i] = 10000
                i += 1
                tng[i] = 12500
                i += 1
                tng[i] = 15000
                i += 1
                tng[i] = 17500
                i += 1
                tng[i] = 20000
                i += 1
                tng[i] = 25000
                i += 1
                tng[i] = 30000
                i += 1
                tng[i] = 35000
                i += 1
                tng[i] = 40000
                i += 1
                tng[i] = 45000
                i += 1
                tng[i] = 50000
                i += 1
                tng[i] = 55000
                i += 1
                tng[i] = 60000
                i += 1
                tng[i] = 65000
                i += 1
                tng[i] = 70000
                i += 1
                tng[i] = 75000
                i += 1
                tng[i] = 80000
                i += 1
                tng[i] = 85000
                i += 1
                tng[i] = 90000
                i += 1
                tng[i] = 95000
                i += 1
                tng[i] = 100000
                i += 1
                tng[i] = 110000
                i += 1
                tng[i] = 120000
                i += 1
                tng[i] = 130000
                i += 1
                tng[i] = 140000
                i += 1
                tng[i] = 150000
                i += 1
                tng[i] = 175000
                i += 1
                tng[i] = 200000
                i += 1
                tng[i] = 225000
                i += 1
                tng[i] = 250000
                i += 1
                tng[i] = 300000
                i += 1
                tng[i] = 350000
                i += 1
                tng[i] = 400000
                i += 1
                tng[i] = 450000
                i += 1
                tng[i] = 500000
                i += 1
                tng[i] = 600000
                i += 1
                tng[i] = 99999999999
                global.unlockGoalMax = i
                if (global.totalGems >= 99999999999)
                    global.totalGems = 99999999990", Data);
        }

        static void FindDownwellExe(DirectoryInfo dir)
        {
            // Get the files in the directory and copy them to the new location.
            FileInfo[] files = dir.GetFiles("*.exe");
            try
            {
                File.Move(files[0].FullName, files[0].DirectoryName + Path.DirectorySeparatorChar + "Downwell.exe");
            }
            catch (Exception e)
            {
                Console.WriteLine("[PATCHER|ERR]: Error when renaming Downwell.exe! " + e.Message);
            }
        }

        // Taken from https://docs.microsoft.com/dotnet/standard/io/how-to-copy-directories
        private static void DirectoryCopy(string sourceDirName, string destDirName, bool copySubDirs)
        {
            // Get the subdirectories for the specified directory.
            DirectoryInfo dir = new DirectoryInfo(sourceDirName);

            if (!dir.Exists)
            {
                throw new DirectoryNotFoundException(
                    "Source directory does not exist or could not be found: "
                    + sourceDirName);
            }

            DirectoryInfo[] dirs = dir.GetDirectories();
            // If the destination directory doesn't exist, create it.
            if (!Directory.Exists(destDirName))
            {
                Directory.CreateDirectory(destDirName);
            }

            // Get the files in the directory and copy them to the new location.
            FileInfo[] files = dir.GetFiles();
            foreach (FileInfo file in files)
            {
                string temppath = Path.Combine(destDirName, file.Name);
                file.CopyTo(temppath, false);
            }

            // If copying subdirectories, copy them and their contents to new location.
            if (copySubDirs)
            {
                foreach (DirectoryInfo subdir in dirs)
                {
                    string temppath = Path.Combine(destDirName, subdir.Name);
                    DirectoryCopy(subdir.FullName, temppath, copySubDirs);
                }
            }
        }
    }
}