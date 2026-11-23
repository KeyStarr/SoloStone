using System;

// How long the NPC waits before it answers a choice the client shows as card backs (#162, #238).
// A decision battlecry such as Elise the Navigator answers in several steps, and the client runs the card's one shared play
// spell again for each step. If an answer lands while the spell of the step before is still alive, the client restarts the
// spell on top of itself and a fullscreen part of it never ends. So the NPC waits, in this order:
//  1. the deal: the client has dealt the backs, or its power queue has been idle for DealWaitMilliseconds
//     without interruption (a busy client cannot deal and resets the idle wait);
//  2. the source's play spell: it has ended, at most SpellWaitMilliseconds;
//  3. the beat: a human-plausible look at the dealt backs, counted from the deal.
// Steps 1 and 2 share one hard cap (WaitCapMilliseconds) from the first time the pick is ready, and end early when
// the turn's deadline is near; the beat is skipped when it would end within BeatDeadlineMarginMilliseconds of the deadline. The beat is a floor: it does not come out of the runner's 4 s artificial-delay budget
// (NpcTurnRunner; owner call: all opposing choices get the floor), and it never exceeds the beat itself, so every hold is bounded.
internal sealed class OpposingChoicePacing
{
    internal const double DealWaitMilliseconds = 3000;
    internal const double SpellWaitMilliseconds = 4000;
    internal const double WaitCapMilliseconds = 10000;
    // Steps 1 and 2 stop this long before the turn's own deadline.
    internal const double DeadlineMarginMilliseconds = 5000;

    // The beat is skipped when it would end less than this before the turn's deadline: the NPC's own pick must reach the
    // engine, not the timer's default answer.
    internal const double BeatDeadlineMarginMilliseconds = 500;

    // Decision battlecries (minions, heroes and weapons) close PLAY/POWER blocks across choices; each answer opens a new
    // POWER block and can restart the shared play spell. Ordinary Discover keeps its block open and must not wait for it.
    private double started = -1, idleSince = -1, spellSince = -1, spellWaited;

    internal bool Started => started >= 0;
    // Set once, when the hold first returns 0: deal (dealt, nothing else to wait for), spell (the source's play spell ended
    // while held), cap (a wait ran out: deal 3 s, spell 4 s or the shared 10 s), deadline (the turn's deadline is near).
    internal string Released { get; private set; }
    // From the first ready pick to the release, and the part of it spent waiting for the source's play spell.
    internal double WaitedMilliseconds { get; private set; }
    internal double SpellWaitedMilliseconds => spellWaited;

    // Milliseconds to keep holding the pick; 0 releases it. revealedAt < 0: not dealt yet. beat: the look at the backs.
    // sourceSpellActive may be null (nothing to probe) and may throw (counts as ended). deadlineLeft may be infinity.
    internal double Hold(double now, double revealedAt, double beat, bool powerIdle, bool sourceIsDecisionBattlecry, Func<bool> sourceSpellActive, double deadlineLeft)
    {
        double hold = Step(now, revealedAt, beat, powerIdle, sourceIsDecisionBattlecry, sourceSpellActive, deadlineLeft);
        if (hold <= 0 && Released == null) { Released = releaseReason ?? "deal"; WaitedMilliseconds = now - started; }
        return hold;
    }

    private string releaseReason;
    internal bool Logged { get; set; }

    // The runner stopped asking (concede, game end, a human choice, the turn timer) before the hold released.
    internal void Abandon(double now)
    {
        if (started < 0 || Released != null) return;
        Released = "canact"; WaitedMilliseconds = now - started;
    }

    private double Step(double now, double revealedAt, double beat, bool powerIdle, bool sourceIsDecisionBattlecry, Func<bool> sourceSpellActive, double deadlineLeft)
    {
        if (started < 0) started = now;
        bool nearDeadline = deadlineLeft <= DeadlineMarginMilliseconds, capped = now - started >= WaitCapMilliseconds;
        bool waiting = !capped && !nearDeadline;
        if (waiting && revealedAt < 0)
        {
            if (!powerIdle) { idleSince = -1; return 100; }
            if (idleSince < 0) idleSince = now;
            if (now - idleSince < DealWaitMilliseconds) return 100;
            releaseReason = "cap";
        }
        if (waiting && sourceIsDecisionBattlecry && SpellActive(sourceSpellActive))
        {
            if (spellSince < 0) spellSince = now;
            spellWaited = now - spellSince;
            if (spellWaited < SpellWaitMilliseconds) return 100;
            releaseReason = "cap";
        }
        else if (spellSince >= 0 && releaseReason == null) { spellWaited = Math.Max(spellWaited, now - spellSince); releaseReason = "spell"; }
        if (!waiting) releaseReason = nearDeadline ? "deadline" : "cap";
        // The beat only follows a deal; an undealt choice that timed out goes at once, as before.
        if (revealedAt < 0) return 0;
        double left = Math.Max(0, beat - (now - revealedAt));
        if (left > 0 && deadlineLeft - left < BeatDeadlineMarginMilliseconds) { releaseReason = "deadline"; return 0; }
        return left;
    }

    private static bool SpellActive(Func<bool> probe)
    {
        if (probe == null) return false;
        try { return probe(); } catch (Exception) { return false; }
    }
}
