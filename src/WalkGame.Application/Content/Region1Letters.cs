using System;
using System.Collections.Generic;

namespace WalkGame.Application.Content
{
    /// <summary>
    /// D-118: optional character vignettes. Nell Hartley grew up in Millbrook and left after the
    /// floods; her letters to her brother Tom turn up at landmarks as they are restored. Each
    /// letter is tied to ONE project's completion, so it is a pure projection of existing
    /// restoration state: no save field, no content-fingerprint change, nothing to miss.
    /// Copy is presentation-only (like D-086 discovery copy) and is calm, with no guilt or pressure.
    /// </summary>
    public static class Region1Letters
    {
        public sealed class Letter
        {
            public Letter(string id, int order, string projectId, string placeKey, string body)
            { Id = id; Order = order; ProjectId = projectId; PlaceKey = placeKey; Body = body; }
            public string Id { get; }
            public int Order { get; }
            /// <summary>The project whose completion reveals the letter at its site.</summary>
            public string ProjectId { get; }
            /// <summary>Where it was found, in player words.</summary>
            public string PlaceKey { get; }
            public string Body { get; }
        }

        public const string Writer = "Nell Hartley";
        public const int MaxBodyLength = 320;

        public static readonly IReadOnlyList<Letter> All = new List<Letter>
        {
            new Letter("letter.trailhead", 1, "proj.clear-trailhead", "the old trailhead",
                "Tom, we left the millstone by the trail so the way in would not be forgotten. I keep picturing someone finding it one day and wondering who we were. If they do, I hope they stay a while."),
            new Letter("letter.intake", 2, "proj.river-intake", "the river intake",
                "Dad counted every flood season the gates held. He never blamed the river. He said it only wanted room, and that one day someone would give it some and the valley would breathe again."),
            new Letter("letter.workshop", 3, "proj.build-workshop", "the settlement workshop",
                "The hinge for the gate is still on old Arlo's bench, Tom. He would not take payment until we came home. I like to think it is waiting there for whoever lights the forge next."),
            new Letter("letter.wetland", 4, "proj.wetland-drainage", "the east wetland",
                "Do you remember the cranes dancing at dawn? Mum swore they would come back if the marsh was ever left to be a marsh. I believe her more each year."),
            new Letter("letter.canopy", 5, "proj.canopy-walkway", "the canopy walkway",
                "I tied a ribbon on the big oak the day we left, for luck. If it is still there, it held. Things in this valley hold on longer than you would think."),
            new Letter("letter.survey", 6, "proj.complete-valley-survey", "the valley survey baseline",
                "Last one for now. Whoever finds these, thank you for walking Millbrook back to life, a little at a time. There is no hurry. The valley will be glad of you whenever you come."),
        }.AsReadOnly();
    }
}
