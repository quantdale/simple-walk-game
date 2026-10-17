using System;
using System.Collections.Generic;

namespace WalkGame.Application.Content
{
    /// <summary>
    /// D-165 (OpenSpec add-interactive-world-presentation): the inhabitants of Millbrook Valley.
    /// D-172: the cast is Mira, Tavi, Rowan and Eli (bible §12 candidate cast; may change after art approval).
    /// Characters appear at places as restoration opens them up and say a few short lines keyed
    /// to restoration milestones. Pure projection of project completion, like Nell's letters
    /// (D-118): no save field, no content-version change, and nothing in progression reads it,
    /// so a player who never opens a conversation progresses exactly like one who reads them all.
    /// Copy is calm and brief (≤ <see cref="MaxLineLength"/>); every line can be skipped and revisited.
    /// Portrait and expression keys are style-neutral binding keys for whatever art direction
    /// Michael approves (D-164); no art exists yet.
    /// </summary>
    public static class Region1Characters
    {
        public const int MaxLineLength = 140;

        /// <summary>Expression keys a line may use (style-neutral).</summary>
        public static readonly IReadOnlyList<string> Expressions = new[] { "neutral", "glad", "wistful", "curious", "proud", "worried" };

        public sealed class Appearance
        {
            public Appearance(string? afterProjectId, string locationId) { AfterProjectId = afterProjectId; LocationId = locationId; }
            /// <summary>The project whose completion brings the character here; null = from the start.</summary>
            public string? AfterProjectId { get; }
            public string LocationId { get; }
        }

        public sealed class Line
        {
            public Line(string id, int order, string? milestoneProjectId, string expression, string text)
            { Id = id; Order = order; MilestoneProjectId = milestoneProjectId; Expression = expression; Text = text; }
            public string Id { get; }
            /// <summary>Strictly ascending per character; the highest reached line is the current one.</summary>
            public int Order { get; }
            /// <summary>The project completion that makes this line available; null = baseline line.</summary>
            public string? MilestoneProjectId { get; }
            public string Expression { get; }
            /// <summary>Authored English (localizable via ContentText, <c>&lt;id&gt;#line</c>).</summary>
            public string Text { get; }
        }

        public sealed class Character
        {
            public Character(string id, string name, IReadOnlyList<Appearance> appearances, IReadOnlyList<Line> lines)
            { Id = id; Name = name; Appearances = appearances; Lines = lines; }
            public string Id { get; }
            public string Name { get; }
            public string PortraitKey => "portrait." + Id;
            /// <summary>In order; the last reached appearance is where the character is now.</summary>
            public IReadOnlyList<Appearance> Appearances { get; }
            public IReadOnlyList<Line> Lines { get; }
        }

        private static Line L(string id, int order, string? milestone, string expression, string text) =>
            new Line(id, order, milestone, expression, text);

