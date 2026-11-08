using System;
using System.IO;
using System.Linq;
using System.Text;
using SoloStone;

static partial class Program
{
    // Helpers for tools/remote/installer_live.py. They make and use throwaway minisign-format keys so a live run needs no extra install.
    //   tool keygen OUTDIR NAME              writes NAME.pub (public key line) and NAME.key (secret seed, keep out of the repo)
    //   tool sign KEYFILE FILE COMMENT [OUT] writes a minisign signature file (default FILE.minisig)
    static int Tool(string[] args) {
        try {
            if (args.Length == 3 && args[0] == "keygen") {
                Directory.CreateDirectory(args[1]);
                var key = new TestKey();
                File.WriteAllText(Path.Combine(args[1], args[2] + ".pub"), key.Public + "\n");
                string secret = Path.Combine(args[1], args[2] + ".key");
                File.WriteAllText(secret, "seed=" + key.SeedText + "\nid=" + Convert.ToBase64String(key.Id) + "\n");
                if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(secret, UnixFileMode.UserRead | UnixFileMode.UserWrite);
                Console.WriteLine(key.Public); return 0;
            }
            if ((args.Length == 4 || args.Length == 5) && args[0] == "sign") {
                var lines = File.ReadAllLines(args[1]).Select(l => l.Split(new[] { '=' }, 2)).ToDictionary(p => p[0], p => p[1]);
                var key = new TestKey(Convert.FromBase64String(lines["seed"]), Convert.FromBase64String(lines["id"]));
                byte[] signature = key.Sign(File.ReadAllBytes(args[2]), args[3], true, "SoloStone live test key");
                File.WriteAllBytes(args.Length == 5 ? args[4] : args[2] + ".minisig", signature); return 0;
            }
            if (args.Length == 2 && args[0] == "check-app") {
                // The release rules over a staged app folder, before the loader is added: no client file, no stray program.
                string app = Path.GetFullPath(args[1]);
                var files = Directory.GetFiles(app, "*", SearchOption.AllDirectories).Select(p => p.Substring(app.Length + 1).Replace(Path.DirectorySeparatorChar, '/')).ToArray();
                string problem = PayloadRules.AppProblem(files.Where(f => !f.StartsWith("payload/", StringComparison.Ordinal)))
                    ?? PayloadRules.Problem(new[] { "BepInEx/plugins/SoloStone.Plugin.dll", "BepInEx/plugins/SabberStoneCore.dll" }, null);
                if (problem != null) { Console.Error.WriteLine(problem); return 1; }
                Console.WriteLine("App rules passed (" + files.Length + " files)."); return 0;
            }
            if ((args.Length == 3 || args.Length == 4) && args[0] == "check-release") {
                // The same check build.ps1 runs through the launcher, for a build folder copied to this machine.
                string problem = ReleaseScan.ReleaseProblem(Path.GetFullPath(args[1]), args[2], args.Length == 3);
                if (problem != null) { Console.Error.WriteLine(problem); return 1; }
                Console.WriteLine("Release check passed: " + args[2] + "."); return 0;
            }
            if (args.Length == 2 && args[0] == "check-manifest") {
                // The launcher's own reading of a draft release-manifest.json (tools/release_build.py assemble).
                byte[] bytes = File.ReadAllBytes(args[1]);
                if (bytes.Length > ReleaseManifest.MaxBytes) throw new InvalidOperationException("Release manifest has an invalid size.");
                var manifest = JsonBytes.Parse<ReleaseManifest>(bytes);
                if (manifest == null) throw new InvalidOperationException("Release manifest is invalid.");
                manifest.Validate();
                Console.WriteLine("Manifest valid: version " + manifest.Latest.Version + ", seq " + manifest.Seq + ", asset " + manifest.Latest.Asset.Name); return 0;
            }
            if (args.Length == 4 && args[0] == "verify") {
                // tool verify FILE SIGNATURE PUBLIC_KEY: the launcher's own minisign check. Prints the trusted comment when the signature holds.
                Console.WriteLine(Minisign.Verify(File.ReadAllBytes(args[1]), File.ReadAllBytes(args[2]), MinisignPublicKey.Parse(args[3]))); return 0;
            }
            if (args.Length > 0 && args[0] == "mono-guard") return MonoGuard.Run(args.Skip(1).ToArray());
            if (args.Length > 0 && args[0] == "bindings") return BindingScan.Run(args.Skip(1).ToArray());
            Console.Error.WriteLine("Usage: tool mono-guard ... | tool bindings ... | tool keygen OUTDIR NAME | tool sign KEYFILE FILE COMMENT [OUT] | tool check-app APPDIR | tool check-release OUTDIR VERSION [--no-setup] | tool check-manifest FILE | tool verify FILE SIGNATURE PUBLIC_KEY"); return 2;
        } catch (Exception e) { Console.Error.WriteLine(e.Message); return 1; }
    }
}
