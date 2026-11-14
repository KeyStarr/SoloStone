using System.Reflection;
static void Check(bool ok, string label) { if (!ok) throw new Exception(label); Console.WriteLine("PASS " + label); }
var allBoards = new[] { 1, 2, 3, 4, 5, 6, 7 };
Check(BoardChoice.Allowlist.Length > 0 && BoardChoice.Allowlist.All(id => id > 0), "board allowlist holds positive ids");
Check(Enumerable.Range(0, 1000).All(seed => BoardChoice.Allowlist.Contains(BoardChoice.Pick(seed, Enumerable.Range(1, 200), 0))),
    "default pick stays inside the shipped allowlist");
foreach (var vector in new[] { (0, 1), (1, 7), (42, 6), (-1, 7), (int.MinValue, 3), (int.MaxValue, 7) })
    Check(BoardChoice.Pick(vector.Item1, allBoards, 0, allBoards) == vector.Item2,
        "stable board for seed " + vector.Item1);
Check(BoardChoice.Pick(0, Array.Empty<int>(), 0, allBoards) == 1
    && BoardChoice.Pick(42, new[] { 20, 21 }, 0, allBoards) == 1, "no eligible boards falls back to one");
Check(BoardChoice.Pick(42, new[] { 2, 4, 20 }, 20, allBoards) == 20, "present forced board bypasses allowlist");
var seenBoards = new HashSet<int>();
var seenSubset = new HashSet<int>();
for (int seed = 0; seed < 10000; seed++)
{
    int board = BoardChoice.Pick(seed, allBoards, 0, allBoards);
    int subset = BoardChoice.Pick(seed, new[] { 20, 7, 2, 4, 2, 7 }, 0, allBoards);
    if (!allBoards.Contains(board) || !new[] { 2, 4, 7 }.Contains(subset))
        throw new Exception("board must be allowlisted and present for seed " + seed);
    if (BoardChoice.Pick(seed, allBoards.Reverse().Concat(allBoards), 0, allBoards) != board
        || BoardChoice.Pick(seed, new[] { 2, 4, 7 }, 0, allBoards) != subset)
        throw new Exception("board must ignore input order and duplicates for seed " + seed);
    if (BoardChoice.Pick(seed, allBoards, 20, allBoards) != board
        || BoardChoice.Pick(seed, allBoards, -1, allBoards) != board)
        throw new Exception("absent or nonpositive override must be ignored for seed " + seed);
    seenBoards.Add(board);
    seenSubset.Add(subset);
}
Check(seenBoards.SetEquals(allBoards) && seenSubset.SetEquals(new[] { 2, 4, 7 }),
    "seeds zero through 9999 cover every eligible board, skipping missing boards");
HandCardStateProjectionTests.Run();
PackFabledProjectionTests.Run();
CraftingProjectionTests.Run();
PremiumProjectionTests.Run();
CosmeticsProjectionTests.Run();
MedalProjectionTests.Run();
var type = Assembly.GetExecutingAssembly().GetType("NativeUIProjection");
Check(type != null, "native projection is implemented");
var supported=(IEnumerable<string>)type!.GetField("Supported",BindingFlags.Static|BindingFlags.NonPublic)!.GetValue(null)!;
CardStateProjectionTests.Run();
Check(LocalCardCatalog.Current.Count == 252
    && supported.Count() == 252 + LocalCoreProgression.AvailableCards.Count
    && new HashSet<string>(supported, StringComparer.Ordinal).SetEquals(
        LocalCardCatalog.Current.Concat(LocalCoreProgression.AvailableCards).Select(card => card.Id)),
    "supported pool is exact Classic catalog plus engine-supported Core unlocks");
Check(LocalCardCatalog.LegacyGeneratedPublicIds.SequenceEqual(new[] {
    "CS2_tk1", "CS2_mirror", "CS2_101t", "EX1_110t", "skele21"
}), "legacy generated audit closure is exact");
object Call(string name, params object[] args) => (type!.GetMethod(name, BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic)
    ?? throw new MissingMethodException(type.FullName, name)).Invoke(null, args)!;
void Reject(string name, params object[] args) { try { Call(name,args); throw new Exception("accepted invalid " + name); } catch(TargetInvocationException e) { Check(e.InnerException is ArgumentException || e.InnerException is InvalidOperationException,"reject invalid " + name); } }
Check((long)Call("DeckId","custom") == 910000001L,"custom ID resolves stable reserved native ID");
Check((long)Call("DeckId","paladin-control") == 910000009L,"last loaner ID resolves stable reserved native ID");
Reject("DeckId","unknown");
Check((bool)Call("IsPersonalId",910000001L),"migrated personal ID remains valid");
Check((bool)Call("IsPersonalId",910000010L),"allocated personal ID is valid");
Check(!(bool)Call("IsPersonalId",910000002L),"loaner ID cannot become personal");
Check(!(bool)Call("IsPersonalId",0L),"zero cannot become personal");
var receipt = new[] {"CS2_179","EX1_019","EX1_007","EX1_561","CS2_087"};
Check(string.Join(",",(string[])Call("Receipt",new object[]{receipt})) == "CS2_179,EX1_019,EX1_007,EX1_561,CS2_087","receipt order preserved literally");
Reject("Receipt",new object[]{new[]{"CS2_179"}});
Reject("Receipt",new object[]{new[]{"CS2_179","CS2_179","CS2_179","CS2_179","BAD"}});
var deck=LocalDecks.Get("paladin");
var owned=deck.CardIds.GroupBy(x=>x).ToDictionary(g=>g.Key,g=>g.Count());
Call("ValidateSelection","custom",deck.Hero,deck.CardIds.ToArray(),owned);
Check(true,"owned exact thirty custom accepted");
owned["CS2_087"]=1;
Reject("ValidateSelection","custom",deck.Hero,deck.CardIds.ToArray(),owned);
Call("ValidateSelection","paladin",deck.Hero,deck.CardIds.ToArray(),new Dictionary<string,int>());
Check(true,"exact loaner accepts no owned copies");
var changed=deck.CardIds.ToArray(); changed[0]="EX1_561";
Reject("ValidateSelection","paladin",deck.Hero,changed,new Dictionary<string,int>());
Reject("ValidateSelection","paladin","HERO_08",deck.CardIds.ToArray(),owned);
owned=deck.CardIds.GroupBy(x=>x).ToDictionary(g=>g.Key,g=>g.Count());
var savedThirty=deck.CardIds.ToArray();
Call("ValidateCustomSelection",910000010L,deck.Hero,savedThirty,deck.Hero,savedThirty,owned);
Check(true,"exact persisted personal selection accepted by native ID");
Reject("ValidateCustomSelection",910000002L,deck.Hero,savedThirty,deck.Hero,savedThirty,owned);
Reject("ValidateCustomSelection",910000010L,deck.Hero,savedThirty,deck.Hero,savedThirty.Take(29).ToArray(),owned);
var otherThirty=savedThirty.ToArray(); otherThirty[0]="EX1_561";
Reject("ValidateCustomSelection",910000010L,deck.Hero,savedThirty,deck.Hero,otherThirty,owned);
Reject("ValidateCustomSelection",910000010L,deck.Hero,savedThirty,"HERO_01",savedThirty,owned);
Check((bool)Call("CanMutatePersonal",910000010L,new[]{910000010L,910000011L}),"persisted personal callback accepted");
Check(!(bool)Call("CanMutatePersonal",910000002L,new[]{910000010L}),"loaner callback rejected");
Check(!(bool)Call("CanMutatePersonal",910000012L,new[]{910000010L}),"deleted or stale callback rejected");
int L(int personal) => NativeUIProjection.ProjectedLoanerKeys(personal).Count;
Check(!(bool)Call("CanCreateDraft",true,true,true,true,NativeUIProjection.MaximumPersonalDecks,L(NativeUIProjection.MaximumPersonalDecks)),"capacity blocks native create");
Check((bool)Call("CanCreateDraft",true,true,true,true,NativeUIProjection.MaximumPersonalDecks-1,L(NativeUIProjection.MaximumPersonalDecks-1)),"raised capacity admits the 92nd personal deck");
Check((bool)Call("CanCreateDraft",true,true,true,true,19,L(19)),"retail personal cap (19) no longer blocks native create");
Check(!(bool)Call("CanCreateDraft",true,false,true,true,0,L(0)),"paste source blocks native create");
Check(!(bool)Call("CanCreateDraft",false,true,true,true,0,L(0)),"unsupported deck type blocks native create");
Check(!(bool)Call("CanCreateDraft",true,true,false,true,0,L(0)),"unsupported hero blocks native create");
Check(!(bool)Call("CanCreateDraft",true,true,true,false,0,L(0)),"non-Standard format blocks native create");
int loanerCount = NativeUIProjection.ProjectedLoanerKeys(0).Count;
Check(loanerCount == 11 && NativeUIProjection.ProjectedLoanerKeys(0, "default").Count == 26,
    "release projects eleven Core starter loaners; explicit harness selector enables tests");
int creationCap = NativeUIProjection.MaximumPersonalDecks;
Check(creationCap == LocalProfile.MaximumCustomDecks && creationCap + NativeUIProjection.PlayerLoanerCapacity == NativeUIProjection.NativeDeckLimit
    && loanerCount <= NativeUIProjection.PlayerLoanerCapacity
    && (bool)Call("CanCreateDraft",true,true,true,true,creationCap - 1,L(creationCap - 1))
    && !(bool)Call("CanCreateDraft",true,true,true,true,creationCap,L(creationCap)), "test loaners never reduce the personal creation slots");
// Live tray: the bootstrap loaner subset stays frozen while personal decks are added (native create, shop decks).
foreach (int bootstrap in new[] { 0, 18, 85, 88, 89, 90, 91 })
{
    int session = L(bootstrap);
    for (int personal = bootstrap; personal <= NativeUIProjection.MaximumPersonalDecks + 1; personal++)
    {
        bool fits = personal < NativeUIProjection.MaximumPersonalDecks && personal + session + 1 <= NativeUIProjection.NativeDeckLimit;
        Check(NativeUIProjection.CanAddPersonalDeck(personal, session) == fits
            && NativeUIProjection.CanCreateDraft(true,true,true,true,personal,session) == fits
            && (NativeUIProjection.PersonalDeckBlocker(personal, session) == null) == fits,
            $"live tray gate after bootstrap {bootstrap}, now {personal} personal decks");
    }
}
// Loaners never count against the personal cap: every personal count up to the cap keeps every loaner and New Deck.
int cap = NativeUIProjection.MaximumPersonalDecks;
Check(Enumerable.Range(0, cap + 2).All(p => L(p) == loanerCount)
    && Enumerable.Range(0, cap).All(p => NativeUIProjection.CanAddPersonalDeck(p, loanerCount)
        && NativeUIProjection.PersonalDeckBlocker(p, loanerCount) == null)
    && !NativeUIProjection.CanAddPersonalDeck(cap, loanerCount)
    && NativeUIProjection.PersonalDeckBlocker(cap, loanerCount) == LocalProfile.PersonalDeckLimitMessage
    && cap + loanerCount <= NativeUIProjection.NativeDeckLimit
    && !NativeUIProjection.CanAddPersonalDeck(cap - 1, NativeUIProjection.PlayerLoanerCapacity + 1)
    && NativeUIProjection.PersonalDeckBlocker(cap - 1, NativeUIProjection.PlayerLoanerCapacity + 1) == NativeUIProjection.TrayFullMessage,
    "all loaners at every personal count; session adds stop only at the personal cap (tray ceiling is defence in depth)");
// #57 review: New Deck needs the native slot gate AND room under the personal cap. With hidden starters (or a test-loaner
// tray) the 111-slot native gate still has room at 100 personal decks; the personal gate must hide New Deck there.
foreach (int newDeckLoaners in new[] { 0, 9, 10, NativeUIProjection.PlayerLoanerCapacity })
    Check(NativeUIProjection.ShowNewDeckButton(true, cap - 1, newDeckLoaners) && !NativeUIProjection.ShowNewDeckButton(true, cap, newDeckLoaners)
        && !NativeUIProjection.ShowNewDeckButton(false, 0, newDeckLoaners), $"New Deck hidden at the personal cap with {newDeckLoaners} session loaners");
Check(cap + 10 < NativeUIProjection.NativeDeckLimit && !NativeUIProjection.ShowNewDeckButton(true, cap, 10),
    "100 personal + one hidden starter: the native slot gate has room, New Deck still hidden");
int limit = NativeUIProjection.NativeDeckLimit;
Check(NativeUIProjection.TraySlots(27) == limit && NativeUIProjection.TraySlots(29) == limit + 2 && NativeUIProjection.TraySlots(26) == limit - 1
    && NativeUIProjection.TraySlots(25) == limit - 2, "native tray literals shift to personal cap + loaner capacity");
Check((bool)Call("OwnsCreateRequest",true,true,true,false)
    && !(bool)Call("OwnsCreateRequest",false,true,true,false)
    && !(bool)Call("OwnsCreateRequest",true,false,true,false)
    && !(bool)Call("OwnsCreateRequest",true,true,false,false)
    && !(bool)Call("OwnsCreateRequest",true,true,true,true),
    "only active current Collection normal draft enters recoverable create boundary");
Check(NativeUIProjection.CreateRequestHero("HERO_07", LocalShop.Default.HeroSkinClass) == "HERO_07"
    && NativeUIProjection.CreateRequestHero("HERO_11l", LocalShop.Default.HeroSkinClass) == "HERO_11"
    && LocalShop.Default.HeroSkinIds.All(skin => NativeUIProjection.CreateRequestHero(skin, LocalShop.Default.HeroSkinClass) == LocalShop.Default.HeroSkinClass(skin))
    && NativeUIProjection.CreateRequestHero("EX1_561", LocalShop.Default.HeroSkinClass) == null
    && NativeUIProjection.CreateRequestHero("HERO_99x", _ => "HERO_99") == null
    && NativeUIProjection.CreateRequestHero(null, LocalShop.Default.HeroSkinClass) == null,
    "a new deck picked through a favourite skin is created for its class hero");
bool OwnsPersonalEditor(bool active = true, bool collection = true, bool manager = true,
    bool tray = true, bool mainContext = true, bool exactDeck = true,
    bool exactInstance = true, bool persisted = true) => (bool)Call("OwnsPersonalDeckEditor",
        active, collection, manager, tray, mainContext, exactDeck, exactInstance, persisted);
Check(OwnsPersonalEditor(), "exact active current personal editor is owned");
Check(!OwnsPersonalEditor(active: false), "inactive personal editor passes through");
Check(!OwnsPersonalEditor(collection: false), "wrong scene personal editor passes through");
Check(!OwnsPersonalEditor(manager: false), "foreign Collection manager passes through");
Check(!OwnsPersonalEditor(tray: false), "foreign Collection tray passes through");
Check(!OwnsPersonalEditor(mainContext: false), "template or sideboard context passes through");
Check(!OwnsPersonalEditor(exactDeck: false), "foreign edited deck passes through");
Check(!OwnsPersonalEditor(exactInstance: false), "foreign hook instance passes through");
Check(!OwnsPersonalEditor(persisted: false), "loaner or unknown deck passes through");
int createAttempts = 0, createFailures = 0, localProjection = 0, outerCleanup = 0;
LocalCustomDeck committedDraft = new() { Id = 910000010L, Name = "Retry", Hero = "HERO_08" };
bool CreateAttempt(bool fail)
{
    object[] createCall = {
        (Func<LocalCustomDeck>)(() => { createAttempts++; if (fail) throw new IOException("locked profile"); return committedDraft; }),
        (Action<Exception>)(error => { Check(error is IOException, "create retains write cause"); createFailures++; }),
        null!
    };
    bool created = (bool)Call("TryCreateDraft", createCall);
    if (created) { Check(ReferenceEquals(createCall[2], committedDraft), "create returns committed draft"); localProjection++; }
    outerCleanup++;
    return created;
}
Check(!CreateAttempt(true) && createAttempts == 1 && createFailures == 1 && localProjection == 0 && outerCleanup == 1,
    "failed create skips local projection while handled prefix permits outer caller cleanup");
