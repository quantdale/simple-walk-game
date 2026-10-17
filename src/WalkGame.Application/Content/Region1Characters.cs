using System;
using System.Collections.Generic;

namespace WalkGame.Application.Content
{
    /// <summary>
    /// D-165 (OpenSpec add-interactive-world-presentation): the inhabitants of Millbrook Valley.
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

        public static readonly IReadOnlyList<Character> All = new List<Character>
        {
            new Character("npc.wren", "Wren",
                new[]
                {
                    new Appearance(null, "lm.trailhead"),
                    new Appearance("proj.refit-observatory-dome", "lm.observatory"),
                },
                new[]
                {
                    L("line.wren.1", 1, null, "curious", "Oh! Another walker. I thought I was the only one who still came this way. The path is buried, but it's here."),
                    L("line.wren.2", 2, "proj.clear-trailhead", "glad", "You cleared the gate. I can see the old trail markers again, all the way down to the river."),
                    L("line.wren.3", 3, "proj.open-lookout", "proud", "From the lookout you can see the whole valley. It looks smaller from up here, and easier to love."),
                    L("line.wren.4", 4, "proj.refit-observatory-dome", "curious", "I moved my maps up to the observatory. The dome turns again. Come and look when you pass."),
                    L("line.wren.5", 5, "proj.complete-valley-survey", "glad", "Every mile of Millbrook is on the map now. Every one of them is a walk you took."),
                }),
            new Character("npc.arlo", "Arlo",
                new[] { new Appearance("proj.build-workshop", "lm.settlement") },
                new[]
                {
                    L("line.arlo.1", 1, "proj.build-workshop", "wistful", "I said I'd come back when someone lit the forge. Didn't think I'd live to see it. Thank you."),
                    L("line.arlo.2", 2, "proj.restore-market-hall", "glad", "The market hall has a roof again. People are already asking where to set up their stalls."),
                    L("line.arlo.3", 3, "proj.wire-settlement-power", "proud", "Lights in the square tonight. The settlement looks like a home now."),
                }),
            new Character("npc.ida", "Ida",
                new[] { new Appearance("proj.wetland-drainage", "lm.wetland") },
                new[]
                {
                    L("line.ida.1", 1, "proj.wetland-drainage", "worried", "The water is finding its old channels. It's muddy now, but give it a season and the marsh will remember itself."),
                    L("line.ida.2", 2, "proj.replant-native-sedges", "glad", "Sedges! Look how green the bank is. The frogs found them before I did."),
                    L("line.ida.3", 3, "proj.build-nesting-islets", "glad", "A pair of cranes landed on the islets this morning. I didn't dare breathe."),
                    L("line.ida.4", 4, "proj.wetland-boardwalk", "proud", "With the boardwalk, anyone can come and watch without disturbing them. That's the best kind of path."),
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
