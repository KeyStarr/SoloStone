using System;

/// <summary>Client-free decisions behind NativeTurnBannerHold: which start-of-turn task lists wait for the "Your Turn" banner,
/// and for how long. The client's own trigger controller shows that banner and waits for it; the Sigil, Objective, Sidequest
/// and Secret controllers do not, so their trigger animations would play under the banner (#146).</summary>
internal static class TurnBannerHoldRules
{
    internal const int SideQuestKeyword = 1192, SigilKeyword = 1749, ObjectiveKeyword = 2311;
    /// <summary>The banner shows for about two seconds; past this the list goes ahead whatever the banner does.</summary>
    internal const double TimeoutSeconds = 4.0;

    /// <summary>True for a TRIGGER block start whose client controller ignores the banner: keyword Sigil, Objective or Sidequest,
    /// or a Secret source. The client routes Objectives by keyword 2311 only; our keyword-less Objective blocks reach its generic
    /// trigger controller, which already waits for the banner, so they are not held here.</summary>
    internal static bool IgnoresBanner(bool triggerBlockStart, int keyword, bool sourceIsSecret)
    {
        if (!triggerBlockStart) return false;
        return keyword == SigilKeyword || keyword == ObjectiveKeyword || keyword == SideQuestKeyword
            || sourceIsSecret;
    }

    /// <summary>Holds one task list at a time. Once a list has been released (banner gone, or timeout) it is never held again.</summary>
    internal sealed class Hold
    {
        private object list;
        private double since;
        private bool released;
        internal bool Holding { get; private set; }
        internal bool Released => released;
        internal double Since => since;
        internal string LastRelease { get; private set; }
        internal double LastWaited { get; private set; }

        /// <summary>True while the queue should wait. Call after asking the client to show the banner.</summary>
        internal bool ShouldHold(object head, bool bannerShowing, double now)
        {
            Holding = false;
            if (head == null) return false;
            if (!ReferenceEquals(head, list)) { list = head; since = now; released = false; }
            if (released) return false;
            if (!bannerShowing) return Release("banner-hidden", now);
            if (now - since >= TimeoutSeconds || now < since) return Release("timeout", now);
            Holding = true;
            return true;
        }

        internal void AcknowledgeRelease() { LastRelease = null; }

        private bool Release(string reason, double now)
        {
            released = true; LastRelease = reason; LastWaited = now - since;
            return false;
        }
    }
}
