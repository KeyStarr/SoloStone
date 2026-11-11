using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

// SoloStone's frozen card text in the client's language (plugin/LocalizedCardText.cs, facts/README.md "Locale files").
static partial class Program
{
    static string FactsFile(string name) => Path.Combine(AppContext.BaseDirectory,"facts",name);
    static Stream FactsOpener(string resource) {
        string path=FactsFile(resource.Substring("SoloStone.Facts.".Length));
        return File.Exists(path) ? File.OpenRead(path) : null;
    }
    static Dictionary<string,string> JsonStrings(JsonElement element) { var result=new Dictionary<string,string>(); foreach(var member in element.EnumerateObject()) result.Add(member.Name,member.Value.GetString()); return result; }

    static void LocalizedTextTests(string temp) {
        var originalOpen=LocalizedCardText.Open;
        try {
            LocalizedCardText.Open=FactsOpener; LocalizedCardText.Reset();
            string[] locales=GameLanguage.Locales.Where(l=>l!="enUS").ToArray();
            Check(locales.Length==13,"13 non-English client locales");
            Check(LocalizedCardText.ResourceName("ruRU")=="SoloStone.Facts.violet-hold-2026-09.ruRU.json","resource name");
            string project=File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"launcher-source","OfflineProbe.csproj"));
            Check(project.Contains("violet-hold-2026-09.*.json") && project.Contains("LogicalName=\"SoloStone.Facts.%(Filename)%(Extension)\""),"the plugin embeds every locale file under the name LocalizedCardText opens");

            // Which locale the text follows.
            foreach(string none in new[]{null,"","enUS","enGB","xxYY","../x","ruru","de-DE"}) Check(LocalizedCardText.Normalize(none)==null,"English and junk follow no locale file: "+none);
            foreach(string locale in locales) Check(LocalizedCardText.Normalize(locale)==locale,"a client locale is kept: "+locale);

            // The reader: escapes, unknown header keys skipped, nothing accepted loosely.
            string good="{\"schemaVersion\":1,\"meta\":\"violet-hold-2026-09\",\"locale\":\"deDE\",\"excluded\":[\"A\",[1,{\"k\":null}]],\"n\":-3.5e2,\"text\":{\n\"X\":\"a\\n\\\"q\\\" \\u00fc \\/ \\\\ \\t\",\n\"Y\":\"\u00fc\u65e5\"}}\n";
            var read=LocalizedCardText.Read(new MemoryStream(Encoding.UTF8.GetBytes(good)),"deDE");
            Check(read.Count==2 && read["X"]=="a\n\"q\" \u00fc / \\ \t" && read["Y"]=="\u00fc\u65e5","escapes and UTF-8 read as JSON does");
            foreach(string bad in new[]{good.Replace("deDE","frFR"),good.Replace("\"schemaVersion\":1","\"schemaVersion\":2"),good.Replace("violet-hold-2026-09","other"),good.Replace(",\"text\":{\n\"X\":\"a\\n\\\"q\\\" \\u00fc \\/ \\\\ \\t\",\n\"Y\":\"\u00fc\u65e5\"}",""),good.TrimEnd()+"x",good.Substring(0,good.Length-6),good.Replace("\\u00fc","\\u00f"),good.Replace("\\t","\\x")})
                {
                    bool refused=false; try { LocalizedCardText.Read(new MemoryStream(Encoding.UTF8.GetBytes(bad)),"deDE"); } catch(InvalidDataException) { refused=true; }
                    Check(refused,"a wrong, partial or malformed locale file is refused: "+bad.Substring(0,Math.Min(60,bad.Length)));
                }

            // The committed files: every card the facts give text for, minus the documented exclusions.
            using var mainDoc=JsonDocument.Parse(File.ReadAllBytes(FactsFile("violet-hold-2026-09.json")));
            var withText=new List<string>();
            foreach(var rows in new[]{mainDoc.RootElement.GetProperty("cards"),mainDoc.RootElement.GetProperty("overlay").GetProperty("cards")})
                foreach(var row in rows.EnumerateArray()) if(row.TryGetProperty("text",out var text) && text.GetString().Length!=0 && !row.TryGetProperty("snapshot",out _)) withText.Add(row.GetProperty("id").GetString());
            Check(withText.Count>9000 && withText.Distinct().Count()==withText.Count,"the card file has English text for thousands of distinct ids");
            var allFiles=Directory.GetFiles(Path.Combine(AppContext.BaseDirectory,"facts"),"violet-hold-2026-09*.json").Select(Path.GetFileName).OrderBy(n=>n,StringComparer.Ordinal).ToArray();
            Check(allFiles.SequenceEqual(new[]{"violet-hold-2026-09.json"}.Concat(locales.Select(l=>"violet-hold-2026-09."+l+".json")).OrderBy(n=>n,StringComparer.Ordinal)),"one file per non-English client locale, next to the card file");
            var exclusions=new List<string>();
            foreach(string locale in locales) {
                using var doc=JsonDocument.Parse(File.ReadAllBytes(FactsFile("violet-hold-2026-09."+locale+".json")));
                var root=doc.RootElement;
                Check(root.GetProperty("locale").GetString()==locale && root.GetProperty("meta").GetString()=="violet-hold-2026-09","the file says which locale it is: "+locale);
                var expected=JsonStrings(root.GetProperty("text"));
                var excluded=root.GetProperty("excluded").EnumerateArray().Select(e=>e.GetString()).ToList();
                var missing=root.GetProperty("missing").EnumerateArray().Select(e=>e.GetString()).ToList();
                if(exclusions.Count==0) exclusions=excluded; else Check(excluded.SequenceEqual(exclusions),"every locale file lists the same exclusions");
                Check(withText.Except(excluded).Except(missing).OrderBy(i=>i,StringComparer.Ordinal).SequenceEqual(expected.Keys.OrderBy(i=>i,StringComparer.Ordinal)),"covers every id with English text, minus exclusions and this locale's gaps: "+locale);
                Check(excluded.Concat(missing).All(withText.Contains) && missing.Count<60 && expected.Values.All(v=>v.Length!=0),"the gaps are few, real ids; no empty text: "+locale);
                var ours=LocalizedCardText.Load(locale);
                Check(ours!=null && ours.Count==expected.Count && expected.All(p=>ours.TryGetValue(p.Key,out var v) && v==p.Value),"the plugin's reader agrees with System.Text.Json on every text: "+locale);
            }
            Check(exclusions.SequenceEqual(new[]{"EDR_525B"}),"the one documented exclusion: EDR_525B (facts text has a leading space the client text lacks)");

