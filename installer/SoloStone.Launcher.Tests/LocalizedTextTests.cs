using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
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
    static Dictionary<string,(string Key,string Text)> JsonEntries(JsonElement element) { var result=new Dictionary<string,(string,string)>(); foreach(var member in element.EnumerateObject()) result.Add(member.Name,(member.Value[0].GetString(),member.Value[1].GetString())); return result; }
    static List<string> JsonList(JsonElement element) => element.EnumerateArray().Select(e=>e.GetString()).ToList();

    // Cards the engine rewrites after load (Configure hooks) so that their text no longer says what the card file's English says:
    // the plugin shows these in English. A new entry here is a decision; a new mismatch outside it fails.
    static readonly string[] EngineRewrittenText={"AT_051","DRG_403e","EX1_155a","EX1_155b","EX1_165","EX1_165a","EX1_165t1","EX1_248","NEW1_008","NEW1_008a","NEW1_008b","NEW1_027e","OG_044a","UNG_028t","UNG_067t1","UNG_067t1e","UNG_067t1e2","UNG_817","UNG_920t1","UNG_999t5","UNG_999t5e"};

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

            // Resolve: the client's answer, unless it says English while Options started another language (not settled yet).
            Check(LocalizedCardText.Resolve("ruRU",null,out bool settled)=="ruRU" && settled,"the client's language wins and is settled");
            Check(LocalizedCardText.Resolve("ruRU","deDE",out settled)=="ruRU" && settled,"the client's language beats the Options choice");
            Check(LocalizedCardText.Resolve("enUS",null,out settled)==null && settled,"English with no other language started is settled");
            Check(LocalizedCardText.Resolve("enGB","enUS",out settled)==null && settled,"enGB is English");
            Check(LocalizedCardText.Resolve("enUS","ruRU",out settled)==null && !settled,"English while Options started Russian is not settled: not remembered");
            Check(LocalizedCardText.Resolve(null,"ruRU",out settled)=="ruRU" && !settled && LocalizedCardText.Resolve("Unknown","deDE",out settled)=="deDE" && !settled,"no usable answer from the client: the Options language, not remembered");
            Check(LocalizedCardText.Resolve(null,null,out settled)==null && !settled,"nothing known is English, not remembered");

            // The key: FNV-1a 32 of the trimmed UTF-8, with the engine's " @spelldmg" suffix ignored. tools/gen_card_facts.py writes the same.
            Check(LocalizedCardText.Key("")=="811c9dc5" && LocalizedCardText.Key("a")=="e40c292c" && LocalizedCardText.Key(" \ta\n ")==LocalizedCardText.Key("a"),"the key trims whitespace at both ends");
            Check(LocalizedCardText.Key("Deal $8 damage. @spelldmg")==LocalizedCardText.Key("Deal $8 damage.") && LocalizedCardText.Key("a b")!=LocalizedCardText.Key("a  b") && LocalizedCardText.Key("\u00fc")!=LocalizedCardText.Key("u"),"the engine's spell damage suffix is not part of the text; inner spaces and UTF-8 are");

            // The reader: escapes, unknown header keys skipped, nothing accepted loosely.
            string good="{\"schemaVersion\":2,\"meta\":\"violet-hold-2026-09\",\"locale\":\"deDE\",\"excluded\":[\"A\",[1,{\"k\":null}]],\"n\":-3.5e2,\"text\":{\n\"X\":[\"0000abcd\",\"a\\n\\\"q\\\" \\u00fc \\/ \\\\ \\t\"],\n\"Y\":[\"00000001\",\"\u00fc\u65e5\"]}}\n";
            var read=LocalizedCardText.Read(new MemoryStream(Encoding.UTF8.GetBytes(good)),"deDE");
            Check(read.Count==2 && read["X"].Text=="a\n\"q\" \u00fc / \\ \t" && read["X"].Key=="0000abcd" && read["Y"].Text=="\u00fc\u65e5","escapes and UTF-8 read as JSON does");
            foreach(string bad in new[]{good.Replace("deDE","frFR"),good.Replace("\"schemaVersion\":2","\"schemaVersion\":1"),good.Replace("violet-hold-2026-09","other"),good.Replace("\"text\":{\n","\"x\":{\n"),good.TrimEnd()+"x",good.Substring(0,good.Length-6),good.Replace("\\u00fc","\\u00f"),good.Replace("\\t","\\x"),good.Replace("[\"00000001\",","\"00000001\",")}) {
                bool refused=false; try { LocalizedCardText.Read(new MemoryStream(Encoding.UTF8.GetBytes(bad)),"deDE"); } catch(InvalidDataException) { refused=true; }
                Check(refused,"a wrong, partial or malformed locale file is refused: "+bad.Substring(0,Math.Min(60,bad.Length)));
            }

            // The committed files against the card file.
            using var mainDoc=JsonDocument.Parse(File.ReadAllBytes(FactsFile("violet-hold-2026-09.json")));
            var english=new Dictionary<string,string>();
            foreach(var rows in new[]{mainDoc.RootElement.GetProperty("cards"),mainDoc.RootElement.GetProperty("overlay").GetProperty("cards")})
                foreach(var row in rows.EnumerateArray()) if(row.TryGetProperty("text",out var text) && text.GetString().Length!=0 && !row.TryGetProperty("snapshot",out _)) english[row.GetProperty("id").GetString()]=text.GetString();
            Check(english.Count>9000,"the card file has English text for thousands of distinct ids");
            var allFiles=Directory.GetFiles(Path.Combine(AppContext.BaseDirectory,"facts"),"violet-hold-2026-09*.json").Select(Path.GetFileName).OrderBy(n=>n,StringComparer.Ordinal).ToArray();
            Check(allFiles.SequenceEqual(new[]{"violet-hold-2026-09.json"}.Concat(locales.Select(l=>"violet-hold-2026-09."+l+".json")).OrderBy(n=>n,StringComparer.Ordinal)),"one file per non-English client locale, next to the card file");
            var all=new Dictionary<string,Dictionary<string,(string Key,string Text)>>();
            foreach(string locale in locales) {
                using var doc=JsonDocument.Parse(File.ReadAllBytes(FactsFile("violet-hold-2026-09."+locale+".json")));
                var root=doc.RootElement;
                Check(root.GetProperty("locale").GetString()==locale && root.GetProperty("meta").GetString()=="violet-hold-2026-09" && root.GetProperty("schemaVersion").GetInt32()==2,"the file says which locale it is: "+locale);
                var expected=JsonEntries(root.GetProperty("text")); all[locale]=expected;
                var excluded=JsonList(root.GetProperty("excluded")); var missing=JsonList(root.GetProperty("missing")); var tokens=JsonList(root.GetProperty("tokenMismatch"));
                Check(excluded.Count==0,"no id is excluded from every locale: "+locale);
                Check(english.Keys.Except(excluded).Except(missing).Except(tokens).OrderBy(i=>i,StringComparer.Ordinal).SequenceEqual(expected.Keys.OrderBy(i=>i,StringComparer.Ordinal)),"covers every id with English text, minus this locale's gaps and placeholder mismatches: "+locale);
                Check(missing.Concat(tokens).All(english.ContainsKey) && missing.Count<60 && tokens.Count<20 && expected.Values.All(v=>v.Text.Length!=0),"the gaps are few, real ids; no empty text: "+locale);
                // Staleness without hsdata: every translation is keyed to the English the card file has now.
                Check(expected.All(p=>p.Value.Key==LocalizedCardText.Key(english[p.Key])),"every translation's key matches the card file's English text (regenerate the locale files after a card file change): "+locale);
                var ours=LocalizedCardText.Load(locale);
                Check(ours!=null && ours.Count==expected.Count && expected.All(p=>ours.TryGetValue(p.Key,out var v) && v.Text==p.Value.Text && v.Key==p.Value.Key),"the plugin's reader agrees with System.Text.Json on every entry: "+locale);
            }
            Check(JsonList(JsonDocument.Parse(File.ReadAllBytes(FactsFile("violet-hold-2026-09.ruRU.json"))).RootElement.GetProperty("tokenMismatch")).SequenceEqual(new[]{"DALA_BOSS_45h","BE_854","JAIL_515"}),"ruRU placeholder mismatches, as pinned: DALA_BOSS_45h BE_854 JAIL_515");

            // The real engine: a translation is only good while the engine plays the English it was made from.
            var rewritten=new SortedSet<string>(StringComparer.Ordinal); int compared=0;
            foreach(var pair in all["ruRU"]) {
                var card=SabberStoneCore.Model.Cards.FromId(pair.Key);
                if(card==null) continue;
                compared++;
                if(pair.Value.Key!=LocalizedCardText.Key(card.Text)) rewritten.Add(pair.Key);
            }
            Check(compared>9000,"the engine has the cards the locale files translate");
            Check(rewritten.SequenceEqual(EngineRewrittenText.OrderBy(i=>i,StringComparer.Ordinal)),"ids whose engine text differs from the card file's English must be exactly the known list; new drift fails. Now: "+string.Join(",",rewritten));
            foreach(string locale in locales) {
                var drift=all[locale].Where(p=>{ var card=SabberStoneCore.Model.Cards.FromId(p.Key); return card!=null && p.Value.Key!=LocalizedCardText.Key(card.Text); }).Select(p=>p.Key).ToList();
                Check(drift.All(rewritten.Contains),"no locale has drift the others do not: "+locale);
                foreach(string id in drift) Check(LocalizedCardText.Lookup(locale,id,SabberStoneCore.Model.Cards.FromId(id).Text,out string never)==LocalizedCardText.Status.Stale && never==null,"a rewritten card falls back to English: "+locale+" "+id);
            }
            var engineBolt=SabberStoneCore.Model.Cards.FromId("AT_001");
            Check(LocalizedCardText.Lookup("ruRU","AT_001",engineBolt.Text,out string boltRu)==LocalizedCardText.Status.Localized && boltRu.Any(c=>c>='\u0400' && c<='\u04ff'),"an untouched card reads in Russian (the engine text carries its @spelldmg suffix; the key ignores it)");

            // Lookups.
            string englishToken=english["CAP_405t3"];
            Check(LocalizedCardText.Lookup("ruRU","CAP_405t3",englishToken,out string ru)==LocalizedCardText.Status.Localized && ru!=englishToken && ru.Any(c=>c>='\u0400' && c<='\u04ff'),"a token reads in Russian");
            Check(LocalizedCardText.Lookup("jaJP","AT_001",english["AT_001"],out string ja)==LocalizedCardText.Status.Localized && ja.StartsWith("[x]"),"Japanese keeps the client's raw [x] marker");
            Check(LocalizedCardText.Lookup("frFR","AT_001",english["AT_001"],out string fr)==LocalizedCardText.Status.Localized && fr.Contains("|4("),"French keeps the raw |4(...) plural marker");
            foreach(string none in new[]{"enUS","enGB",null,"xxYY"}) Check(LocalizedCardText.Lookup(none,"AT_001",english["AT_001"],out string unused)==LocalizedCardText.Status.English && unused==null,"English keeps the engine text: "+none);
            Check(LocalizedCardText.Lookup("ruRU","NO_SUCH_CARD","x",out _)==LocalizedCardText.Status.NoEntry && LocalizedCardText.Lookup("ruRU","","x",out _)==LocalizedCardText.Status.NoEntry && LocalizedCardText.Lookup("ruRU",null,"x",out _)==LocalizedCardText.Status.NoEntry,"an unknown id falls back");
            Check(LocalizedCardText.Lookup("ruRU","AT_001","Some other English text.",out string stale)==LocalizedCardText.Status.Stale && stale==null,"other engine English than the file was made from: English");
            Check(LocalizedCardText.Lookup("ruRU","AT_001",english["AT_001"]+"  ",out _)==LocalizedCardText.Status.Localized && LocalizedCardText.Lookup("ruRU","EDR_525B",english["EDR_525B"],out _)==LocalizedCardText.Status.Localized,"whitespace at the ends of the English text is not a difference (EDR_525B has a leading space in the card file)");
            string gap=JsonList(JsonDocument.Parse(File.ReadAllBytes(FactsFile("violet-hold-2026-09.ruRU.json"))).RootElement.GetProperty("missing"))[0];
            Check(LocalizedCardText.Lookup("ruRU",gap,english[gap],out _)==LocalizedCardText.Status.NoEntry,"a card the locale has no text for falls back: "+gap);
            Check(LocalizedCardText.Lookup("ruRU","BE_854",english["BE_854"],out _)==LocalizedCardText.Status.NoEntry,"a translation with other $N than the English was left out (BE_854 ruRU)");

            // Only the running locale is read, once; a missing or broken file is not an error and not retried.
            var opened=new List<string>();
            LocalizedCardText.Open=name=>{ opened.Add(name); return FactsOpener(name); }; LocalizedCardText.Reset();
            LocalizedCardText.Lookup("deDE","AT_001",english["AT_001"],out _); LocalizedCardText.Lookup("deDE","AT_002",english["AT_002"],out _); LocalizedCardText.Lookup("enUS","AT_001","x",out _);
            Check(opened.SequenceEqual(new[]{"SoloStone.Facts.violet-hold-2026-09.deDE.json"}),"English reads no file; a locale file is read once, alone");
            opened.Clear(); LocalizedCardText.Reset();
            LocalizedCardText.Open=name=>{ opened.Add(name); throw new IOException("denied"); };
            Check(LocalizedCardText.Lookup("koKR","AT_001","x",out _)==LocalizedCardText.Status.NoFile && LocalizedCardText.Lookup("koKR","AT_002","x",out _)==LocalizedCardText.Status.NoFile && opened.Count==1,"a file that will not open falls back to English without throwing, and is not retried");
            LocalizedCardText.Open=name=>new MemoryStream(Encoding.UTF8.GetBytes("{\"schemaVersion\":2"));
            LocalizedCardText.Reset();
            Check(LocalizedCardText.Lookup("plPL","AT_001","x",out _)==LocalizedCardText.Status.NoFile,"a truncated file falls back to English without throwing");
            LocalizedCardText.Open=name=>null; LocalizedCardText.Reset();
            Check(LocalizedCardText.Lookup("thTH","AT_001","x",out _)==LocalizedCardText.Status.NoFile,"a build without the file falls back to English");

            // The wiring (a live client is needed for the rest: hslive card-raw-text).
            string bridge=File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"plugin-source","ClientBridge.cs"));
            Check(bridge.Contains("LocalNativeText.TryGetOverride(staged || NativeUIProbe.Active, __0, TextUtils.GetDefaultTextVariant, out string text)") && bridge.Contains("RawCardTextProbe") && bridge.Contains("status == LocalizedCardText.Status.Localized"),"LocalRawCardText still goes through LocalNativeText, and the live probe says whether the text came from the locale file");
            string control=File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"plugin-source","NativeTestControl.cs"));
            string protocol=File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"plugin-source","TestControlProtocol.cs"));
            Check(control.Contains("case \"card-raw-text\"") && protocol.Contains("case \"card-raw-text\": extras = \"cardId\"; Text(\"cardId\"); break;") && !protocol.Substring(protocol.IndexOf("internal bool Mutation",StringComparison.Ordinal),600).Contains("card-raw-text"),"card-raw-text is a read-only test-control query for one card ID");

            LocalizedCardText.Open=FactsOpener; LocalizedCardText.Reset();
            BuiltPluginTextTests(locales);
        } finally { LocalizedCardText.Open=originalOpen; LocalizedCardText.Reset(); }
    }

    // The built plugin (installer/out/plugin-release, from installer/test-all.sh): its embedded locale files, the native-id to
    // engine-id map LocalNativeText builds, and how the client locale is remembered.
    static void BuiltPluginTextTests(string[] locales) {
        string dll=FindOut("out","plugin-release","bin","OfflineProbe","debug","SoloStone.Plugin.dll");
        if(dll==null) { Skip("built plugin text checks: installer/out/plugin-release is not built"); return; }
        var assembly=Assembly.LoadFrom(dll);
        var names=assembly.GetManifestResourceNames().Where(n=>n.StartsWith("SoloStone.Facts.",StringComparison.Ordinal)).OrderBy(n=>n,StringComparer.Ordinal).ToArray();
        Check(names.SequenceEqual(locales.Select(l=>"SoloStone.Facts.violet-hold-2026-09."+l+".json").OrderBy(n=>n,StringComparer.Ordinal)),"the built plugin embeds exactly the 13 locale files");
        foreach(string locale in locales)
            using(var stream=assembly.GetManifestResourceStream("SoloStone.Facts.violet-hold-2026-09."+locale+".json")) Check(stream.Length==new FileInfo(FactsFile("violet-hold-2026-09."+locale+".json")).Length,"embedded uncompressed, byte for byte: "+locale);
        const BindingFlags any=BindingFlags.NonPublic|BindingFlags.Public|BindingFlags.Static;
        var type=assembly.GetType("LocalNativeText",true); var match=assembly.GetType("NativeMatch",true);
        Func<string,string> engineId=id=>(string)type.GetMethod("EngineIdFor",any).Invoke(null,new object[]{id});
        Func<string,string> transport=id=>(string)match.GetMethod("TransportCardId",any,null,new[]{typeof(string)},null).Invoke(null,new object[]{id});
        Check(engineId("HERO_08bp")=="CS2_034" && engineId("HERO_01bp")=="CS2_102" && engineId("AT_001")=="AT_001","transported ids map back to the engine id the card file knows (Fireblast, Armor Up!)");
        string jade=engineId("CFM_712_t07");
        Check(jade!=null && jade.StartsWith("CFM_712_t",StringComparison.Ordinal) && transport(jade)=="CFM_712_t07","a Jade Golem tier maps back to one of its engine sizes");
        var ids=(IReadOnlyCollection<string>)type.GetProperty("SupportedIds",any).GetValue(null);
        Check(ids.Count>5000 && ids.All(id=>engineId(id)!=null && transport(engineId(id))==id),"every native id with text maps to an engine id that transports back to it");
        var raw=type.GetMethod("RawFor",any,null,new[]{typeof(string),typeof(string)},null);
        string english=(string)raw.Invoke(null,new object[]{"HERO_08bp",null});
        string russian=(string)raw.Invoke(null,new object[]{"HERO_08bp","ruRU"});
        var fireblast=LocalizedCardText.Load("ruRU")["CS2_034"];
        Check(english==SabberStoneCore.Model.Cards.FromId("CS2_034").Text && russian==fireblast.Text && russian!=english,"Fireblast (native HERO_08bp) reads from the file's CS2_034 entry in Russian, engine English otherwise");
        Check((string)raw.Invoke(null,new object[]{"UNG_067t1","ruRU"})==(string)raw.Invoke(null,new object[]{"UNG_067t1",null}),"a card the engine rewrites shows English even in Russian");

        // The client locale is remembered once settled, and not while the client says English but Options started another language.
        var clientLocale=type.GetMethod("ClientLocale",any,null,new[]{typeof(Func<string>),typeof(Func<string>)},null);
        var reset=type.GetMethod("ResetLocaleCache",any);
        Func<string,string,(string,int)> ask=(client,running)=>{ reset.Invoke(null,null); int calls=0; Func<string> c=()=>{ calls++; return client; };
            var first=(string)clientLocale.Invoke(null,new object[]{c,(Func<string>)(()=>running)}); var again=(string)clientLocale.Invoke(null,new object[]{c,(Func<string>)(()=>running)});
            Check(first==again,"the same answer twice: "+client+"/"+running); return (first,calls); };
        Check(ask("ruRU","enUS")==("ruRU",1),"a client locale is asked once and remembered");
        Check(ask("enUS",null)==(null,1) && ask("enGB","enUS")==(null,1),"English with no other language started is remembered");
        Check(ask("enUS","ruRU")==(null,2),"English from the client while Options started Russian is asked again, not remembered");
        Check(ask(null,"deDE")==("deDE",2),"no answer from the client: the Options language, asked again");
        reset.Invoke(null,null);
    }
}