Check(CreateAttempt(false) && createAttempts == 2 && createFailures == 1 && localProjection == 1 && outerCleanup == 2,
    "create retry commits once and still permits outer caller cleanup");
bool downstreamCreateErrorEscaped = false;
try { if (CreateAttempt(false)) throw new InvalidOperationException("native editor error"); }
catch (InvalidOperationException) { downstreamCreateErrorEscaped = true; }
Check(downstreamCreateErrorEscaped, "create boundary does not swallow downstream native errors");
string createRoot = Path.Combine(Path.GetTempPath(), "native-create-boundary-" + Guid.NewGuid());
Directory.CreateDirectory(createRoot);
try
{
    string path = Path.Combine(createRoot, "profile.xml");
    long candidate;
    using (var profile = new LocalProfile(path))
    {
        var seed = profile.CreateDraft("Seed", "HERO_08");
        profile.DeleteDraft(seed.Id);
        byte[] before = File.ReadAllBytes(path);
        candidate = profile.NextCustomDeckId;
        Directory.CreateDirectory(path + ".tmp");
        int failures = 0;
        object[] durableCreateCall = {
            (Func<LocalCustomDeck>)(() => profile.CreateDraft("Create boundary", "HERO_08")),
            (Action<Exception>)(_ => failures++),
            null!
        };
        bool created = (bool)Call("TryCreateDraft", durableCreateCall);
        Check(!created && durableCreateCall[2] == null && failures == 1 && profile.CustomDecks.Count == 0
            && profile.NextCustomDeckId == candidate && File.ReadAllBytes(path).SequenceEqual(before),
            "failed durable create preserves bytes library and allocator");
        Directory.Delete(path + ".tmp");
        created = (bool)Call("TryCreateDraft", durableCreateCall);
        var saved = (LocalCustomDeck)durableCreateCall[2];
        Check(created && failures == 1 && saved.Id == candidate && profile.CustomDecks.Count == 1
            && profile.NextCustomDeckId == candidate + 1,
            "durable create retry uses original candidate exactly once");
    }
    using var reopened = new LocalProfile(path);
    Check(reopened.CustomDecks.Count == 1 && reopened.CustomDecks.Single().Id == candidate
        && reopened.NextCustomDeckId == candidate + 1, "retried create survives restart without ID reuse");
}
finally { Directory.Delete(createRoot, true); }
bool TryDeckBoxLock(bool active, bool collection, long deckId, out bool locked)
{
    object[] args = { active, collection, deckId, false };
    bool owned = (bool)Call("TryProjectDeckBoxLock", args);
    locked = (bool)args[3];
    return owned;
}
Check(!TryDeckBoxLock(false,true,910000002L,out _),"inactive deck box lock passes through");
Check(!TryDeckBoxLock(true,false,910000002L,out _),"non-Collection deck box lock passes through");
Check(TryDeckBoxLock(true,true,910000002L,out bool loanerLocked) && loanerLocked,
    "local loaner uses logical lock");
Check(TryDeckBoxLock(true,true,910000010L,out bool personalLocked) && !personalLocked,
    "local personal keeps normal unlocked visuals");
Check(TryDeckBoxLock(true,true,-1L,out bool unusedLocked) && !unusedLocked,
    "local unused slot stays unlocked for native hiding");
bool ShowLoanerTooltip(bool active, bool collection, long deckId, bool locked = true) =>
    (bool)Call("ShowLoanerTooltip", active, collection, deckId, locked);
Check(ShowLoanerTooltip(true, true, 910000002L), "locked local loaner gets loaner tooltip");
Check(!ShowLoanerTooltip(false, true, 910000002L), "inactive loaner keeps native tooltip");
Check(!ShowLoanerTooltip(true, false, 910000002L), "non-Collection loaner keeps native tooltip");
Check(!ShowLoanerTooltip(true, true, 910000010L), "personal deck keeps native tooltip");
Check(!ShowLoanerTooltip(true, true, 910000002L, false), "unlocked box keeps native tooltip");
bool ShowDelete(bool active, bool collection, long deckId, bool requested = true) =>
    (bool)Call("ShowDeckBoxDeleteButton", active, collection, deckId, requested);
Check(!ShowDelete(true, true, 910000002L), "current fixed loaner hides delete button");
Check(ShowDelete(false, true, 910000002L), "inactive fixed loaner preserves native delete visibility");
Check(ShowDelete(true, false, 910000002L), "non-Collection fixed loaner preserves native delete visibility");
Check(ShowDelete(true, true, 910000010L), "personal deck preserves native delete visibility");
Check(ShowDelete(true, true, 42L), "unknown deck preserves native delete visibility");
Check(ShowDelete(true, true, -1L), "unused slot preserves native delete visibility");
Check(!ShowDelete(true, true, 910000002L, false), "native hide request remains hidden");
string SaveAction(bool active, bool personal, bool exists, bool deleted, bool exactPersonal, bool loaner,
    bool stopReorder, bool ownershipReconcile, bool exactLoaner, bool crafting = false) => Call("DeckSaveAction", active, personal, exists,
        deleted, exactPersonal, loaner, stopReorder, ownershipReconcile, exactLoaner, crafting).ToString()!;
// Live 2026-10-30: a craft's ownership reconcile that drops a Fabled bundle must not throw (the profile deck is restored).
Check(SaveAction(true,true,true,false,false,false,false,true,false,true) == "Ignore", "crafting ownership reconcile of a personal deck is ignored");
Check(SaveAction(true,true,true,false,false,false,false,true,false,false) == "Reject", "other ownership reconcile of a changed personal deck is still refused");
Check(SaveAction(true,false,false,false,false,true,false,true,false,true) == "Reject", "crafting reconcile never makes a changed loaner writable");
Check(!(bool)Call("SupportsLocalDeckSave", false, true, true, false), "inactive save scope passes through");
Check(!(bool)Call("SupportsLocalDeckSave", true, false, true, false), "non-Collection save scope passes through");
Check(!(bool)Call("SupportsLocalDeckSave", true, true, false, false), "unsupported Collection source passes through");
Check((bool)Call("SupportsLocalDeckSave", true, true, true, false), "supported Collection save enters local policy");
Check((bool)Call("SupportsLocalDeckSave", true, false, true, true), "pack reward reconcile enters local policy");
Check(SaveAction(false,false,false,false,false,false,false,false,false) == "Passthrough", "inactive deck save passes through");
Check(SaveAction(true,true,true,false,true,false,false,false,false) == "Ignore", "unchanged personal save is a no-op");
Check(SaveAction(true,true,true,false,false,false,false,false,false) == "Save", "changed personal Locked envelope is revalidated");
Check(SaveAction(true,true,true,false,false,false,false,true,false) == "Reject", "changed personal reward reconcile is rejected");
Check(SaveAction(true,true,false,true,false,false,false,false,false) == "Ignore", "deleted personal trailing save is ignored");
Check(SaveAction(true,true,false,false,false,false,false,false,false) == "Reject", "unknown personal-range deck save is rejected");
Check(SaveAction(true,false,false,false,false,true,true,false,true) == "Ignore", "exact loaner reorder save is a no-op");
Check(SaveAction(true,false,false,false,false,true,false,false,true) == "Reject", "exact loaner non-reorder save is rejected");
Check(SaveAction(true,false,false,false,false,true,false,true,true) == "Ignore", "exact loaner reward reconcile clears pending state");
Check(SaveAction(true,false,false,false,false,true,false,true,false) == "Reject", "changed loaner reward reconcile is rejected");
Check(SaveAction(true,false,false,false,false,true,true,false,false) == "Reject", "changed loaner Locked envelope is rejected");
Check(SaveAction(true,false,false,false,false,false,true,false,false) == "Reject", "unknown native deck save is rejected");
var callbackOwner = new DeleteCallbackOwner();
Action<object> responseCallback = callbackOwner.Delete;
MethodInfo expectedDelete = typeof(DeleteCallbackOwner).GetMethod(nameof(DeleteCallbackOwner.Delete))!;
object callbackData = new();
bool Matches(Delegate callback, object target, MethodInfo method, object data) =>
    (bool)Call("MatchesDeleteConfirmation", callback, target, method, callbackData, data);
Check(Matches(responseCallback, callbackOwner, expectedDelete, callbackData), "own delete callback matches exact target method and data");
Check(!Matches(responseCallback, new DeleteCallbackOwner(), expectedDelete, callbackData)
    && !Matches((Action<object>)callbackOwner.Other, callbackOwner, expectedDelete, callbackData)
    && !Matches(responseCallback, callbackOwner, expectedDelete, new object()),
    "foreign target method and callback data cannot own delete confirmation");
Check((bool)Call("OwnsDeleteResponse", true, true, true)
    && !(bool)Call("OwnsDeleteResponse", false, true, true)
    && !(bool)Call("OwnsDeleteResponse", true, false, true)
    && !(bool)Call("OwnsDeleteResponse", true, true, false),
    "only active current Collection confirmation enters local delete boundary");
// A failed owned precommit must return normally before any native continuation runs.
foreach (string entry in new[] { "list", "editor", "discard" })
{
    int writes = 0, nativeMutations = 0, failures = 0;
    bool DeleteAttempt(bool fail) => (bool)Call("TryPrepareDelete",
        (Action)(() => { if (fail) throw new IOException("locked profile"); writes++; }),
        (Action<Exception>)(error => { Check(error is IOException, entry + " retains write cause"); failures++; }));
    if (DeleteAttempt(true)) nativeMutations++;
    Check(writes == 0 && nativeMutations == 0 && failures == 1, entry + " failure skips native mutation normally");
    if (DeleteAttempt(false)) nativeMutations++;
    Check(writes == 1 && nativeMutations == 1, entry + " retry permits native callback after commit");
    bool foreignEscaped = false;
    try { if (DeleteAttempt(false)) throw new InvalidOperationException("foreign native error"); }
    catch (InvalidOperationException) { foreignEscaped = true; }
    Check(foreignEscaped, entry + " original exception is not swallowed");
}
// Exercise the production boundary with real durable profile failures and restart.
string deleteRoot = Path.Combine(Path.GetTempPath(), "native-delete-boundary-" + Guid.NewGuid());
Directory.CreateDirectory(deleteRoot);
try
{
    foreach (string entry in new[] { "list", "editor", "discard" })
    {
        string path = Path.Combine(deleteRoot, entry + ".xml");
        long id;
        using (var profile = new LocalProfile(path))
        {
            id = profile.CreateDraft("Delete boundary", "HERO_08").Id;
            byte[] before = File.ReadAllBytes(path);
            Directory.CreateDirectory(path + ".tmp");
            int callbacks = 0, errors = 0;
            bool allowed = (bool)Call("TryPrepareDelete",
                (Action)(() => profile.DeleteDraft(id)), (Action<Exception>)(_ => errors++));
            if (allowed) callbacks++;
            Check(!allowed && callbacks == 0 && errors == 1 && profile.GetDraft(id) != null
                && File.ReadAllBytes(path).SequenceEqual(before), entry + " failed durable precommit preserves bytes and draft");
            Directory.Delete(path + ".tmp");
            allowed = (bool)Call("TryPrepareDelete",
                (Action)(() => profile.DeleteDraft(id)), (Action<Exception>)(_ => errors++));
            if (allowed) callbacks++;
            Check(allowed && callbacks == 1 && errors == 1 && profile.GetDraft(id) == null,
                entry + " durable retry permits exactly one native continuation");
        }
        using var reopened = new LocalProfile(path);
        Check(reopened.GetDraft(id) == null, entry + " deletion survives restart");
    }
}
finally { Directory.Delete(deleteRoot, true); }
string deleteFailure = (string)Call("DeleteFailureMessage", 910000010L, "locked profile");
Check(deleteFailure.Contains("not deleted") && deleteFailure.Contains("retry", StringComparison.OrdinalIgnoreCase),
    "delete failure tells player no deletion occurred and retry is available");
var noticeType = Assembly.GetExecutingAssembly().GetType("NativeDeckDeleteNotice")
    ?? throw new MissingMemberException("NativeDeckDeleteNotice");
object managerOwner = new(), dialogOwner = new();
object NewDeleteNotice() => Activator.CreateInstance(noticeType, BindingFlags.Instance | BindingFlags.NonPublic,
    null, new object[] { managerOwner, dialogOwner, "Deck deleted", "Reopen Collection" }, null)!;
