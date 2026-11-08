using System;
using System.IO;
using System.Linq;

namespace SoloStone
{
    // Everything a player reads. No WinForms here, so the wording is tested on any machine.
    public enum VerdictTone { Neutral, Warning, Block }
    public static class Texts
    {
        public const string AppName = "SoloStone";
        public const string HowTitle = "How SoloStone works";
        public const string HowBody = "• Plays Hearthstone offline, on your PC.\r\n• Makes a separate copy of your Hearthstone. Your own install is only read, never changed.\r\n• Blocks the copy from the internet.";
        public const string BeforeBody = "• Hearthstone is installed through Battle.net.\r\n• Battle.net and Hearthstone are closed.";
        public const string LegalLine = "Includes no Hearthstone program files, art or sound. Card names and text belong to Blizzard (see NOTICE).";
        public const string FanNotice = "Unofficial fan project, not affiliated with Blizzard. Offline only; use at your own risk.";
        public const string FolderTitle = "Your Hearthstone folder";
        public const string FolderHint = "Not sure where it is? In Battle.net, open Hearthstone, click the gear next to Play, then Show in Explorer.";
        public const string FolderHelp = "Choose the folder where Hearthstone is installed. SoloStone only reads it.";
        public const string FolderBrowse = "Browse…";
        public const string Recheck = "Check again";
        public const string FolderNone = "No Hearthstone folder was found. Choose it with Browse.";
        public const string FolderChecking = "Checking this folder…";
        public const string AcceptWarning = "I understand. Continue with this client anyway.";
        public const string CopyTitle = "Where to put the offline copy";
        public const string CopyHelp = "The copy takes a lot of space. It must be a new or empty folder, outside your Hearthstone folder.";
        public const string ReviewTitle = "What will happen";
        public const string ReviewUpdates = "Check for updates when SoloStone starts";
        public const string ReviewUpdatesOff = "Updates are not set up in this build, so there is nothing to check.";
        public const string RunTitle = "Setting up SoloStone";
        public const string Install = "Install";
        public const string Next = "Next";
        public const string Back = "Back";
        public const string Cancel = "Cancel";
        public const string Close = "Close";
        public const string Retry = "Try again";
        public const string AskAgain = "Ask again";
        public const string RemovePartial = "Remove partial copy";
        public const string OpenLog = "Open log";
        public const string OpenSoloStone = "Open SoloStone";
        public const string Play = "Play";
        public const string Skip = "Skip update";
        public const string InstallUpdate = "Install this update";
        public const string RollBack = "Roll back";
        public const string RestoreBackup = "Restore backup";
        public const string Uninstall = "Uninstall…";
        public const string FixFirewall = "Reapply internet block";
        public const string UpdatesSwitch = "Check for updates";
        public const string PlayWarning = "I understand. Play with this client anyway.";
        public const string UninstallAsk = "Remove SoloStone and its copy of Hearthstone? Your own Hearthstone is not touched. Saves are kept unless you choose to delete them.";
        // SoloStone records only the files it must check, not all 12 GB, so it can't tell what a player added to the copy.
        public static string UninstallAskFor(string client) => UninstallAsk + (string.IsNullOrEmpty(client) ? "" : " Everything in " + client + " is deleted, including anything you added there. Move your own files out first.");
        public const string UninstallSaves = "Also delete my saves. This cannot be undone.";
        public const string UninstallGo = "Uninstall";
        public const string UninstallKeep = "Keep SoloStone";
        public const string NotInstalled = "SoloStone is not set up yet.";

        public static string ReviewBody(string source, string target, bool updatesConfigured) =>
            "• Copy and check Hearthstone from " + source + " to " + target + ".\r\n"
            + "• Add offline files and give the copy separate Windows settings.\r\n"
            + "• Block the copy in Windows Firewall. Allow the Windows permission prompt or SoloStone cannot start.\r\n"
            + (updatesConfigured ? "• If enabled, check GitHub for signed updates at launch. GitHub sees your internet address; SoloStone sends nothing else about you.\r\n" : "")
            + "Your Hearthstone folder, Battle.net and account stay unchanged. Your saves are kept when you uninstall.";
        public static string TestedLine(string tested) => "Latest client we tested with: " + (string.IsNullOrEmpty(tested) ? "unknown" : tested);
        public static string YourLine(string found) => "Your client: " + (string.IsNullOrEmpty(found) ? "unknown" : found);
        public static string SoloStoneLine(string release) => "SoloStone: " + (string.IsNullOrEmpty(release) ? "unknown" : release);
        public static string SpaceLine(long required, long free) =>
            "About " + Gb(required) + " needed. " + Gb(Math.Max(0, free)) + " available on that drive.";
        static string Gb(long bytes) => (bytes / (1024.0 * 1024 * 1024)).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + " GB";
        public static string Version(string version, string build) => string.IsNullOrEmpty(version) || string.IsNullOrEmpty(build) ? version : (build.IndexOf('.') >= 0 ? build : version + "." + build);
        public static VerdictTone VerdictColor(VerdictKind? kind) => kind == VerdictKind.Block ? VerdictTone.Block : kind == VerdictKind.Warning ? VerdictTone.Warning : VerdictTone.Neutral;
        public static string Headline(Verdict v) => v.Kind == VerdictKind.Allow ? "This client works with SoloStone."
            : v.Kind == VerdictKind.Warning ? "SoloStone has not been tested with this Hearthstone version."
            : v.Reasons != null && v.Reasons.Length != 0 ? (v.Reasons[0].StartsWith("This SoloStone release runs on Hearthstone ", StringComparison.Ordinal) ? "Client version " + Version(v.FoundVersion, v.FoundBuild) + " is not supported. Tested version: " + Version(v.TestedVersion, v.TestedBuild) + "." : string.Join(" ", v.Reasons)) : "SoloStone cannot use this client.";
        public static string Detail(Verdict v) {
            if (v == null) return "";
            if (v.Kind == VerdictKind.Block) return "";
            string lead = ClientPolicy.Sentence(Version(v.TestedVersion, v.TestedBuild), Version(v.FoundVersion, v.FoundBuild));
            string message = v.Message ?? "";
            string detail = message.StartsWith(lead, StringComparison.Ordinal) ? message.Substring(lead.Length).Trim() : message;
            if (v.Kind == VerdictKind.Block && v.Reasons != null) foreach (string reason in v.Reasons) detail = detail.Replace(reason, "").Trim();
            if (v.Kind == VerdictKind.Allow && !string.IsNullOrEmpty(v.FoundBuild) && !string.IsNullOrEmpty(v.TestedBuild) && v.FoundBuild.Split('.').Last() != v.TestedBuild.Split('.').Last()) detail = (detail + " Your build differs from the tested build.").Trim();
            return detail;
        }
        public static string LogFile(string root) => Path.Combine(root, "reports", "installer.log");
    }
}