            // Lookups.
            using var english=JsonDocument.Parse(File.ReadAllBytes(FactsFile("violet-hold-2026-09.json")));
            string englishToken=null;
            foreach(var row in english.RootElement.GetProperty("overlay").GetProperty("cards").EnumerateArray()) if(row.GetProperty("id").GetString()=="CAP_405t3") englishToken=row.GetProperty("text").GetString();
            Check(englishToken!=null,"CAP_405t3 (a token PR 24 covers) has English text");
            Check(LocalizedCardText.TryGet("ruRU","CAP_405t3",out string ru) && ru!=englishToken && ru.Any(c=>c>='\u0400' && c<='\u04ff'),"a token reads in Russian");
            Check(LocalizedCardText.TryGet("ruRU","AT_001",out string bolt) && bolt.Contains("$8") && bolt.Any(c=>c>='\u0400' && c<='\u04ff'),"a catalog card reads in Russian, with its raw $ markup");
            Check(LocalizedCardText.TryGet("jaJP","AT_001",out string ja) && ja.StartsWith("[x]"),"Japanese keeps the client's raw [x] marker");
            Check(LocalizedCardText.TryGet("frFR","AT_001",out string fr) && fr.Contains("|4("),"French keeps the raw |4(...) plural marker");
            foreach(string none in new[]{"enUS","enGB",null,"xxYY"}) Check(!LocalizedCardText.TryGet(none,"AT_001",out string unused) && unused==null,"English keeps the engine text: "+none);
            Check(!LocalizedCardText.TryGet("ruRU","NO_SUCH_CARD",out _) && !LocalizedCardText.TryGet("ruRU","",out _) && !LocalizedCardText.TryGet("ruRU",null,out _),"an unknown id falls back");
            Check(!LocalizedCardText.TryGet("ruRU","EDR_525B",out _),"an excluded id falls back to English");
            using(var ruDoc=JsonDocument.Parse(File.ReadAllBytes(FactsFile("violet-hold-2026-09.ruRU.json")))) {
                string gap=ruDoc.RootElement.GetProperty("missing")[0].GetString();
                Check(!LocalizedCardText.TryGet("ruRU",gap,out _),"a card the locale has no text for falls back: "+gap);
            }

            // Only the running locale is read, once; a missing or broken file is not an error and not retried.
            var opened=new List<string>();
            LocalizedCardText.Open=name=>{ opened.Add(name); return FactsOpener(name); }; LocalizedCardText.Reset();
            LocalizedCardText.TryGet("deDE","AT_001",out _); LocalizedCardText.TryGet("deDE","AT_002",out _); LocalizedCardText.TryGet("enUS","AT_001",out _);
            Check(opened.SequenceEqual(new[]{"SoloStone.Facts.violet-hold-2026-09.deDE.json"}),"English reads no file; a locale file is read once, alone");
            opened.Clear(); LocalizedCardText.Reset();
            LocalizedCardText.Open=name=>{ opened.Add(name); throw new IOException("denied"); };
            Check(!LocalizedCardText.TryGet("koKR","AT_001",out _) && !LocalizedCardText.TryGet("koKR","AT_002",out _) && opened.Count==1,"a file that will not open falls back to English without throwing, and is not retried");
            LocalizedCardText.Open=name=>new MemoryStream(Encoding.UTF8.GetBytes("{\"schemaVersion\":1"));
            LocalizedCardText.Reset();
            Check(!LocalizedCardText.TryGet("plPL","AT_001",out _),"a truncated file falls back to English without throwing");
            LocalizedCardText.Open=name=>null; LocalizedCardText.Reset();
            Check(!LocalizedCardText.TryGet("thTH","AT_001",out _),"a build without the file falls back to English");

            // The wiring (a live client is needed for the rest: hslive card-raw-text).
            string bridge=File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"plugin-source","ClientBridge.cs"));
            Check(bridge.Contains("LocalNativeText.TryGetOverride(staged || NativeUIProbe.Active, __0, TextUtils.GetDefaultTextVariant, out string text)") && bridge.Contains("RawCardTextProbe"),"LocalRawCardText still goes through LocalNativeText, and the live probe exists");
            string control=File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"plugin-source","NativeTestControl.cs"));
            string protocol=File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"plugin-source","TestControlProtocol.cs"));
            Check(control.Contains("case \"card-raw-text\"") && protocol.Contains("case \"card-raw-text\": extras = \"cardId\"; Text(\"cardId\"); break;") && !protocol.Substring(protocol.IndexOf("internal bool Mutation",StringComparison.Ordinal),600).Contains("card-raw-text"),"card-raw-text is a read-only test-control query for one card ID");
        } finally { LocalizedCardText.Open=originalOpen; LocalizedCardText.Reset(); }
    }
}