        // D-172: the on-screen cast follows Michael's Creative Direction Bible v1 §12 (candidate cast;
        // names, looks and lines can change after the Phase B round 2 art approval, D-171).
        // Nell Hartley stays the off-screen letter writer (Region1Letters, unchanged). Arlo stays a
        // figure from her letters; Tavi was his apprentice. Speaking styles (bible §12):
        //   Mira: hopeful, curious, practical, a little impatient with broken infrastructure.
        //   Tavi: energetic, quick, delighted by machines that work again.
        //   Rowan: gentle, unhurried, attentive to plants and slow ecological recovery.
        //   Eli: observant, precise, curious about wildlife, history and change over time.
        public static readonly IReadOnlyList<Character> All = new List<Character>
        {
            // Trailkeeper: the Old Trailhead, its paths and bridges (Phase D slice character).
            new Character("npc.mira", "Mira",
                new[] { new Appearance(null, "lm.trailhead") },
                new[]
                {
                    L("line.mira.1", 1, null, "curious", "Oh, a walker! Mind the signpost, it fell over again. The trail's under this brush somewhere. Help me find it?"),
                    L("line.mira.2", 2, "proj.clear-trailhead", "worried", "The high trail's just across the river. Shame the old bridge gave up before we did."),
                    L("line.mira.3", 3, "proj.rebuild-trail-bridges", "glad", "Look at that! The high trail is ours again."),
                    L("line.mira.4", 4, "proj.open-lookout", "proud", "Lookout's open. From up there the whole valley fits in one look. Smaller than I remembered, and easier to love."),
                    L("line.mira.5", 5, "proj.wetland-boardwalk", "glad", "Boardwalk's done. Every path in Millbrook joins up now, and not one plank wobbles. I checked them all."),
                }),
            // Tinkerer: the river intake and waterworks, then the settlement workshop.
            new Character("npc.tavi", "Tavi",
                new[]
                {
                    new Appearance("proj.clear-trailhead", "lm.river-intake"),
                    new Appearance("proj.build-workshop", "lm.settlement"),
                },
                new[]
                {
                    L("line.tavi.1", 1, "proj.clear-trailhead", "curious", "Hi! I'm Tavi. The intake's jammed solid with silt and branches. Get me some help and I'll have water moving!"),
                    L("line.tavi.2", 2, "proj.river-intake", "glad", "Hear that? Water through the gates again! Old gears, new grease. The whole valley just took a breath."),
                    L("line.tavi.3", 3, "proj.clear-reservoir", "proud", "Reservoir's holding! Not one leak. Okay, one tiny leak. Fixed it. Next stop: pipes all the way to the square."),
                    L("line.tavi.4", 4, "proj.lay-water-lines", "glad", "Water lines reach the hub now. Turn a tap in the square and the river answers. Still feels like magic to me."),
                    L("line.tavi.5", 5, "proj.build-workshop", "wistful", "Arlo taught me at this bench before the floods. His gate hinge was still here. I lit the forge for him."),
                    L("line.tavi.6", 6, "proj.restore-market-hall", "glad", "The market hall has a roof! Folks keep bringing me broken things to mend. Best problem I've ever had."),
                    L("line.tavi.7", 7, "proj.wire-settlement-power", "proud", "Lights in the square tonight! The mill race spins the generator and every window glows. It looks like home."),
                }),
            // Gardener: the Canopy Grove woodland, the East Wetland while it heals, then back to the grove.
            new Character("npc.rowan", "Rowan",
                new[]
                {
                    new Appearance("proj.clear-fallen-timber", "lm.canopy"),
                    new Appearance("proj.wetland-drainage", "lm.wetland"),
                    new Appearance("proj.canopy-walkway", "lm.canopy"),
                },
                new[]
                {
                    L("line.rowan.1", 1, "proj.clear-fallen-timber", "wistful", "The fallen trunks are gone. Underneath, the soil is still alive. Old woods are patient, if you're gentle with them."),
                    L("line.rowan.2", 2, "proj.plant-woodland-understory", "glad", "Ferns, hazel, little oaks. They're small for now, but listen. The birds already know they're here."),
                    L("line.rowan.3", 3, "proj.wetland-drainage", "worried", "The water is finding its old channels. It's muddy now, but give it a season and the marsh will remember itself."),
                    L("line.rowan.4", 4, "proj.replant-native-sedges", "glad", "Sedges! Look how green the bank is. The frogs found them before I did."),
                    L("line.rowan.5", 5, "proj.build-nesting-islets", "glad", "A pair of cranes landed on the islets this morning. I didn't dare breathe."),
                    L("line.rowan.6", 6, "proj.canopy-walkway", "proud", "The walkway joins the crowns now. Walk softly up there. The grove is awake, and glad of the company."),
                }),
            // Naturalist: the Ridge Observatory and the valley survey.
            new Character("npc.eli", "Eli",
                new[] { new Appearance("proj.open-lookout", "lm.observatory") },
                new[]
                {
                    L("line.eli.1", 1, "proj.open-lookout", "curious", "Hello. I'm Eli. I note what lives here. The dome is rusted shut, but from the ridge I counted three kinds of finch."),
                    L("line.eli.2", 2, "proj.refit-observatory-dome", "glad", "The dome turns again. I've moved my maps up here. Come and look when you pass; the valley changes week by week."),
                    L("line.eli.3", 3, "proj.calibrate-survey-rig", "curious", "Instruments aligned. The old surveyors marked every bend of the river a century ago. Their lines still hold."),
                    L("line.eli.4", 4, "proj.complete-valley-survey", "glad", "Every mile of Millbrook is on the map now. Every one of them is a walk you took."),
                }),
        }.AsReadOnly();

        /// <summary>The character with this id, or null.</summary>
        public static Character? Find(string characterId)
        {
            foreach (var c in All) if (string.Equals(c.Id, characterId, StringComparison.Ordinal)) return c;
            return null;
        }
    }
}