object NoticeCall(object notice, string method, params object[] args) => noticeType.GetMethod(method,
    BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(notice, args)!;
var deleteNotice = NewDeleteNotice();
NoticeCall(deleteNotice, "MarkHidden", new object());
Check(!(bool)NoticeCall(deleteNotice, "TryTake", managerOwner, true), "foreign hide cannot release owned delete notice");
NoticeCall(deleteNotice, "MarkHidden", dialogOwner);
Check(!(bool)NoticeCall(deleteNotice, "TryTake", new object(), true), "foreign manager cannot consume owned notice");
Check((bool)NoticeCall(deleteNotice, "TryTake", managerOwner, true)
    && !(bool)NoticeCall(deleteNotice, "TryTake", managerOwner, true), "owned hidden notice is consumed exactly once");
var staleNotice = NewDeleteNotice();
NoticeCall(staleNotice, "MarkHidden", dialogOwner);
Check(!(bool)NoticeCall(staleNotice, "TryTake", managerOwner, false)
    && !(bool)NoticeCall(staleNotice, "TryTake", managerOwner, true), "out-of-scene notice retires instead of showing on return");
Check((string)noticeType.GetProperty("Header", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(deleteNotice)! == "Deck deleted"
    && (string)noticeType.GetProperty("Message", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(deleteNotice)! == "Reopen Collection",
    "committed notice keeps captured durable truth after later operations");

string committedRefresh=(string)Call("ReorderRefreshFailureMessage",true,"visual update failed");
Check(committedRefresh.Contains("saved",StringComparison.OrdinalIgnoreCase)
    && committedRefresh.Contains("reopen",StringComparison.OrdinalIgnoreCase)
    && !committedRefresh.Contains("not saved",StringComparison.OrdinalIgnoreCase)
    && !committedRefresh.Contains("rolled back",StringComparison.OrdinalIgnoreCase),
    "post-commit refresh failure preserves durable success truth");
string unchangedRefresh=(string)Call("ReorderRefreshFailureMessage",false,"visual update failed");
Check(unchangedRefresh.Contains("unchanged",StringComparison.OrdinalIgnoreCase)
    && unchangedRefresh.Contains("reopen",StringComparison.OrdinalIgnoreCase),
    "pre-commit refresh failure preserves unchanged durable truth");
string deleteRefresh=(string)Call("DeleteRefreshFailureMessage",910000010L,"rank target missing");
Check(deleteRefresh.Contains("deleted",StringComparison.OrdinalIgnoreCase)
    && deleteRefresh.Contains("reopen",StringComparison.OrdinalIgnoreCase)
    && !deleteRefresh.Contains("delete failed",StringComparison.OrdinalIgnoreCase)
    && !deleteRefresh.Contains("not deleted",StringComparison.OrdinalIgnoreCase),
    "post-commit delete refresh failure preserves durable success truth");
string createRefresh=(string)Call("CreateRefreshFailureMessage",910000010L,"editor unavailable");
Check(createRefresh.Contains("saved",StringComparison.OrdinalIgnoreCase)
    && createRefresh.Contains("reopen",StringComparison.OrdinalIgnoreCase)
    && createRefresh.Contains("durable",StringComparison.OrdinalIgnoreCase)
    && !createRefresh.Contains("not saved",StringComparison.OrdinalIgnoreCase)
    && !createRefresh.Contains("retry",StringComparison.OrdinalIgnoreCase)
    && !createRefresh.Contains("rolled back",StringComparison.OrdinalIgnoreCase),
    "post-commit create presentation failure preserves durable success truth");
string saveRefresh=(string)Call("SaveRefreshFailureMessage",910000010L,"status path locked");
Check(saveRefresh.Contains("saved",StringComparison.OrdinalIgnoreCase)
    && saveRefresh.Contains("reopen",StringComparison.OrdinalIgnoreCase)
    && !saveRefresh.Contains("not saved",StringComparison.OrdinalIgnoreCase)
    && !saveRefresh.Contains("retry",StringComparison.OrdinalIgnoreCase),
    "post-commit save diagnostic failure preserves durable success truth");
string savePrecommit=(string)Call("DeckWriteFailureMessage","save",910000010L,"profile locked");
Check(savePrecommit.Contains("failed",StringComparison.OrdinalIgnoreCase)
    && savePrecommit.Contains("retry",StringComparison.OrdinalIgnoreCase)
    && !savePrecommit.Contains("was saved",StringComparison.OrdinalIgnoreCase),
    "pre-commit save failure remains unsaved and retryable");

IReadOnlyDictionary<long,long> Ranks(params KeyValuePair<long,long>[] personals) =>
    NativeUIProjection.ExpectedNativeSortOrders(personals);
var zeroRanks = Ranks();
long firstLoanerId = NativeUIProjection.DeckId(NativeUIProjection.ProjectedLoanerKeys(0)[0]);
Check(zeroRanks.Count == loanerCount && NativeUIProjection.ProjectedLoanerKeys(0)
    .Select((key, i) => zeroRanks[NativeUIProjection.DeckId(key)] == NativeUIProjection.MaximumPersonalDecks + i).All(ok => ok),
    "zero-personal projection keeps fixed loaner band");
var duplicateTokenRanks = Ranks(new(910000012L,5), new(910000001L,5), new(910000010L,2));
Check(duplicateTokenRanks[910000010L] == 0 && duplicateTokenRanks[910000001L] == 1
    && duplicateTokenRanks[910000012L] == 2 && duplicateTokenRanks[firstLoanerId] == NativeUIProjection.MaximumPersonalDecks,
    "personal ranks sort by token then ID before loaners");
Reject("ExpectedNativeSortOrders", new object[] { new[] { new KeyValuePair<long,long>(910000010L,-100) }, null! });
var createdRanks = Ranks(new(910000001L,5), new(910000010L,20), new(910000011L,910000011L));
Check(createdRanks[910000011L] == 2, "new personal appends by persisted token");
var deletedRanks = Ranks(new(910000001L,5), new(910000011L,910000011L));
Check(deletedRanks[910000011L] == 1, "personal display ranks compress after delete");
var fullRanks = Ranks(Enumerable.Range(0,NativeUIProjection.MaximumPersonalDecks).Select(i => new KeyValuePair<long,long>(910000010L + i, 1000 - i)).ToArray());
// At the personal cap the tray still projects every loaner beside the full personal library.
int fullLoanerCount = NativeUIProjection.ProjectedLoanerKeys(NativeUIProjection.MaximumPersonalDecks).Count;
Check(fullLoanerCount == loanerCount
    && NativeUIProjection.ProjectedLoanerKeys(NativeUIProjection.MaximumPersonalDecks).SequenceEqual(NativeUIProjection.ProjectedLoanerKeys(0)),
    "full personal capacity keeps every loaner within the native tray");
Check(fullRanks.Count == NativeUIProjection.MaximumPersonalDecks + fullLoanerCount
    && fullRanks.Count <= NativeUIProjection.NativeDeckLimit
    && fullRanks.Values.OrderBy(rank => rank).SequenceEqual(Enumerable.Range(0,fullRanks.Count).Select(i => (long)i)),
    "capped personals project with every loaner and contiguous unique ranks within the native tray");
Reject("ExpectedNativeSortOrders", new object[] { Enumerable.Range(0,NativeUIProjection.MaximumPersonalDecks + 1).Select(i => new KeyValuePair<long,long>(910000010L + i, i)).ToArray(), null! });
string[] starters = { "mage", "warrior", "priest", "mage-frost", "warrior-control", "priest-combo", "paladin", "paladin-control" };
string[] testPriority = {
    "test-showcase-deathknight", "test-decisions", "test-standard-batch-1", "test-standard-batch-2-warrior", "test-standard-batch-2-priest",
    "test-standard-batch-2-mage", "test-standard-batch-2-paladin", "test-standard-batch-2-druid", "test-standard-batch-2-rogue",
    "test-standard-batch-2-warlock", "test-standard-batch-2-hunter", "test-standard-batch-2-shaman", "test-standard-batch-2-demonhunter",
    "test-rush-silence", "test-standard-extra-warrior", "test-standard-extra-priest", "test-standard-extra-druid", "test-standard-extra-hunter", "test-standard-extra-demonhunter", "test-standard-extra-rogue",
    "test-deathknight-rainbow", "test-deathknight-frost", "test-deathknight-unholy", "test-deathknight-blood", "test-deathknight-buu",
    "test-standard-more-warrior", "test-standard-more-priest", "test-standard-more-mage", "test-standard-more-paladin",
    "test-standard-more-druid", "test-standard-more-rogue", "test-standard-more-warlock", "test-standard-more-hunter",
    "test-standard-more-shaman", "test-standard-more-demonhunter",
    "test-standard-b10-warrior", "test-standard-b10-priest", "test-standard-b10-mage", "test-standard-b10-paladin",
    "test-standard-b10-druid", "test-standard-b10-rogue", "test-standard-b10-warlock", "test-standard-b10-hunter",
    "test-standard-b10-shaman", "test-standard-b10-demonhunter", "test-standard-b10-deathknight",
    "test-standard-b10-deathknight-2",
    "test-standard-mecha-warrior", "test-standard-mecha-priest", "test-standard-mecha-mage", "test-standard-mecha-paladin",
    "test-standard-mecha-druid", "test-standard-mecha-rogue", "test-standard-mecha-warlock", "test-standard-mecha-hunter",
    "test-standard-mecha-shaman", "test-standard-mecha-demonhunter", "test-standard-mecha-deathknight", "test-standard-mecha-deathknight-2",
    "test-standard-rewind-rogue", "test-standard-rewind-mage", "test-standard-rewind-warlock", "test-standard-rewind-shaman", "test-standard-rewind-paladin",
    "test-standard-rewind-druid", "test-standard-rewind-warrior", "test-standard-rewind-priest", "test-standard-rewind-demonhunter", "test-standard-rewind-hunter",
    "test-keywords-cfa-paladin", "test-keywords-cfa-hunter",
    "test-keywords-cfa-warrior", "test-keywords-cfa-shaman", "test-keywords-cfa-priest", "test-keywords-cfa-mage",
    "test-standard-cardstate-mage", "test-standard-cardstate-shaman", "test-standard-cardstate-warlock", "test-standard-cardstate-warrior",
    "test-standard-cardstate-druid", "test-standard-cardstate-hunter", "test-standard-cardstate-paladin", "test-standard-cardstate-priest",
    "test-standard-b11-warrior", "test-standard-b11-priest", "test-standard-b11-mage", "test-standard-b11-paladin",
    "test-standard-b11-druid", "test-standard-b11-rogue", "test-standard-b11-warlock", "test-standard-b11-hunter",
    "test-standard-b11-shaman", "test-standard-azalina-priest"
};
foreach (var (personalCount, sourceTests) in new[] { (0,18), (1,17), (2,16), (3,15), (4,14), (5,13), (6,12), (7,11), (17,1), (18,0), (19,0) })
{
    var projected = NativeUIProjection.ProjectedLoanerKeys(personalCount, "default");
    int expectedTests = sourceTests;
    Check(projected.SequenceEqual(starters.Concat(testPriority.Take(expectedTests))),
        "starters pinned, test loaners dropped last-first at personal count " + personalCount);
    var personals = Enumerable.Range(0, personalCount).Select(i => new KeyValuePair<long,long>(910000010L + i, 99 - i)).ToArray();
    var ranks = NativeUIProjection.ExpectedNativeSortOrders(personals, projected);
    Check(ranks.Count == personalCount + 8 + expectedTests && ranks.Count <= (personalCount < 19 ? 26 : 27)
        && personals.OrderBy(personal => personal.Value).ThenBy(personal => personal.Key)
            .Select((personal, i) => ranks[personal.Key] == i).All(matches => matches)
        && projected.Select((key, i) => ranks[NativeUIProjection.DeckId(key)] == NativeUIProjection.MaximumPersonalDecks + i).All(matches => matches)
        && ranks.Values.Distinct().Count() == ranks.Count,
        "all personals projected; tray fits, fixed loaner band with unique ranks at " + personalCount);
    Check(NativeUIProjection.CanCreateDraft(true,true,true,true,personalCount,projected.Count) == (personalCount < NativeUIProjection.MaximumPersonalDecks),
        "creation gate ignores test loaners at " + personalCount);
}
// Mid-session ranks must retain bootstrap's subset even when a delete frees space for a test loaner.
var frozenLoaners = NativeUIProjection.ProjectedLoanerKeys(18);
var afterDelete = NativeUIProjection.ExpectedNativeSortOrders(
    Enumerable.Range(0,17).Select(i => new KeyValuePair<long,long>(910000010L + i, i)), frozenLoaners);
Check(afterDelete.Count == 17 + loanerCount && !afterDelete.ContainsKey(900000010L)
    && afterDelete.Values.Distinct().Count() == afterDelete.Count
    && frozenLoaners.All(key => afterDelete[NativeUIProjection.DeckId(key)] == zeroRanks[NativeUIProjection.DeckId(key)]),
    "delete ranks preserve bootstrap loaner subset until restart");
var afterCreate = NativeUIProjection.ExpectedNativeSortOrders(
    Enumerable.Range(0,19).Select(i => new KeyValuePair<long,long>(910000010L + i, i)), frozenLoaners);
Check(afterCreate.Count == 19 + loanerCount && afterCreate[firstLoanerId] == NativeUIProjection.MaximumPersonalDecks,
    "create ranks preserve bootstrap loaner subset with all personals first");
Check(frozenLoaners.All(key => afterCreate[NativeUIProjection.DeckId(key)] == afterDelete[NativeUIProjection.DeckId(key)]),
    "session loaner ranks never shift on personal create or delete");
var sessionLoaners = NativeUIProjection.ProjectedLoanerKeys(6);
foreach (int personalCount in new[] { 5, 6, 7 })
{
    var ranks = NativeUIProjection.ExpectedNativeSortOrders(
        Enumerable.Range(0, personalCount).Select(i => new KeyValuePair<long,long>(910000010L + i, i)), sessionLoaners);
    Check(sessionLoaners.All(key => ranks[NativeUIProjection.DeckId(key)] == zeroRanks[NativeUIProjection.DeckId(key)]),
        "session test loaner ranks stay fixed at personal count " + personalCount);
}
Reject("ProjectedLoanerKeys", -1, null!);
const string documentedSelectorPath = @"C:\hs-native-ui-"+@"prototype\client\reports\test-loaner-group.txt";
Check(NativeUIProjection.TestLoanerGroupPath == documentedSelectorPath,
    "bootstrap selector stays inside disposable client reports");
static string ProjectionSourceDirectory([System.Runtime.CompilerServices.CallerFilePath] string file = "") => Path.GetDirectoryName(file)!;
// The coordinator's verification note is private; where it is absent the check is skipped.
// It sits in the private tree, or under private/ once the private add-on is inside a public checkout.
string verificationDocPath = new[] { "../../docs/roadmap", "../../private/docs/roadmap" }
    .Select(folder => Path.Combine(ProjectionSourceDirectory(), folder, "2026-10-19-standard-live-verification.md"))
    .FirstOrDefault(File.Exists) ?? "";
if (verificationDocPath != "")
{
    var selectorDocLines = File.ReadAllText(verificationDocPath).Split('\n').Where(line => line.Contains("test-loaner-group.txt")).ToArray();
    Check(selectorDocLines.Length >= 2 && selectorDocLines.All(line => line.Contains(NativeUIProjection.TestLoanerGroupPath)),
        "documented selector path and coordinator command match plugin path");
}
else Console.WriteLine("SKIP documented selector path: maintainer-only (docs/roadmap is not in this checkout)");
NativeUIProjection.TestLoanerGroupPath = @"C:\hs-native-ui-"+@"prototype\lanes\2\client\reports\test-loaner-group.txt";
Check(NativeUIProjection.TestLoanerGroupPath.Contains(@"\lanes\2\"), "caller configures lane selector independently");
NativeUIProjection.TestLoanerGroupPath = documentedSelectorPath;
string selectorRoot = Path.Combine(Path.GetTempPath(), "native-loaner-selector-" + Guid.NewGuid());
Directory.CreateDirectory(selectorRoot);
try
{
    string path = Path.Combine(selectorRoot, "test-loaner-group.txt");
    var failures = new List<string>();
    void CheckSelector(string label, string expected, bool warns)
    {
        var warnings = new List<string>();
        try
        {
            string group = NativeUIProjection.LoadTestLoanerGroup(path, warnings.Add);
            if (group != expected || warnings.Count != (warns ? 1 : 0)
                || (warns && !warnings[0].Contains("disabled")))
                failures.Add(label + ": group=" + (group ?? "null") + ", warnings=" + warnings.Count);
            else Check(true, "selector loading " + label);
        }
        catch (Exception error) { failures.Add(label + ": threw " + error.GetType().Name); }
    }
    CheckSelector("missing disables tests", null!, true);
    foreach (var sample in new[] {
        (Label: "empty falls back", Text: "", Expected: (string)null!, Warns: true),
        (Label: "whitespace falls back", Text: " \r\n", Expected: (string)null!, Warns: true),
        (Label: "unknown falls back", Text: "unknown", Expected: (string)null!, Warns: true),
        (Label: "oversized valid prefix falls back", Text: "extra" + new string(' ', 60), Expected: (string)null!, Warns: true),
        (Label: "oversized valid number falls back", Text: "4" + new string(' ', 64), Expected: (string)null!, Warns: true),
        (Label: "out-of-range group falls back", Text: "12", Expected: (string)null!, Warns: true),
        (Label: "zero group falls back", Text: "0", Expected: (string)null!, Warns: true),
        (Label: "leading zero falls back", Text: "04", Expected: (string)null!, Warns: true),
        (Label: "signed group falls back", Text: "+4", Expected: (string)null!, Warns: true),
        (Label: "two-digit leading zero falls back", Text: "010", Expected: (string)null!, Warns: true),
        (Label: "default trimmed case insensitive", Text: " DeFaUlT\r\n", Expected: "1", Warns: false),
        (Label: "deathknight trimmed case insensitive", Text: " DeAtHkNiGhT\r\n", Expected: "3", Warns: false),
        (Label: "extra trimmed case insensitive", Text: " ExTrA\r\n", Expected: "2", Warns: false),
        (Label: "numbered group trimmed", Text: " 4\r\n", Expected: "4", Warns: false),
        (Label: "reserved batch 10 group", Text: "5", Expected: "5", Warns: false),
        (Label: "mech families group six", Text: "6", Expected: "6", Warns: false),
        (Label: "Rewind group seven", Text: "7", Expected: "7", Warns: false),
        (Label: "CFA group eight", Text: "8", Expected: "8", Warns: false),
        (Label: "batch 11 group ten", Text: " 10\r\n", Expected: "10", Warns: false),
        (Label: "card-state group nine", Text: "9", Expected: "9", Warns: false),
        (Label: "showcase alias", Text: " ShOwCaSe ", Expected: "11", Warns: false),
        (Label: "showcase number", Text: "11", Expected: "11", Warns: false),
        (Label: "numbered group one", Text: "1", Expected: "1", Warns: false),
        (Label: "64 byte boundary accepts extra", Text: "extra" + new string(' ', 59), Expected: "2", Warns: false),
        (Label: "UTF8 BOM accepts extra", Text: "\uFEFFextra\r\n", Expected: "2", Warns: false),
        (Label: "UTF8 BOM accepts number", Text: "\uFEFF3\r\n", Expected: "3", Warns: false)
    })
    {
        File.WriteAllText(path, sample.Text);
        CheckSelector(sample.Label, sample.Expected, sample.Warns);
    }
    File.WriteAllText(path, "extra");
    using (var locked = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        CheckSelector("unreadable disables tests", null!, true);
    Check(failures.Count == 0, "real selector loading: " + string.Join("; ", failures));
}
finally { Directory.Delete(selectorRoot, true); }
Check(NativeUIProjection.ProjectedLoanerKeys(1, "unknown")
    .SequenceEqual(NativeUIProjection.ProjectedLoanerKeys(1, "11")),
    "unknown group cannot block tray projection");
Check(NativeUIProjection.ShowcaseLoanerKeys.SequenceEqual(new[] { "test-showcase-deathknight" })
    && NativeUIProjection.DeckId("test-showcase-deathknight") == 900000048L, "DK showcase leads every group with id 900000048");
Check(NativeUIProjection.TestLoanerGroups[9].SequenceEqual(new[] {
    "test-standard-b11-warrior", "test-standard-b11-priest", "test-standard-b11-mage", "test-standard-b11-paladin",
    "test-standard-b11-druid", "test-standard-b11-rogue", "test-standard-b11-warlock", "test-standard-b11-hunter",
    "test-standard-b11-shaman" }), "group 10 retains batch-11 table order");
// Every group projects the showcase first, then its prefix-selected members, then the rest in table order.
IEnumerable<string> GroupFirst(string prefix) => testPriority.Take(1)
    .Concat(testPriority.Skip(1).Where(key => key.StartsWith(prefix, StringComparison.Ordinal)))
    .Concat(testPriority.Skip(1).Where(key => !key.StartsWith(prefix, StringComparison.Ordinal)));
for (int personalCount = 0; personalCount <= 3; personalCount++)
{
    var extraMembers = NativeUIProjection.TestLoanerGroups[1];
    var extraFirst = testPriority.Take(1).Concat(extraMembers).Concat(testPriority.Skip(1).Where(key => !extraMembers.Contains(key)));
    var projected = NativeUIProjection.ProjectedLoanerKeys(personalCount, "extra");
    Check(projected.SequenceEqual(starters.Concat(extraFirst.Take(18 - personalCount))),
        "extra group prioritizes overflow without changing capacity at P=" + personalCount);
    Check(NativeUIProjection.ProjectedLoanerKeys(personalCount, "default")
        .Concat(projected).Concat(NativeUIProjection.ProjectedLoanerKeys(personalCount, "deathknight"))
        .Concat(NativeUIProjection.ProjectedLoanerKeys(personalCount, "4")).Concat(NativeUIProjection.ProjectedLoanerKeys(personalCount, "5")).Concat(NativeUIProjection.ProjectedLoanerKeys(personalCount, "6"))
        .Concat(NativeUIProjection.ProjectedLoanerKeys(personalCount, "7"))
        .Concat(NativeUIProjection.ProjectedLoanerKeys(personalCount, "8"))
        .Concat(NativeUIProjection.ProjectedLoanerKeys(personalCount, "9"))
        .Concat(NativeUIProjection.ProjectedLoanerKeys(personalCount, "10"))
        .Concat(NativeUIProjection.ProjectedLoanerKeys(personalCount, "11"))
        .ToHashSet().SetEquals(starters.Concat(testPriority).Concat(NativeUIProjection.TestLoanerGroups[9]).Concat(NativeUIProjection.PlayerLoanerKeys)),
        "test groups expose coverage seats; player selection stays curated at P=" + personalCount);
    var seatFirst = GroupFirst("test-standard-more-");
    Check(NativeUIProjection.ProjectedLoanerKeys(personalCount, " 4 ").SequenceEqual(starters.Concat(seatFirst.Take(18 - personalCount))),
        "group 4 prioritizes ten seat loaners without changing capacity at P=" + personalCount);
    var batch10First = GroupFirst("test-standard-b10-");
    Check(NativeUIProjection.ProjectedLoanerKeys(personalCount, "5").SequenceEqual(starters.Concat(batch10First.Take(18 - personalCount))),
        "group 5 prioritizes twelve batch-10 seat loaners without changing capacity at P=" + personalCount);
    var batch11First = GroupFirst("test-standard-b11-");
    Check(NativeUIProjection.ProjectedLoanerKeys(personalCount, "10").SequenceEqual(starters.Concat(batch11First.Take(18 - personalCount))),
        "group 10 prioritizes nine batch-11 seat loaners without changing capacity at P=" + personalCount);
    var mechaFirst = GroupFirst("test-standard-mecha-");
    Check(NativeUIProjection.ProjectedLoanerKeys(personalCount, "6").SequenceEqual(starters.Concat(mechaFirst.Take(18 - personalCount))),
        "group 6 prioritizes twelve mechanic-families seat loaners without changing capacity at P=" + personalCount);
    var rewindFirst = GroupFirst("test-standard-rewind-");
    Check(NativeUIProjection.ProjectedLoanerKeys(personalCount, "7").SequenceEqual(starters.Concat(rewindFirst.Take(18 - personalCount))),
        "group 7 reaches ten Rewind loaners without changing capacity at P=" + personalCount);
    foreach (var alias in new[] { ("default", "1"), ("extra", "2"), ("deathknight", "3") })
        Check(NativeUIProjection.ProjectedLoanerKeys(personalCount, alias.Item1).SequenceEqual(NativeUIProjection.ProjectedLoanerKeys(personalCount, alias.Item2)),
            "alias " + alias.Item1 + " projects group " + alias.Item2 + " at P=" + personalCount);
    Check(NativeUIProjection.ProjectedLoanerKeys(personalCount, " DeAtHkNiGhT ").SequenceEqual(starters.Concat(
        GroupFirst("test-deathknight-").Take(18 - personalCount))),
        "DK group prioritizes five DK loaners without changing capacity at P=" + personalCount);
    var frozenRanks = NativeUIProjection.ExpectedNativeSortOrders(
        Enumerable.Range(0, personalCount + 1).Select(i => new KeyValuePair<long,long>(910000010L + i, i)), projected);
    Check(projected.Select((key, i) => frozenRanks[NativeUIProjection.DeckId(key)] == NativeUIProjection.MaximumPersonalDecks + i).All(matches => matches)
        && frozenRanks.Count <= 27, "extra group stays frozen through session create at P=" + personalCount);
}
Check(NativeUIProjection.ProjectedLoanerKeys(NativeUIProjection.MaximumPersonalDecks + 1).SequenceEqual(NativeUIProjection.ProjectedLoanerKeys(NativeUIProjection.MaximumPersonalDecks))
    && NativeUIProjection.ProjectedLoanerKeys(int.MaxValue).SequenceEqual(NativeUIProjection.ProjectedLoanerKeys(NativeUIProjection.MaximumPersonalDecks)),
    "overflow visible personal counts clamp to the personal cap");
foreach (string key in testPriority.Except(NativeUIProjection.ProjectedLoanerKeys(18)))
{
    var loaner = LocalDecks.Get(key);
    long id = NativeUIProjection.DeckId(key);
    Check(NativeUIProjection.IsLoanerId(id) && !NativeUIProjection.IsPersonalId(id)
        && NativeUIProjection.IsExactLoaner(id, loaner.Hero, loaner.CardIds)
        && !NativeUIProjection.CanMutatePersonal(id, new[] { id }), "absent test loaner keeps immutable reserved identity: " + key);
    NativeUIProjection.ValidateSelection(key, loaner.Hero, loaner.CardIds.ToArray(), new Dictionary<string,int>());
    NativeUIProjection.ValidateMatchDeckBinding(key, null!);
    Check(TryDeckBoxLock(true, true, id, out bool locked) && locked
        && !NativeUIProjection.ShowDeckBoxDeleteButton(true, true, id, true),
        "absent test loaner keeps lock and delete policy: " + key);
}
string capacityRoot = Path.Combine(Path.GetTempPath(), "native-capacity-" + Guid.NewGuid());
Directory.CreateDirectory(capacityRoot);
try
{
    string path = Path.Combine(capacityRoot, "profile.xml");
    using (var profile = new LocalProfile(path))
        for (int i = 0; i < NativeUIProjection.MaximumPersonalDecks; i++) profile.CreateDraft("Saved " + i, "HERO_08");
    // Restored engine support can expose one stored draft past the cap; the visible-draft rule shows the cap.
    var serializer = new System.Xml.Serialization.XmlSerializer(typeof(LocalProfileData));
    LocalProfileData stored;
    using (var reader = File.OpenRead(path)) stored = (LocalProfileData)serializer.Deserialize(reader)!;
    stored.CustomDecks.Add(new LocalCustomDeck { Id = stored.NextCustomDeckId++, SortOrder = long.MaxValue, Name = "Restored", Hero = "HERO_08" });
    using (var writer = File.Create(path)) serializer.Serialize(writer, stored);
    byte[] beforeReload = File.ReadAllBytes(path);
    using var reopened = new LocalProfile(path);
    var saved = reopened.CustomDecks;
    var persistedRanks = Ranks(saved.Select(personal => new KeyValuePair<long,long>(personal.Id, personal.SortOrder)).ToArray());
    Check(saved.Count == NativeUIProjection.MaximumPersonalDecks && saved.All(personal => persistedRanks.ContainsKey(personal.Id))
        && persistedRanks.Count == NativeUIProjection.MaximumPersonalDecks + fullLoanerCount, "a full visible personal library from one extra stored draft projects within capacity");
    Check(saved.All(personal => personal.Name != "Restored") && beforeReload.SequenceEqual(File.ReadAllBytes(path)),
        "capacity projection preserves every stored draft byte, including restored overflow");
    const string absentLoaner = "test-standard-batch-2-demonhunter";
    var match = reopened.Begin(absentLoaner, LocalDecks.OpponentFor(absentLoaner, Array.Empty<string>()).Id, 123);
    reopened.Settle(match.Id, "Lost", 0, true);
    reopened.Dispose();
    using var withHistory = new LocalProfile(path);
    Check(withHistory.History.Last().Deck == absentLoaner && withHistory.History.Last().DeckHero == "HERO_10"
        && withHistory.CustomDecks.Count == NativeUIProjection.MaximumPersonalDecks, "history for an absent test loaner survives restart at full personal capacity");
}
finally { Directory.Delete(capacityRoot, true); }
string[] addedClasses = { "druid", "rogue", "warlock", "hunter", "shaman", "demonhunter" };
for (int i = 0; i < addedClasses.Length; i++)
{
    string key = "test-standard-batch-2-" + addedClasses[i];
    long id = 900000016L + i;
    Check((long)Call("DeckId", key) == id && (bool)Call("IsLoanerId", id)
        && !(bool)Call("IsPersonalId", id) && NativeUIProjection.ExpectedNativeSortOrders(Array.Empty<KeyValuePair<long,long>>(), NativeUIProjection.ProjectedLoanerKeys(0, "default"))[id] == NativeUIProjection.MaximumPersonalDecks + 15 + i, "explicit added loaner identity and rank " + key);
}
Reject("ValidateCustomSelection",910000010L,"HERO_99",savedThirty,"HERO_99",savedThirty,owned);
var bridgePersonal=LocalDecks.Custom("Bridge",deck.Hero,savedThirty);
Call("ValidateMatchDeckBinding","custom:910000010",bridgePersonal);
Check(true,"explicit personal key accepts concrete playable snapshot at bridge");
Call("ValidateMatchDeckBinding","paladin",null!);
Check(true,"loaner key accepts no personal snapshot at bridge");
Reject("ValidateMatchDeckBinding","custom",bridgePersonal);
Reject("ValidateMatchDeckBinding","custom:910000010",null!);
Reject("ValidateMatchDeckBinding","paladin",bridgePersonal);
Reject("ValidateMatchDeckBinding","custom:not-a-number",bridgePersonal);
Reject("ValidateMatchDeckBinding","custom: 910000010",bridgePersonal);
var partialBridge=LocalDecks.Draft("Partial",deck.Hero,savedThirty.Take(29));
Reject("ValidateMatchDeckBinding","custom:910000010",partialBridge);
string recoverable=(string)Call("DeckWriteFailureMessage","save",910000010L,"disk full");
Check(recoverable.Contains("disk full") && recoverable.Contains("retry",StringComparison.OrdinalIgnoreCase),
    "native deck save failure is actionable and retryable");
var emptyOwned=new Dictionary<string,int>();
Check(!(bool)Call("ValidateDraftProjection",910000010L,"Empty","HERO_08",Array.Empty<string>(),"Empty","HERO_08",Array.Empty<string>(),emptyOwned),
    "real zero-card projected draft remains incomplete");
var twentyNine=LocalDecks.Get("mage").CardIds.Take(29).ToArray();
var twentyNineOwned=twentyNine.GroupBy(id=>id).ToDictionary(group=>group.Key,group=>group.Count());
Check(!(bool)Call("ValidateDraftProjection",910000010L,"Partial","HERO_08",twentyNine,"Partial","HERO_08",twentyNine,twentyNineOwned),
    "real 29-card projected draft remains incomplete");
Check((bool)Call("ValidateDraftProjection",910000010L,"Complete",deck.Hero,savedThirty,"Complete",deck.Hero,savedThirty,owned),
    "real 30-card projected draft is playable");
Check(!(bool)Call("ValidateDraftProjection",910000010L,"Missing",deck.Hero,savedThirty,"Missing",deck.Hero,savedThirty,emptyOwned),
    "missing cards remain projected but deck is incomplete");
Reject("ValidateCustomSelection",910000010L,deck.Hero,savedThirty,deck.Hero,savedThirty,emptyOwned);
Reject("ValidateDraftProjection",910000010L,"Saved",deck.Hero,savedThirty,"Changed",deck.Hero,savedThirty,owned);
foreach (var loaner in LocalDecks.All)
    Check((bool)Call("IsExactLoaner",Call("DeckId",loaner.Id),loaner.Hero,loaner.CardIds.ToArray()), "exact fixed loaner accepted: " + loaner.Id);
var mageLoaner=LocalDecks.Get("mage");
var alteredLoaner=mageLoaner.CardIds.ToArray(); alteredLoaner[0]="EX1_561";
Check(!(bool)Call("IsExactLoaner",Call("DeckId","mage"),"HERO_01",mageLoaner.CardIds.ToArray()), "altered loaner hero rejected");
Check(!(bool)Call("IsExactLoaner",Call("DeckId","mage"),mageLoaner.Hero,alteredLoaner), "altered loaner body rejected");
Check(!(bool)Call("IsExactLoaner",Call("DeckId","warrior"),mageLoaner.Hero,mageLoaner.CardIds.ToArray()), "altered loaner ID rejected");
Check(!(bool)Call("IsExactLoaner",long.MaxValue,mageLoaner.Hero,mageLoaner.CardIds.ToArray()), "out-of-range loaner ID rejected");
Check(!(bool)Call("IsExactLoaner",Call("DeckId","custom"),mageLoaner.Hero,mageLoaner.CardIds.ToArray()), "custom deck rejected by loaner exception");
Check((bool)Call("AllowedCache","NetCacheCollection"),"collection cache accepted");
Check(!(bool)Call("AllowedCache","NetCacheQuests"),"unknown cache rejected");
Check((bool)Call("AllowedPreference","COLLECTION_MANAGER"),"collection preference accepted");
Check(!(bool)Call("AllowedPreference","QUESTS"),"unknown preference rejected");
Check(type!.GetMethod("ResolvePublisher", BindingFlags.Static|BindingFlags.NonPublic) != null, "publisher resolver is implemented");
Check(((MethodInfo)Call("ResolvePublisher",typeof(Publisher))).Name == "OnNetCacheObjReceived", "exact generic publisher resolved");
try { Call("ResolvePublisher",typeof(NoPublisher)); throw new Exception("missing publisher accepted"); }
catch (TargetInvocationException e) { Check(e.InnerException is MissingMethodException,"missing publisher fails explicitly"); }
Check(type!.GetMethod("TryGetDefinitionTag", BindingFlags.Static|BindingFlags.NonPublic) != null, "definition tag projection is implemented");
Check(type!.GetMethod("TryGetDbfTag", BindingFlags.Static|BindingFlags.NonPublic) != null, "DBF tag projection is implemented");
void Definition(bool context, bool loaded, bool canonical, string id, int tag, bool applies, int value) {
    object[] args = { context, loaded, canonical, id, tag, 0 };
    Check((bool)Call("TryGetDefinitionTag",args) == applies && (int)args[5] == value,
        "definition " + context + ":" + loaded + ":" + canonical + ":" + id + ":" + tag);
}
void Dbf(bool context, string id, int tag, bool applies, int value) {
    object[] args = { context, id, tag, 0 };
    Check((bool)Call("TryGetDbfTag",args) == applies && (int)args[3] == value,
        "DBF " + context + ":" + id + ":" + tag);
}
foreach (string name in LocalCardCatalog.NativeAuditTags.Except(new[] { "CARD_SET", "FACTION", "DISCARD_CARDS" }))
{
    var tag = name == "DURABILITY" ? SabberStoneCore.Enums.GameTag.HEALTH : Enum.Parse<SabberStoneCore.Enums.GameTag>(name);
    string id = name == "DURABILITY" ? "CS2_091" : "CS2_222";
    var card = SabberStoneCore.Model.Cards.FromId(id);
    int direct = card[name == "DURABILITY" ? SabberStoneCore.Enums.GameTag.DURABILITY : tag];
    Definition(true, true, true, id, (int)tag, true, direct);
    int dbf = direct;
    if (LocalCardCatalog.NativeAuditReferencedTags.Contains(name) && card.RefTags.TryGetValue(tag, out int referenced)) dbf = Math.Max(dbf, referenced);
    Dbf(true, id, (int)tag, true, dbf);
}
foreach (string refName in LocalCardCatalog.NativeAuditReferencedTags)
{
    var refTag = Enum.Parse<SabberStoneCore.Enums.GameTag>(refName);
    var factCard = SabberStoneCore.Model.Cards.FromId("DINO_410");
    factCard.RefTags.TryGetValue(refTag, out int factReferenced);
    object[] refArgs = { true, "DINO_410", (int)refTag, 0 };
    Check((bool)Call("TryGetReferencedTag", refArgs) && (int)refArgs[3] == factReferenced, "referenced " + refName);
    refArgs[0] = false;
    Check(!(bool)Call("TryGetReferencedTag", refArgs), "original referenced " + refName);
}
Dbf(true,"CS2_222",(int)SabberStoneCore.Enums.GameTag.AURA,true,1);
Dbf(true,"CS2_222",(int)SabberStoneCore.Enums.GameTag.CLASS,true,12);
Dbf(true,"CS2_222",(int)SabberStoneCore.Enums.GameTag.COLLECTIBLE,true,1);
Dbf(true,"CS2_124",(int)SabberStoneCore.Enums.GameTag.CHARGE,true,1);
Definition(true,true,true,"CS2_091",45,true,4); // Light's Justice: durability, not engine Health=0.
Definition(true,true,true,"CS2_097",45,true,2); // Truesilver Champion durability.
Definition(true,true,true,"EX1_366",45,true,5); // Sword of Justice durability uses native HEALTH.
Definition(true,true,true,"CS2_088",45,true,6); // Guardian of Kings original health.
Definition(true,true,true,"CS1_112",48,true,5); // Holy Nova original mana.
Definition(true,true,true,"CS2_093",48,true,4); // Consecration original mana.
Definition(true,true,true,"EX1_005",48,true,5); // Leeroy pinned engine mana.
Definition(true,true,true,"EX1_362",47,true,2); // Argent Protector pinned engine attack.
Definition(true,true,true,"EX1_007",45,true,3); // Acolyte of Pain pinned engine health.
Definition(true,true,true,"EX1_398t",47,true,2); // Generated Battle Axe attack.
Definition(true,true,true,"EX1_398t",45,true,2); // Generated Battle Axe durability.

var directDifferences = new (string Id, SabberStoneCore.Enums.GameTag Tag, int Expected)[] {
    ("CS2_088", SabberStoneCore.Enums.GameTag.TAUNT, 0),
    ("CS2_221", SabberStoneCore.Enums.GameTag.CARDRACE, 0),
    ("CS2_231", SabberStoneCore.Enums.GameTag.CARDRACE, 0),
    ("CS2_235", SabberStoneCore.Enums.GameTag.RARITY, 2),
    ("CS2_236", SabberStoneCore.Enums.GameTag.RARITY, 2),
    ("DREAM_01", SabberStoneCore.Enums.GameTag.CANT_BE_TARGETED_BY_SPELLS, 1),
    ("DREAM_01", SabberStoneCore.Enums.GameTag.CANT_BE_TARGETED_BY_HERO_POWERS, 1),
    ("DS1_055", SabberStoneCore.Enums.GameTag.CARDRACE, 0),
    ("EX1_001", SabberStoneCore.Enums.GameTag.CARDRACE, 0),
    ("EX1_002", SabberStoneCore.Enums.GameTag.CARDRACE, 0),
    ("EX1_012", SabberStoneCore.Enums.GameTag.CARDRACE, 0),
    ("EX1_016", SabberStoneCore.Enums.GameTag.CARDRACE, 0),
    ("EX1_025t", SabberStoneCore.Enums.GameTag.RARITY, 1),
    ("EX1_045", SabberStoneCore.Enums.GameTag.CANT_ATTACK, 1),
    ("EX1_059", SabberStoneCore.Enums.GameTag.CARDRACE, 0),
    ("EX1_084", SabberStoneCore.Enums.GameTag.AURA, 1),
    ("EX1_089", SabberStoneCore.Enums.GameTag.CHARGE, 0),
    ("EX1_093", SabberStoneCore.Enums.GameTag.CARDRACE, 0),
    ("EX1_097", SabberStoneCore.Enums.GameTag.CARDRACE, 0),
    ("EX1_110", SabberStoneCore.Enums.GameTag.TAUNT, 0),
    ("EX1_130a", SabberStoneCore.Enums.GameTag.CARDRACE, 0),
    ("EX1_298", SabberStoneCore.Enums.GameTag.CANT_ATTACK, 1),
    ("EX1_350", SabberStoneCore.Enums.GameTag.CARDRACE, 0),
    ("EX1_382", SabberStoneCore.Enums.GameTag.CARDRACE, 0),
    ("EX1_405", SabberStoneCore.Enums.GameTag.CARDRACE, 0),
    ("EX1_563", SabberStoneCore.Enums.GameTag.SPELLPOWER, 5),
    ("EX1_591", SabberStoneCore.Enums.GameTag.CARDRACE, 0),
    ("EX1_595", SabberStoneCore.Enums.GameTag.CARDRACE, 0),
    ("NEW1_020", SabberStoneCore.Enums.GameTag.CARDRACE, 0),
    ("NEW1_023", SabberStoneCore.Enums.GameTag.CANT_BE_TARGETED_BY_SPELLS, 1),
    ("NEW1_023", SabberStoneCore.Enums.GameTag.CANT_BE_TARGETED_BY_HERO_POWERS, 1),
    ("tt_004", SabberStoneCore.Enums.GameTag.CARDRACE, 0)
};
foreach (var difference in directDifferences)
{
    Definition(true,true,true,difference.Id,(int)difference.Tag,true,difference.Expected);
    Dbf(true,difference.Id,(int)difference.Tag,true,difference.Expected);
}
Check(directDifferences.Length == 32, "all observed direct differences are covered");

var flattenedReferences = new (string Id, SabberStoneCore.Enums.GameTag Tag)[] {
    ("CS2_027", SabberStoneCore.Enums.GameTag.TAUNT),
    ("CS2_103", SabberStoneCore.Enums.GameTag.CHARGE),
    ("CS2_146", SabberStoneCore.Enums.GameTag.CHARGE),
    ("CS2_203", SabberStoneCore.Enums.GameTag.SILENCE),
    ("EX1_002", SabberStoneCore.Enums.GameTag.TAUNT),
    ("EX1_048", SabberStoneCore.Enums.GameTag.SILENCE),
    ("EX1_058", SabberStoneCore.Enums.GameTag.TAUNT),
    ("EX1_080", SabberStoneCore.Enums.GameTag.SECRET),
    ("EX1_084", SabberStoneCore.Enums.GameTag.CHARGE),
    ("EX1_093", SabberStoneCore.Enums.GameTag.TAUNT),
    ("EX1_179", SabberStoneCore.Enums.GameTag.FREEZE),
    ("EX1_184", SabberStoneCore.Enums.GameTag.DIVINE_SHIELD),
    ("EX1_186", SabberStoneCore.Enums.GameTag.SECRET),
    ("EX1_274", SabberStoneCore.Enums.GameTag.SECRET),
    ("EX1_362", SabberStoneCore.Enums.GameTag.DIVINE_SHIELD),
    ("EX1_371", SabberStoneCore.Enums.GameTag.DIVINE_SHIELD),
    ("EX1_412", SabberStoneCore.Enums.GameTag.WINDFURY),
    ("EX1_584", SabberStoneCore.Enums.GameTag.SPELLPOWER),
    ("EX1_590", SabberStoneCore.Enums.GameTag.DIVINE_SHIELD),
    ("EX1_612", SabberStoneCore.Enums.GameTag.SECRET),
    ("NEW1_040", SabberStoneCore.Enums.GameTag.TAUNT)
};
foreach (var reference in flattenedReferences)
{
    Definition(true,true,true,reference.Id,(int)reference.Tag,true,0);
    Dbf(true,reference.Id,(int)reference.Tag,true,1);
}
Check(flattenedReferences.Length == 21, "all observed flattened DBF references are covered");
foreach (string id in new[] { "CS2_103", "EX1_084" })
{
    int charge = (int)SabberStoneCore.Enums.GameTag.CHARGE;
    object[] expectedArgs = { true, id, charge, 0 };
    Check((bool)Call("TryGetAuditDefinitionTag", expectedArgs) && (int)expectedArgs[3] == 0,
        id + " audit expects attached direct CHARGE zero");
    int expectedDirect = (int)expectedArgs[3];
    Check((bool)Call("DefinitionTagMatches", true, 0, expectedDirect)
        && (bool)Call("TransportTagMatches", false, 0, false, 0),
        id + " direct-zero definition and omitted transport are valid together");
    Check(!(bool)Call("DefinitionTagMatches", true, 1, expectedDirect)
        && !(bool)Call("TransportTagMatches", true, 1, false, 0),
        id + " actual direct-definition and transport drift remain fatal");
}
Definition(true,true,true,"EX1_287",(int)SabberStoneCore.Enums.GameTag.COUNTER,false,0);
Dbf(true,"EX1_287",(int)SabberStoneCore.Enums.GameTag.COUNTER,true,1);
Definition(false,true,true,"CS1_112",48,false,0);
Definition(true,false,true,"CS1_112",48,false,0);
Definition(true,true,false,"CS1_112",48,false,0);
Definition(true,true,true,"UNSUPPORTED",48,false,0);
Definition(true,true,true,"CS1_112",(int)SabberStoneCore.Enums.GameTag.CARD_SET,false,0);
Definition(true,true,true,"CS1_112",(int)SabberStoneCore.Enums.GameTag.FACTION,false,0);
Dbf(false,"CS1_112",48,false,0);
Dbf(true,"UNSUPPORTED",48,false,0);
Check((bool)Call("DefinitionContext",true,false,true,false,"Starting"), "Starting applies canonical definition baseline");
Check((bool)Call("DefinitionContext",true,false,true,false,"Playing"), "Playing applies canonical definition baseline");
Check(!(bool)Call("DefinitionContext",true,false,true,false,"Menu"), "inactive screen passes definitions through");
Check(!(bool)Call("DefinitionContext",true,true,true,true,"Playing"), "original audit bypasses definition baseline");
Check((bool)Call("DefinitionContext",true,false,true,true,"Menu"), "local UI enables definition and enchantment name context");
Check(!(bool)Call("DefinitionContext",false,false,true,true,"Playing"), "inactive local probe preserves native definitions and names");
Check(!(bool)Call("DefinitionContext",true,false,false,true,"Playing"), "uninitialized scene preserves native definitions and names");
Check((bool)Call("LiveFrameIsStable",true,true,false,false,0,7,7), "synchronized idle live frame is stable");
Check(!(bool)Call("LiveFrameIsStable",true,true,true,false,0,7,7), "busy live frame is skipped");
Check(!(bool)Call("LiveFrameIsStable",true,true,false,true,0,7,7), "processing live frame is skipped");
Check(!(bool)Call("LiveFrameIsStable",true,true,false,false,1,7,7), "queued live frame is skipped");
Check(!(bool)Call("LiveFrameIsStable",true,true,false,false,0,6,7), "unsynchronized turn is skipped");
Check((bool)Call("TransportTagMatches",false,0,false,0), "two omitted transport tags match");
Check((bool)Call("TransportTagMatches",true,0,true,0), "two explicit zero transport tags match");
Check(!(bool)Call("TransportTagMatches",false,0,true,0), "missing expected transport tag fails");
Check(!(bool)Call("TransportTagMatches",true,0,false,0), "unexpected transport tag fails even when zero");
Check(!(bool)Call("TransportTagMatches",true,0,true,1), "wrong transport value fails");
Check(!(bool)Call("TransportTagMatches",true,3,true,4),
    "damaged character native maximum-health drift remains fatal");
Check(!(bool)Call("TransportTagMatches",true,1,true,0),
    "stale native DAMAGE remains a fatal transport mismatch");
Check((bool)Call("DefinitionTagMatches",true,1,1), "canonical definition exact value passes");
Check(!(bool)Call("DefinitionTagMatches",true,0,1), "canonical definition mismatch fails");
Check((bool)Call("DefinitionTagMatches",false,0,1), "dynamic definition is explicitly outside static comparison");
int engineLookups = 0;
Func<string,string> transportIdentity = id => id;
Func<int,SabberStoneCore.Model.Entities.IPlayable> lookup = _ => { engineLookups++; return null; };
object[] hiddenResolve = { "", 81, lookup, transportIdentity, null };
Check(!(bool)Call("TryResolveVisibleEngineEntity",hiddenResolve) && engineLookups == 0,
    "blank native identity skips before engine lookup");
object[] unknownResolve = { "UNSUPPORTED", 82, lookup, transportIdentity, null };
Check(!(bool)Call("TryResolveVisibleEngineEntity",unknownResolve) && engineLookups == 0,
    "unknown native identity skips before engine lookup");
foreach (string legacyId in LocalCardCatalog.LegacyGeneratedPublicIds)
    Check((bool)Call("SupportsPreview", legacyId), "legacy generated public identity is auditable: " + legacyId);
foreach (string quarantinedId in new[] { "EX1_577", "EX1_614", "EX1_614t", "EX1_finkle" })
{
    Definition(true,true,true,quarantinedId,48,false,0);
    Dbf(true,quarantinedId,48,false,0);
    object[] quarantinedResolve = { quarantinedId, 82, lookup, transportIdentity, null };
    Check(!(bool)Call("TryResolveVisibleEngineEntity",quarantinedResolve) && engineLookups == 0,
        "quarantined native identity skips before engine lookup: " + quarantinedId);
}
object[] publicResolve = { "CS2_088", 83, lookup, transportIdentity, null };
Check(!(bool)Call("TryResolveVisibleEngineEntity",publicResolve) && engineLookups == 1,
    "known public identity performs shared-ID engine lookup");
var identityGame = new SabberStoneCore.Model.Game(new SabberStoneCore.Config.GameConfig {
    Player1HeroClass = SabberStoneCore.Enums.CardClass.PALADIN,
    Player2HeroClass = SabberStoneCore.Enums.CardClass.MAGE,
    Shuffle = false, SkipMulligan = true, History = false, Logging = false
});
var visibleEngineEntity = SabberStoneCore.Model.Entities.Entity.FromCard(identityGame.Player1,
    SabberStoneCore.Model.Cards.FromId("CS2_088"), zone:identityGame.Player1.HandZone);
Func<int,SabberStoneCore.Model.Entities.IPlayable> resolvedLookup = id => {
    Check(id == visibleEngineEntity.Id, "public resolver looks up the shared entity ID");
    return visibleEngineEntity;
};
object[] sameIdentityResolve = { "CS2_088", visibleEngineEntity.Id, resolvedLookup, transportIdentity, null };
Check((bool)Call("TryResolveVisibleEngineEntity",sameIdentityResolve)
    && ReferenceEquals(sameIdentityResolve[4], visibleEngineEntity),
    "same public card identity permits live comparison");
foreach (string legacyId in LocalCardCatalog.LegacyGeneratedPublicIds)
{
    var legacyEntity = SabberStoneCore.Model.Entities.Entity.FromCard(identityGame.Player1,
        SabberStoneCore.Model.Cards.FromId(legacyId), zone:identityGame.Player1.BoardZone);
    int beforeLegacyLookup = engineLookups;
    Func<int,SabberStoneCore.Model.Entities.IPlayable> legacyLookup = id => {
        engineLookups++;
        Check(id == legacyEntity.Id, "legacy resolver keeps exact public entity ID: " + legacyId);
        return legacyEntity;
    };
    object[] legacyResolve = { legacyId, legacyEntity.Id, legacyLookup, transportIdentity, null };
    Check((bool)Call("TryResolveVisibleEngineEntity",legacyResolve)
        && ReferenceEquals(legacyResolve[4], legacyEntity) && engineLookups == beforeLegacyLookup + 1,
        "legacy generated public actor resolves canonically: " + legacyId);
}
object[] changedIdentityResolve = { "CS2_087", visibleEngineEntity.Id, resolvedLookup, transportIdentity, null };
Check(!(bool)Call("TryResolveVisibleEngineEntity",changedIdentityResolve) && changedIdentityResolve[4] == null,
    "different public card identity rejects live comparison without returning engine identity");
Func<string,string> remappedIdentity = _ => "CS2_087";
object[] transportedIdentityResolve = { "CS2_087", visibleEngineEntity.Id, resolvedLookup, remappedIdentity, null };
Check((bool)Call("TryResolveVisibleEngineEntity",transportedIdentityResolve)
    && ReferenceEquals(transportedIdentityResolve[4], visibleEngineEntity),
    "transported public identity governs live comparison rather than raw engine ID");
var noCardEntity = (SabberStoneCore.Model.Entities.IPlayable)System.Runtime.CompilerServices.RuntimeHelpers
    .GetUninitializedObject(typeof(SabberStoneCore.Model.Entities.Minion));
Func<int,SabberStoneCore.Model.Entities.IPlayable> noCardLookup = _ => noCardEntity;
object[] noCardResolve = { "CS2_088", 83, noCardLookup, transportIdentity, null };
Check(!(bool)Call("TryResolveVisibleEngineEntity",noCardResolve) && noCardResolve[4] == null,
    "null engine card rejects live comparison without throwing or returning an entity");
Check(type!.GetMethod("ReceiptMatchesActors", BindingFlags.Static|BindingFlags.NonPublic) != null, "native actor receipt comparison is implemented");
Check((bool)Call("ReceiptMatchesActors",receipt,new[]{"CS2_087","EX1_561","EX1_007","EX1_019","CS2_179"}), "native actor order may differ from saved receipt");
var duplicateReceipt = new[]{"CS2_179","CS2_179","EX1_007","EX1_007","CS2_087"};
Check((bool)Call("ReceiptMatchesActors",duplicateReceipt,new[]{"CS2_087","EX1_007","CS2_179","EX1_007","CS2_179"}), "native actors preserve duplicate receipt quantities");
Check(!(bool)Call("ReceiptMatchesActors",duplicateReceipt,new[]{"CS2_087","EX1_007","CS2_179","CS2_179","CS2_179"}), "same distinct IDs with wrong quantities fail receipt proof");
Check(!(bool)Call("ReceiptMatchesActors",receipt,new[]{"CS2_179","EX1_019","EX1_007","EX1_561"}), "missing native actor fails receipt proof");
Check(!(bool)Call("ReceiptMatchesActors",receipt,new[]{"CS2_179","EX1_019","EX1_007","EX1_561","CS2_087","CS2_087"}), "extra native actor fails receipt proof");
Check(!(bool)Call("ReceiptMatchesActors",receipt,new[]{"CS2_179","EX1_019","EX1_007","EX1_561",null}), "unloaded actor definition fails receipt proof");
Check(!(bool)Call("ReceiptMatchesActors",receipt,null), "absent native actors fail receipt proof");
Check(!(bool)Call("ReceiptMatchesActors",null,receipt), "unarmed replay fails receipt proof");
Check(!(bool)Call("ReceiptMatchesActors",Array.Empty<string>(),Array.Empty<string>()), "two empty snapshots never prove a receipt");
var invalidReceipt = new[]{"CS2_179","EX1_019","EX1_007","EX1_561","UNSUPPORTED"};
Check(!(bool)Call("ReceiptMatchesActors",invalidReceipt,invalidReceipt), "matching unsupported IDs never prove a valid receipt");
Check((int)Call("ProjectedPackCount",0,0,false) == 0, "zero packs project an empty native scene");
Check((int)Call("ProjectedPackCount",6,0,false) == 6, "idle native count matches spendable inventory");
Check((int)Call("ProjectedPackCount",5,1,false) == 6, "pending receipt adds one recoverable pack");
Check((int)Call("ProjectedPackCount",5,1,true) == 5, "in-flight receipt is not counted twice");
Check((int)Call("ProjectedPackCount",5,40,false) == 45, "pending 40-pack batch restores every recoverable pack to the tray");
Check((int)Call("ProjectedPackCount",5,40,true) == 5, "in-flight batch is not counted twice");
Reject("ProjectedPackCount", 0, -1, false);
IReadOnlyList<(int boosterId, int count)> PackStacks((string type, int count)[] inventory,
    string pendingType = null, int pendingPacks = 0, bool revealRunning = false) =>
    (IReadOnlyList<(int boosterId, int count)>)Call("ProjectedPackStacks", inventory, pendingType, pendingPacks, revealRunning);
Check(PackStacks(Array.Empty<(string, int)>()).Count == 0, "empty inventory projects no booster stacks");
var allPackTypes = new[] { FairPackRules.Legacy }.Concat(FairPackRules.PackTypes).ToArray();
foreach (int boosterId in allPackTypes.Select(FairPackRules.NativeBoosterId)
    .Concat(new[] { 975, 1047, 1057, -1, 0, 2, 629, int.MaxValue }).Distinct())
foreach (bool probeActive in new[] { false, true })
foreach (bool nativeOpenable in new[] { false, true })
{
    const string nativeReason = "GLUE_STORE_PREORDER_MARCH";
    var arguments = new object[] { probeActive, boosterId, nativeOpenable, nativeReason };
    bool projected = (bool)Call("TryProjectPackOpenability", arguments);
    bool expected = probeActive && allPackTypes.Select(FairPackRules.NativeBoosterId).Contains(boosterId);
    Check(projected == expected && (bool)arguments[2] == (expected || nativeOpenable)
        && (string)arguments[3] == (expected ? string.Empty : nativeReason),
        "pack release only overrides active known booster " + boosterId + "/" + probeActive + "/" + nativeOpenable);
}
Check((string)Call("PackTypeForBooster", 975) == "EMERALD_DREAM"
    && (string)Call("PackTypeForBooster", 1047) == "ESCAPEFROM_VIOLET_HOLD"
    && !FairPackRules.IsKnown("BE") && Call("PackTypeForBooster", 1057) == null,
    "release fixtures include active EDR/JAIL and exclude inactive BE");
var mixedPacks = allPackTypes.Select((packType, index) => (packType, index + 2)).Reverse().ToArray();
var mixedStacks = PackStacks(mixedPacks);
Check(mixedStacks.Select(stack => stack.boosterId).SequenceEqual(allPackTypes.Select(FairPackRules.NativeBoosterId))
    && mixedStacks.Select(stack => stack.count).SequenceEqual(Enumerable.Range(2, allPackTypes.Length)),
    "mixed inventory projects one real booster per type in Legacy then active pack order");
Check(mixedStacks[0].boosterId == 1 && mixedStacks[1].boosterId == 713
    && mixedStacks.Select(stack => stack.boosterId).Distinct().Count() == mixedStacks.Count,
    "Legacy and Standard use pinned distinct native booster IDs");
var pendingPacks = new[] { (FairPackRules.Standard, 3), (FairPackRules.Legacy, 2) };
Check(PackStacks(pendingPacks, FairPackRules.Standard, 1).SequenceEqual(new[] { (1, 2), (713, 4) }),
    "pending Standard receipt adds one only to Standard stack");
Check(PackStacks(pendingPacks, FairPackRules.Legacy, 1).SequenceEqual(new[] { (1, 3), (713, 3) }),
    "pending Legacy receipt adds one only to Legacy stack");
Check(PackStacks(pendingPacks, FairPackRules.Standard, 1, true).SequenceEqual(new[] { (1, 2), (713, 3) }),
    "running receipt does not add another pack to any stack");
Check(PackStacks(pendingPacks, FairPackRules.Standard, 40).SequenceEqual(new[] { (1, 2), (713, 43) }),
    "pending batch adds every pack only to its own stack");
Check(PackStacks(pendingPacks, FairPackRules.Standard, 40, true).SequenceEqual(new[] { (1, 2), (713, 3) }),
    "running batch does not add packs to any stack");
Check(PackStacks(pendingPacks, FairPackRules.Standard).SequenceEqual(new[] { (1, 2), (713, 3) }),
    "stale pending type without pending receipt adds nothing");
Check(PackStacks(new[] { (FairPackRules.Standard, 0), (FairPackRules.Legacy, 0) }).Count == 0,
    "zero-count inventory omits empty stacks");
Check(PackStacks(new[] { (FairPackRules.Standard, 0) }, FairPackRules.Standard, 1).SequenceEqual(new[] { (713, 1) }),
    "zero-count type with pending receipt remains draggable");
Check(PackStacks(Array.Empty<(string, int)>(), FairPackRules.Standard, 1).SequenceEqual(new[] { (713, 1) }),
    "pending type absent from inventory still projects recoverable stack");
Check(PackStacks(Array.Empty<(string, int)>(), FairPackRules.Standard, 40).SequenceEqual(new[] { (713, 40) }),
    "exhausted inventory retains all pending batch packs");
Check(PackStacks(new[] { (FairPackRules.Standard, 0) }, FairPackRules.Standard, 1, true).Count == 0,
    "zero-count pending stack disappears during reveal");
var resumeInventory = new[] { (FairPackRules.Standard, 0), (FairPackRules.Legacy, 2) };
var resumeStacks = PackStacks(resumeInventory, FairPackRules.Standard, 1);
var runningStacks = PackStacks(resumeInventory, FairPackRules.Standard, 1, true);
Check(((int[])Call("BoosterIdsToZero", resumeStacks.Select(stack => stack.boosterId), runningStacks))
    .SequenceEqual(new[] { 713 }), "resuming exhausted Standard while dragging Legacy zeroes only Standard actor");
Check(((int[])Call("BoosterIdsToZero", resumeStacks.Select(stack => stack.boosterId), resumeStacks)).Length == 0,
    "cleanup keeps every actor when all booster stacks remain present");
Check(((int[])Call("BoosterIdsToZero", new[] { 1, 713 }, PackStacks(Array.Empty<(string, int)>())))
    .SequenceEqual(new[] { 1, 713 }), "empty tray zeroes all previous actors");
Check(((int[])Call("BoosterIdsToZero", Array.Empty<int>(), runningStacks)).Length == 0,
    "cleanup needs no actors before native tray initializes");
foreach (string packType in allPackTypes)
{
    Check(FairPackRules.IsKnown(packType) && (string)Call("PackTypeForBooster", FairPackRules.NativeBoosterId(packType)) == packType,
        "native booster round-trips known type " + packType);
    var expected = new[] { (FairPackRules.NativeBoosterId(packType), 1) };
    Check(PackStacks(Array.Empty<(string, int)>(), packType, 1).SequenceEqual(expected),
        "pending receipt projects exact native type " + packType);
}
foreach (int unknownBooster in new[] { -1, 0, 2, int.MaxValue })
    Check(Call("PackTypeForBooster", unknownBooster) == null, "unknown booster ID never guesses pack type " + unknownBooster);
Reject("ProjectedPackStacks", new[] { ("UNKNOWN", 1) }, null, 0, false);
Reject("ProjectedPackStacks", new[] { (FairPackRules.Standard, -1) }, null, 0, false);
Reject("ProjectedPackStacks", Array.Empty<(string, int)>(), null, 1, false);
Reject("ProjectedPackStacks", Array.Empty<(string, int)>(), "UNKNOWN", 1, false);
Reject("ProjectedPackStacks", Array.Empty<(string, int)>(), null, -1, false);
var expansionReceipt = new[] { "EDR_001", "EDR_001", "EDR_001", "EDR_001", "EDR_001" };
Check(LocalProfile.KnownLedgerCard("EDR_001") && !supported.Contains("EDR_001"),
    "expansion receipt fixture belongs to typed ledger outside legacy projection pool");
Check(((string[])Call("Receipt", new object[] { expansionReceipt })).SequenceEqual(expansionReceipt),
    "typed expansion receipt passes native replay validation");
Check((bool)Call("ReceiptMatchesActors", expansionReceipt, expansionReceipt),
    "typed expansion actors prove pending receipt for Done acknowledgement");
var expansionGrants = (IReadOnlyDictionary<string,int>)Call("NewGrantCounts", new object[] { new[] {
    new LocalPackCard { Id = "EDR_001", DuplicateGold = 0 },
    new LocalPackCard { Id = "EDR_001", DuplicateGold = 0 }
} });
Check(expansionGrants.Count == 1 && expansionGrants["EDR_001"] == 2,
    "typed expansion ownership grants reach native collection");
var grants = (IReadOnlyDictionary<string,int>)Call("NewGrantCounts", new object[] { new[] {
    new LocalPackCard { Id = "CS2_179", DuplicateGold = 0 },
    new LocalPackCard { Id = "CS2_179", DuplicateGold = 0 },
    new LocalPackCard { Id = "EX1_007", DuplicateGold = 10 },
    new LocalPackCard { Id = "CS2_087", DuplicateGold = 0 }
} });
Check(grants.Count == 2 && grants["CS2_179"] == 2 && grants["CS2_087"] == 1,
    "live native grants include only committed nonduplicates with quantities");
var premiumReceipt = new[] {
    new LocalPackCard { Id = "CS2_179", Premium = 1 }, new LocalPackCard { Id = "CS2_179" },
    new LocalPackCard { Id = "EX1_007", Premium = 1 }, new LocalPackCard { Id = "EX1_561", Premium = 3 },
    new LocalPackCard { Id = "CS2_087" }
};
var premiumKeys = premiumReceipt.Select(card => card.Key).ToArray();
Check(((string[])Call("Receipt", new object[] { premiumKeys })).SequenceEqual(premiumKeys)
    && (bool)Call("ReceiptMatchesActors", premiumKeys, premiumKeys.Reverse().ToArray())
    && !(bool)Call("ReceiptMatchesActors", premiumKeys, premiumReceipt.Select(card => card.Id).ToArray()), "premium native receipt matches exact versions and multiplicities");
var premiumDelta = (IReadOnlyDictionary<string, int>)Call("NewGrantCounts", new object[] { premiumReceipt });
Check(premiumDelta["CS2_179"] == 1 && premiumDelta["CS2_179:1"] == 1 && premiumDelta["EX1_561:3"] == 1,
    "pack native deltas separate Normal, Golden, Signature");
foreach (int quality in new[] { -1, 2, 4 }) Reject("NewGrantCounts", new object[] { new[] { new LocalPackCard { Id = "CS2_179", Premium = quality } } });
Reject("Receipt", new object[] { premiumKeys.Select(key => key == "CS2_179:1" ? "CS2_179:2" : key).ToArray() });
bool Done(bool active, bool running, bool shown, bool sameDirector, string[] saved, string[] actors, bool[] revealed,
    bool alreadyAcknowledged = false, string[] page = null) =>
    (bool)Call("CanAcknowledgePackReveal", active, running, shown, sameDirector, saved, actors, revealed, alreadyAcknowledged, page);
Check(Done(true,true,true,true,duplicateReceipt,
    new[]{"CS2_087","EX1_007","CS2_179","EX1_007","CS2_179"}, new[]{true,true,true,true,true}),
    "Done accepts exact five revealed actors with duplicate quantities");
Check(!Done(false,true,true,true,receipt,receipt,new[]{true,true,true,true,true}), "Done rejects inactive probe");
Check(!Done(true,false,true,true,receipt,receipt,new[]{true,true,true,true,true}), "Done rejects no running reveal");
Check(!Done(true,true,false,true,receipt,receipt,new[]{true,true,true,true,true}), "Done rejects hidden button");
Check(!Done(true,true,true,false,receipt,receipt,new[]{true,true,true,true,true}), "Done rejects stale director");
Check(!Done(true,true,true,true,receipt,receipt,new[]{true,true,false,true,true}), "Done rejects unrevealed actor");
Check(!Done(true,true,true,true,receipt,receipt,new[]{true,true,true,true}), "Done rejects missing reveal state");
Check(!Done(true,true,true,true,receipt,new[]{"CS2_179","EX1_019","EX1_007","EX1_561"},new[]{true,true,true,true}),
    "Done rejects missing actor");
Check(!Done(true,true,true,true,receipt,null,new[]{true,true,true,true,true}), "Done rejects absent actors");
Check(!Done(true,true,true,true,receipt,receipt,new[]{true,true,true,true,true},true), "duplicate Done is not acknowledged twice");
// Native mass highlights are shuffled groups of five, not the full 5N receipt.
var highlightBatch = premiumKeys.Concat(receipt).ToArray();
var highlightActors = new[] { "EX1_561:3", "CS2_179:1", "EX1_007:1", "CS2_087", "CS2_179" };
Check(Done(true,true,true,true,highlightBatch,highlightActors,new[]{true,true,true,true,true}, page: highlightActors),
    "mass Done accepts five revealed highlights from a larger receipt with exact Golden and Signature versions");
Check(!Done(true,true,true,true,highlightBatch,new[]{"EX1_561:3","EX1_561:3","EX1_007:1","CS2_087","CS2_179"},new[]{true,true,true,true,true}, page: highlightActors),
    "mass highlights reject duplicate actors beyond the saved quantity");
Check(!Done(true,true,true,true,highlightBatch,new[]{"EX1_561:1","CS2_179:1","EX1_007:1","CS2_087","CS2_179"},new[]{true,true,true,true,true}, page: highlightActors),
    "mass highlights reject a substituted premium version");
Check(!Done(true,true,true,true,highlightBatch,highlightActors,new[]{true,true,false,true,true}, page: highlightActors)
    && !Done(true,true,true,false,highlightBatch,highlightActors,new[]{true,true,true,true,true}, page: highlightActors),
    "mass highlights reject unrevealed actors and a stale director");
Check(!Done(true,true,true,true,highlightBatch.Take(9).ToArray(),highlightActors,new[]{true,true,true,true,true}, page: highlightActors),
    "mass highlights reject an incomplete batch receipt");
Check(Done(true,true,true,true,premiumKeys,highlightActors,new[]{true,true,true,true,true}, page: highlightActors)
    && !Done(true,true,true,true,premiumKeys,premiumReceipt.Select(card => card.Id).ToArray(),new[]{true,true,true,true,true}),
    "single-pack Done still requires all five exact premium versions");
Check(!Done(true,true,true,true,highlightBatch,highlightActors,new[]{true,true,true,true,true}),
    "mass highlights require a captured native page, never a batch-wide subset");
var wrongPage = highlightActors.ToArray(); wrongPage[2] = "EX1_019";
Check(!Done(true,true,true,true,highlightBatch,wrongPage,new[]{true,true,true,true,true}, page: highlightActors),
    "another card present elsewhere in the batch cannot substitute for the native page");
wrongPage = highlightActors.ToArray(); wrongPage[0] = "EX1_561";
Check(!Done(true,true,true,true,highlightBatch,wrongPage,new[]{true,true,true,true,true}, page: highlightActors),
    "another premium present elsewhere in the batch cannot substitute for the native page");
string Completion(bool active, bool running, bool sameDirector, bool acknowledged) =>
    Call("PackCompletion", active, running, sameDirector, acknowledged).ToString()!;
Check(Completion(false,false,false,false) == "Passthrough", "inactive completion keeps original native lifecycle");
Check(Completion(true,false,true,true) == "Ignore", "duplicate completion after cleanup is ignored");
Check(Completion(true,true,false,false) == "Ignore", "stale prior director cannot fail a newer reveal");
Check(Completion(true,true,true,false) == "Fail", "current director cannot finish before durable Done acknowledgement");
Check(Completion(true,true,true,true) == "Complete", "acknowledged exact director completes its reveal");
Check(NativeMenuFlow.HomeRoute(NativeHomeControl.Traditional) == NativeMenuTarget.Picker, "home Traditional routes to picker");
Check(NativeMenuFlow.HomeRoute(NativeHomeControl.Collection) == NativeMenuTarget.Collection, "home Collection routes to collection");
Check(NativeMenuFlow.HomeRoute(NativeHomeControl.OpenPacks) == NativeMenuTarget.Pack, "home Open Packs routes to pack opening");
Check(NativeMenuFlow.HomeRoute(NativeHomeControl.Unsupported) == NativeMenuTarget.None, "unsupported home control has no route");
Check(NativeMenuFlow.HomeRoute(NativeHomeControl.Store) == NativeMenuTarget.LocalShop, "home Store press is the local shop's own event, not a blocked control");
Check(NativeMenuFlow.CollectionExitAllowed(true, false, false), "online collection exit keeps native route");
Check(NativeMenuFlow.CollectionExitAllowed(false, true, true), "local picker-origin collection exit keeps picker route");
Check(!NativeMenuFlow.CollectionExitAllowed(false, true, false), "local home-origin collection exit keeps native offline fallback");
Check(!NativeMenuFlow.CollectionExitAllowed(false, false, true), "inactive picker-origin collection exit keeps native offline fallback");
Check(NativeMenuFlow.FatalPackDisconnectReturnsHome(false), "fatal pack disconnect returns idle pack scene home");
Check(!NativeMenuFlow.FatalPackDisconnectReturnsHome(true), "fatal pack disconnect keeps active reveal");
var rewardSummary = LocalRewardPresentation.Summary(new LocalMatchRecord {
    Id = "reward-match", Result = "Won", Gold = 90, BonusGold = 40, PackReward = true,
    RewardFacts = new LocalRewardFacts { DistinctCardsPlayed = 6, EnemyMinionsDefeated = 5, MinimumHealth = 5 }
});
Check(rewardSummary.Header == "VICTORY" && rewardSummary.Items.Count == 2
    && rewardSummary.Items[0].Kind == LocalRewardKind.Gold && rewardSummary.Items[0].Amount == 90
    && rewardSummary.Items[1].Kind == LocalRewardKind.Pack && rewardSummary.Items[1].Amount == 1,
    "reward summary preserves exact gold and optional pack actors");
Check(rewardSummary.Body.Contains("6 different cards") && rewardSummary.Body.Contains("5 enemy minions")
    && rewardSummary.Body.Contains("5 Health"), "reward summary names earned encounter facts");
var noAchievements = LocalAchievementPresentation.Build(Array.Empty<LocalAchievementGrantReceipt>());
Check(noAchievements.Count == 0 && noAchievements.Caption == "" && noAchievements.Details == "",
    "zero saved grants produce no achievement announcement");
var oneAchievement = LocalAchievementPresentation.Build(new[] {
    LocalAchievementGrantReceipt.FromCatalog("calculated")
});
Check(oneAchievement.Count == 1 && oneAchievement.Caption == "Calculated."
    && oneAchievement.Details == "Calculated. — Card back: Wyrmrest",
    "one saved cosmetic grant uses catalog name and approved cosmetic name");
var groupedAchievements = LocalAchievementPresentation.Build(new[] {
    LocalAchievementGrantReceipt.FromCatalog("seven-card-pickup"),
    LocalAchievementGrantReceipt.FromCatalog("aggressively-average"),
    LocalAchievementGrantReceipt.FromCatalog("welcome-to-legend")
});
Check(groupedAchievements.Count == 3 && groupedAchievements.Caption == "Seven-Card Pickup + 2 more"
    && groupedAchievements.Caption.Length <= LocalAchievementPresentation.CaptionLimit
    && groupedAchievements.Gold == 200 && groupedAchievements.Dust == 0 && groupedAchievements.StandardPacks == 3
    && groupedAchievements.Details.Contains("Seven-Card Pickup — 200 gold")
    && groupedAchievements.Details.Contains("Aggressively Average — 3 Standard packs")
    && groupedAchievements.Details.Contains("Welcome to Legend — Linked milestone: Legend"),
    "many saved grants produce one bounded caption and exact saved reward rows");
var toastLifetime = new LocalAchievementToastLifetime();
int toastGeneration = toastLifetime.Begin();
Check(!toastLifetime.Destroyed(toastGeneration, false, true) && toastLifetime.Active,
    "foreign toast destruction cannot finish the owned presentation");
Check(toastLifetime.ObserveNormalHide(toastGeneration, true)
    && toastLifetime.Destroyed(toastGeneration, true, true) && !toastLifetime.Active,
    "held caption plus matching normal hide followed by owned destruction completes the toast");
int retiredToastGeneration = toastLifetime.Begin();
toastLifetime.Retire();
Check(!toastLifetime.ObserveNormalHide(retiredToastGeneration, true)
    && !toastLifetime.Destroyed(retiredToastGeneration, true, true),
    "forced retirement and later scene destruction cannot complete the toast");
int unhiddenToastGeneration = toastLifetime.Begin();
Check(!toastLifetime.Destroyed(unhiddenToastGeneration, true, true),
    "owned scene destruction without normal hide is not completion");
int invisibleToastGeneration = toastLifetime.Begin();
toastLifetime.ObserveNormalHide(invisibleToastGeneration, true);
Check(!toastLifetime.Destroyed(invisibleToastGeneration, true, false),
    "normal hide cannot acknowledge a toast without a confirmed active-caption hold");
var achievementRewardSummary = LocalRewardPresentation.Summary(new LocalMatchRecord {
    Id = "achievement-reward", Result = "Won", Turns = 4, RewardVersion = LocalEconomy.RollRewardVersion,
    Gold = 50, DustReward = 10, PackReward = true,
    Packs = new List<LocalPackCount> { new LocalPackCount { PackType = "CORE", Count = 1 } },
    RewardFacts = new LocalRewardFacts(), AchievementGrants = new List<LocalAchievementGrantReceipt> {
        LocalAchievementGrantReceipt.FromCatalog("seven-card-pickup"),
        LocalAchievementGrantReceipt.FromCatalog("fine-ill-do-it-myself"),
        LocalAchievementGrantReceipt.FromCatalog("aggressively-average"),
        LocalAchievementGrantReceipt.FromCatalog("calculated")
    }
});
Check(achievementRewardSummary.Items.Count == 4
    && achievementRewardSummary.Items.Count(item => item.Kind == LocalRewardKind.Gold && item.Amount == 250) == 1
    && achievementRewardSummary.Items.Count(item => item.Kind == LocalRewardKind.Dust && item.Amount == 160) == 1
    && achievementRewardSummary.Items.Count(item => item.Kind == LocalRewardKind.Pack && item.PackType == "CORE" && item.Amount == 1) == 1
    && achievementRewardSummary.Items.Count(item => item.Kind == LocalRewardKind.Pack
        && item.PackType == FairPackRules.Standard && item.Amount == 3) == 1,
    "base and saved achievement rewards aggregate by currency and exact pack type");
var sharedStandardPack = LocalRewardPresentation.Summary(new LocalMatchRecord {
    Id = "shared-standard-pack", Result = "Won", Turns = 4, RewardVersion = LocalEconomy.RollRewardVersion,
    PackReward = true, Packs = new List<LocalPackCount> { new LocalPackCount { PackType = FairPackRules.Standard, Count = 1 } },
    RewardFacts = new LocalRewardFacts(),
    AchievementGrants = new List<LocalAchievementGrantReceipt> { LocalAchievementGrantReceipt.FromCatalog("aggressively-average") }
});
Check(sharedStandardPack.Items.Count(item => item.Kind == LocalRewardKind.Pack) == 1
    && sharedStandardPack.Items.Single(item => item.Kind == LocalRewardKind.Pack).Amount == 4,
    "base and achievement Standard packs collapse into one native stack");
Check(LocalRewardSafety.BoosterId(LocalEconomy.RollRewardVersion, FairPackRules.Standard,
        FairPackRules.NativeBoosterId, id => id == FairPackRules.NativeBoosterId(FairPackRules.Standard))
        == FairPackRules.NativeBoosterId(FairPackRules.Standard)
    && LocalRewardSafety.SameBooster(713, 3, 713, 3)
    && !LocalRewardSafety.SameBooster(713, 3, 713, 1)
    && !LocalRewardSafety.SameBooster(713, 0, 713, 0),
    "V4 Standard booster mapping and equality preserve the saved pack count");
Check(achievementRewardSummary.Body.Contains("Achievements:")
    && achievementRewardSummary.Body.Contains("Calculated. — Card back: Wyrmrest")
    && !achievementRewardSummary.Body.Contains("different cards")
    && !achievementRewardSummary.Body.Contains("defeated minions")
    && !achievementRewardSummary.Body.Contains("health bonus")
    && !achievementRewardSummary.Body.Contains("bonus pack"),
    "V4 details list saved achievements without habit-copy spam");
var manyAchievementGrants = LocalAchievementCatalog.All.Take(5)
    .Select(row => LocalAchievementGrantReceipt.FromCatalog(row.Id)).ToList();
var manyAchievementSummary = LocalRewardPresentation.Summary(new LocalMatchRecord {
    Id = "many-achievements", Result = "Won", Turns = 8, RewardVersion = LocalEconomy.RollRewardVersion,
    RewardFacts = new LocalRewardFacts(), AchievementGrants = manyAchievementGrants
});
Check(manyAchievementSummary.Body.Contains("5 achievement rewards saved.", StringComparison.Ordinal)
    && manyAchievementSummary.Body.Contains(
        "Open Feats from the home screen to inspect every condition and exact reward.", StringComparison.Ordinal)
    && !manyAchievementSummary.Body.Contains(LocalAchievementPresentation.Build(manyAchievementGrants).Details,
        StringComparison.Ordinal),
    "large achievement result stays readable and directs exact reward inspection to the browser");
var progressSummary = LocalRewardPresentation.Summary(new LocalMatchRecord {
    Id = "progress-match", Result = "Won", Turns = 4, Gold = 50,
    RewardFacts = new LocalRewardFacts { DistinctCardsPlayed = 2, EnemyMinionsDefeated = 3, MinimumHealth = 20 }
});
Check(progressSummary.Body.Contains("2/6 different cards") && progressSummary.Body.Contains("3/5 enemy minions")
    && progressSummary.Body.Contains("Minimum observed Health: 20")
    && !progressSummary.Body.Contains("dropping to 20 Health"),
    "reward summary reports unearned progress and observed minimum Health truthfully");
var zeroSummary = LocalRewardPresentation.Summary(new LocalMatchRecord {
    Id = "zero-match", Result = "Lost", Turns = 8, RewardVersion = 4,
    Gold = 0, RewardFacts = new LocalRewardFacts()
});
Check(zeroSummary.Items.Count == 0
    && LocalRewardPresentation.NoItemsText(zeroSummary) == "No match rewards: only victories roll a reward.",
    "ordinary loss bypasses reward boxes and names the absent victory reward");
foreach (string outcome in new[] { "Lost", "Tied" })
{
    var paid = LocalRewardPresentation.Summary(new LocalMatchRecord {
        Id = "paid-" + outcome, Result = outcome, Turns = 4, RewardVersion = 5,
        Gold = 14, DustReward = 9, RewardFacts = new LocalRewardFacts()
    });
    Check(paid.Items.Count == 2 && paid.Items.Any(i => i.Kind == LocalRewardKind.Gold && i.Amount == 14)
        && paid.Items.Any(i => i.Kind == LocalRewardKind.Dust && i.Amount == 9)
        && paid.Body.Contains("1/3 of a victory roll"), "V5 paid loss/draw presents both native reward items");
}
var shortLoss = LocalRewardPresentation.Summary(new LocalMatchRecord {
    Id = "short-loss", Result = "Lost", Turns = 3, RewardVersion = 5, RewardFacts = new LocalRewardFacts()
});
Check(shortLoss.Items.Count == 0 && LocalRewardPresentation.NoItemsText(shortLoss).Contains("before four turns"),
    "V5 short loss retains no-reward card");
var cosmeticAchievementLoss = LocalRewardPresentation.Summary(new LocalMatchRecord {
    Id = "cosmetic-achievement-loss", Result = "Lost", Conceded = true, Turns = 8, RewardVersion = LocalEconomy.RollRewardVersion,
    RewardFacts = new LocalRewardFacts(), AchievementGrants = new List<LocalAchievementGrantReceipt> {
        LocalAchievementGrantReceipt.FromCatalog("the-board-is-a-workplace-hazard")
    }
});
Check(cosmeticAchievementLoss.Items.Count == 0
    && LocalRewardPresentation.NoItemsText(cosmeticAchievementLoss)
        == "Achievement reward saved. Open Details. No ordinary victory reward."
    && cosmeticAchievementLoss.Body.Contains("The Board Is a Workplace Hazard — Card back: Lightforged", StringComparison.Ordinal),
    "cosmetic-only loss keeps zero fake boxes but acknowledges the saved achievement reward");
Check(LocalRewardPresentation.CanServiceSettlementRetry(true, "Result", true),
    "stable completed GAMEPLAY services settlement retry without a reward receipt");
Check(!LocalRewardPresentation.CanServiceSettlementRetry(false, "Result", true)
    && !LocalRewardPresentation.CanServiceSettlementRetry(true, "Playing", true)
    && !LocalRewardPresentation.CanServiceSettlementRetry(true, "Result", false),
    "unready, active-match, and unrelated scenes do not service settlement retry");
Check(LocalRewardPresentation.ShouldRouteResultHome(true, true, "Result", true),
    "completed GAMEPLAY receipt routes to HUB before reward presentation");
Check(!LocalRewardPresentation.ShouldRouteResultHome(false, true, "Result", true)
    && !LocalRewardPresentation.ShouldRouteResultHome(true, false, "Result", true)
    && !LocalRewardPresentation.ShouldRouteResultHome(true, true, "Playing", true)
    && !LocalRewardPresentation.ShouldRouteResultHome(true, true, "Result", false),
    "unready, receipt-free, alive, and non-GAMEPLAY contexts do not reroute");
Check(NativeUIProjection.PlayedDeckId("custom:910000012") == 910000012L
    && NativeUIProjection.PlayedDeckId("mage") == NativeUIProjection.DeckId("mage")
    && NativeUIProjection.PlayedDeckId("custom:x") == 0 && NativeUIProjection.PlayedDeckId("custom:-4") == 0
    && NativeUIProjection.PlayedDeckId("no-such-deck") == 0 && NativeUIProjection.PlayedDeckId(null) == 0,
    "post-match picker reselects the played deck from its saved key; unknown keys keep the native default");
Check(LocalRewardPresentation.CanPresentInScene(true, true),
    "ready HUB permits reward summary");
Check(!LocalRewardPresentation.CanPresentInScene(true, false)
    && !LocalRewardPresentation.CanPresentInScene(false, true),
    "GAMEPLAY and unready HUB cannot present reward summary");
Check(LocalRewardPresentation.CanInstantiate("m", 4, "m", 4, LocalRewardPhase.LoadingRewardBoxes,
    "m", true, false, true), "current reward prefab callback may instantiate");
Check(!LocalRewardPresentation.CanInstantiate("m", 3, "m", 4, LocalRewardPhase.LoadingRewardBoxes,
    "m", true, false, true), "stale reward generation cannot instantiate");
Check(!LocalRewardPresentation.CanInstantiate("m", 4, "m", 4, LocalRewardPhase.Closing,
    "m", true, false, true), "closing reward flow cannot instantiate");
Check(!LocalRewardPresentation.CanInstantiate("m", 4, "m", 4, LocalRewardPhase.LoadingRewardBoxes,
    "other", true, false, true), "different pending receipt cannot instantiate");
Check(LocalRewardPresentation.CanAcknowledge("m", 4, "m", 4, LocalRewardPhase.Closing, true, 3, 2),
    "matching closing owner with all actors opened can acknowledge");
Check(!LocalRewardPresentation.CanAcknowledge("m", 4, "m", 4, LocalRewardPhase.DoneReady, true, 3, 2),
    "Done button alone cannot acknowledge rewards");
Check(!LocalRewardPresentation.CanAcknowledge("m", 4, "m", 4, LocalRewardPhase.Closing, true, 1, 2),
    "partially opened reward actors cannot acknowledge");
Check(LocalRewardPresentation.RewardEscape(true, LocalRewardPhase.DoneReady, 3, 2)
    == LocalRewardEscapeAction.NavigateBack,
    "completed owned reward Escape uses and pops the native back handler");
Check(LocalRewardPresentation.RewardEscape(true, LocalRewardPhase.Revealing, 1, 2)
        == LocalRewardEscapeAction.Consume
    && LocalRewardPresentation.RewardEscape(true, LocalRewardPhase.DoneReady, 1, 2)
        == LocalRewardEscapeAction.Consume
    && LocalRewardPresentation.RewardEscape(true, LocalRewardPhase.Closing, 3, 2)
        == LocalRewardEscapeAction.Consume,
    "owned incomplete and closing reward Escape cannot open Game Menu");
Check(LocalRewardPresentation.RewardEscape(true, LocalRewardPhase.SaveError, 3, 2)
        == LocalRewardEscapeAction.Passthrough
    && LocalRewardPresentation.RewardEscape(true, LocalRewardPhase.Idle, 3, 2)
        == LocalRewardEscapeAction.Passthrough
    && LocalRewardPresentation.RewardEscape(false, LocalRewardPhase.DoneReady, 3, 2)
        == LocalRewardEscapeAction.Passthrough,
    "failed, idle, and foreign reward states preserve native Escape");
Check(LocalRewardPresentation.UnexpectedDestroy(true, LocalRewardPhase.Revealing),
    "owned reveal destruction falls back");
Check(!LocalRewardPresentation.UnexpectedDestroy(true, LocalRewardPhase.Closing),
    "normal closing destruction stays inert");
Check(LocalRewardPresentation.SuppressArenaNarrative(true, true, LocalRewardPhase.Revealing)
    && LocalRewardPresentation.SuppressArenaNarrative(true, true, LocalRewardPhase.DoneReady),
    "exact current owned reward display suppresses Arena narration while visible");
Check(!LocalRewardPresentation.SuppressArenaNarrative(false, true, LocalRewardPhase.Revealing)
    && !LocalRewardPresentation.SuppressArenaNarrative(true, false, LocalRewardPhase.Revealing)
    && !LocalRewardPresentation.SuppressArenaNarrative(true, true, LocalRewardPhase.Closing),
    "absent, replaced, and stale reward displays preserve Arena narration");
Check(LocalRewardPresentation.SuppressRankedIntro(true, true, true),
    "local initialized Tournament suppresses ranked intro");
Check(!LocalRewardPresentation.SuppressRankedIntro(false, true, true)
    && !LocalRewardPresentation.SuppressRankedIntro(true, false, true)
    && !LocalRewardPresentation.SuppressRankedIntro(true, true, false),
    "ranked intro suppression preserves all other callers");
bool originalCasualMode = NativeUIProjection.CasualPlayMode;
try
{
    foreach (bool casual in new[] { false, true })
    {
        NativeUIProjection.CasualPlayMode = casual;
        foreach (string option in new[] {
            "HAS_SEEN_COLLECTIONMANAGER", "HAS_SEEN_COLLECTIONMANAGER_AFTER_PRACTICE", "HAS_STARTED_A_DECK",
            "HAS_SEEN_PACK_OPENING", "HAS_OPENED_BOOSTER", "HAS_SEEN_SET_FILTER_TUTORIAL",
            "HAS_SEEN_MULLIGAN", "HAS_SEEN_THE_COIN", "HAS_SEEN_STEALTH_TAUNTER"
        })
        {
            Check(NativeUIProjection.TryGetOnboardingOption(true, option, out bool seen) && seen,
                option + " reads seen in " + (casual ? "Casual" : "Ranked"));
            Check(!NativeUIProjection.TryGetOnboardingOption(false, option, out _), option + " preserves non-isolated reads");
        }
        Check(NativeUIProjection.TryGetOnboardingOption(true, "IN_RANKED_PLAY_MODE", out bool ranked) && ranked == !casual
            && !NativeUIProjection.TryGetOnboardingOption(false, "IN_RANKED_PLAY_MODE", out _),
            "ranked option follows mode only in isolated launches");
    }
}
finally { NativeUIProjection.CasualPlayMode = originalCasualMode; }
Check(NativeUIProjection.FormatPickerMode(new[] { "WILD_BUTTON_CLICKED" }, new[] { "Button", "StandardRow" }) == "WILD"
    && NativeUIProjection.FormatPickerMode(new string[0], new[] { "Clickable", "CasualButton", "Buttons" }) == "CASUAL"
    && NativeUIProjection.FormatPickerMode(new[] { "STANDARD_BUTTON_CLICKED" }, new[] { "Clickable" }) == "STANDARD"
    && NativeUIProjection.FormatPickerMode(new[] { "STANDARD_CLICKED", "WILD_CLICKED" }, new[] { "Wild" }) == null
    && NativeUIProjection.FormatPickerMode(new[] { "HIDE" }, new[] { "Clickable", "Standard_Wild_Group", "Classic" }) == "CLASSIC"
    && NativeUIProjection.FormatPickerMode(null, new[] { "Background" }) == null,
    "format picker buttons classify by their own events first, then the nearest single-mode name");
Check(!NativeUIProjection.IsDisabledFormatMode("STANDARD") && !NativeUIProjection.IsDisabledFormatMode("CASUAL")
    && !NativeUIProjection.IsDisabledFormatMode(null) && new[] { "WILD", "CLASSIC", "TWIST" }.All(NativeUIProjection.IsDisabledFormatMode),
    "Standard and Casual stay selectable; Wild, Classic and Twist are shown but disabled");
var dimmedColor = NativeUIProjection.DisabledFormatTint((1f, 0.8f, 0.6f, 0.9f), (0.2f, 0.2f, 0.2f, 1f));
var dimmedTint = NativeUIProjection.DisabledFormatTint(null, (1f, 1f, 1f, 0.5f));
Check(dimmedColor.HasValue && dimmedColor.Value.Property == "_Color"
    && Math.Abs(dimmedColor.Value.R - 0.45f) < 1e-5f && Math.Abs(dimmedColor.Value.G - 0.36f) < 1e-5f
    && Math.Abs(dimmedColor.Value.B - 0.27f) < 1e-5f && dimmedColor.Value.A == 0.9f
    && dimmedTint.HasValue && dimmedTint.Value.Property == "_TintColor" && dimmedTint.Value.R == 0.45f && dimmedTint.Value.A == 0.5f
    && NativeUIProjection.DisabledFormatTint(null, null) == null,
    "disabled format buttons dim their own material colour (alpha kept), never compounding");
// Review 2026-10-28: the deck picker's InitAssets forces SetInRankedPlayMode(true) on every load; only a picker switch may
// change the mode, every other caller follows the remembered one.
NativeUIProjection.CasualPlayMode = true;
NativeUIProjection.PickerSwitchActive = false;
bool kept = !NativeUIProjection.FollowRankedPlayMode(true) && NativeUIProjection.CasualPlayMode;
NativeUIProjection.PickerSwitchActive = true;
bool switched = NativeUIProjection.FollowRankedPlayMode(true) && !NativeUIProjection.CasualPlayMode && !NativeUIProjection.PickerSwitchActive;
bool followsAfter = NativeUIProjection.FollowRankedPlayMode(false) && !NativeUIProjection.CasualPlayMode;
NativeUIProjection.CasualPlayMode = false;
Check(kept && switched && followsAfter, "non-picker ranked-mode writes follow the remembered mode; a picker switch adopts its request once");
Check(!NativeUIProjection.TryGetOnboardingOption(false, "HAS_SEEN_MULLIGAN", out _)
    && !NativeUIProjection.TryGetOnboardingOption(false, "HAS_SEEN_THE_COIN", out _)
    && !NativeUIProjection.TryGetOnboardingOption(false, "HAS_SEEN_STEALTH_TAUNTER", out _)
    && !NativeUIProjection.TryGetOnboardingOption(true, "HEARTHSTONE_DUOS_PORTAL_TUTORIAL", out _)
    && !NativeUIProjection.TryGetOnboardingOption(true, "HAS_SEEN_LOOT_IN_GAME_MULLIGAN_1_VO", out _),
    "match onboarding projection preserves non-isolated and unsupported-mode hints");
Check(!NativeUIProjection.TryGetOnboardingOption(false, "HAS_SEEN_COLLECTIONMANAGER", out _)
    && !NativeUIProjection.TryGetOnboardingOption(true, "SHOW_SWITCH_TO_WILD_ON_PLAY_SCREEN", out _)
    && !NativeUIProjection.TryGetOnboardingOption(true, "SOUND", out _)
    && !NativeUIProjection.TryGetOnboardingOption(true, null, out _),
    "onboarding projection preserves inactive, gameplay, unrelated and unknown options");
EndpointZeroTagsProjectionTests.Run();
Console.WriteLine("PROJECTION TESTS PASS");
class Publisher { private void OnNetCacheObjReceived<T>(T value) {} }
class NoPublisher {}

class DeleteCallbackOwner { public void Delete(object value) {} public void Other(object value) {} }
