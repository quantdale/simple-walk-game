using System;
using System.Collections.Generic;
using WalkGame.Application.ReadModels;
using WalkGame.Domain.Projects;

namespace WalkGame.Shell.Presentation
{
    // KI-027(d) (OpenSpec extend-unity-presentation-seam): characters in the world, taps on characters
    // and inspectable props, and dialogue actions (bible §13, §15, §16). Style-neutral like D-169: these
    // types name keys and outcomes, never a sprite, prefab, layout or animation.

    /// <summary>Hands out one view per character. Optional: a world presenter without characters simply draws none.</summary>
    public interface ICharacterPresenter
    {
        /// <summary>The view for a character, or null when this presenter has none (the character is simply not drawn).</summary>
        ICharacterView? FindCharacter(string characterId);
    }

    /// <summary>One character in the world (bible §11–§13). Placement anchors are presentation data, never save data.</summary>
    public interface ICharacterView
    {
        string CharacterId { get; }
        /// <summary>Called on every refresh: where the character is (or that they are absent) and their mood.</summary>
        void Show(CharacterPresentation character);
        /// <summary>A short visible reaction. Fire-and-forget; nothing waits for it.</summary>
        void React(CharacterReaction reaction);
    }

    public enum CharacterReaction
    {
        /// <summary>The player tapped the character (bible §15 step 1: the NPC reacts visibly).</summary>
        Tapped = 0,
        /// <summary>A visible change just reached the character (new line after a restoration; bible §13 acknowledgement).</summary>
        Acknowledge = 1,
    }

    /// <summary>What a character view needs. Keys come from the D-166 character read model.</summary>
    public sealed class CharacterPresentation
    {
        /// <summary>Expression used when the character has no current line.</summary>
        public const string DefaultExpression = "neutral";

        public CharacterPresentation(string characterId, string name, string? locationId, string portraitKey,
            string? currentLineId, string expression, bool hasPendingLine)
        {
            CharacterId = characterId; Name = name; LocationId = locationId; PortraitKey = portraitKey;
            CurrentLineId = currentLineId; Expression = string.IsNullOrEmpty(expression) ? DefaultExpression : expression;
            HasPendingLine = hasPendingLine;
        }

        public string CharacterId { get; }
        public string Name { get; }
        /// <summary>The place the character stands now; null while they have not arrived (hide the view).</summary>
        public string? LocationId { get; }
        public bool IsPresent => LocationId != null;
        public string PortraitKey { get; }
        public string? CurrentLineId { get; }
        /// <summary>Mood from progress: the expression of the character's current (highest reached) line.</summary>
        public string Expression { get; }
        /// <summary>The current line has not been marked seen (a "has something to say" cue).</summary>
        public bool HasPendingLine { get; }

        /// <summary>In-world sprite/rig set, e.g. <c>npc.mira</c>.</summary>
        public string SpriteKey => CharacterId;
        /// <summary>In-world mood variant, e.g. <c>npc.mira@worried</c>; falls back to <see cref="SpriteKey"/>.</summary>
        public string MoodKey => CharacterId + "@" + Expression;
        /// <summary>Portrait for the current expression, e.g. <c>portrait.npc.mira@glad</c>; falls back to <see cref="PortraitKey"/>.</summary>
        public string PortraitExpressionKey => PortraitKey + "@" + Expression;

        internal static CharacterPresentation From(CharacterDialogueReadModel c, ICollection<string> seen)
        {
            var line = c.CurrentLine;
            return new CharacterPresentation(c.CharacterId, c.Name, c.LocationId, c.PortraitKey, line?.LineId,
                line?.Expression ?? DefaultExpression, line != null && !seen.Contains(line.LineId));
        }
    }

    /// <summary>Shows a close look at a place or one of its projects (a prop such as the damaged bridge). Read-only.</summary>
    public interface IInspectPresenter
    {
        void ShowInspect(InspectCard card);
        void HideInspect();
    }

    /// <summary>What an inspect card shows. Built from canonical read models; opening it changes nothing.</summary>
    public sealed class InspectCard
    {
        public InspectCard(string locationId, string locationTitle, string landmarkVariantKey, string? projectId,
            string? projectTitle, ProjectStatus? projectStatus, long vitalityInvested, long vitalityCost,
            int constructionStage, int constructionStages, RestorationChoicesReadModel.Choice? choice)
        {
            LocationId = locationId; LocationTitle = locationTitle; LandmarkVariantKey = landmarkVariantKey;
            ProjectId = projectId; ProjectTitle = projectTitle; ProjectStatus = projectStatus;
            VitalityInvested = vitalityInvested; VitalityCost = vitalityCost;
            ConstructionStage = constructionStage; ConstructionStages = constructionStages; Choice = choice;
        }

        public string LocationId { get; }
        public string LocationTitle { get; }
        public string LandmarkVariantKey { get; }
        /// <summary>The inspected project (prop), or null for a card about the place alone.</summary>
        public string? ProjectId { get; }
        public string? ProjectTitle { get; }
        public ProjectStatus? ProjectStatus { get; }
        public long VitalityInvested { get; }
        public long VitalityCost { get; }
        /// <summary>Visible build stage while the project is active (0 otherwise), of <see cref="ConstructionStages"/>.</summary>
        public int ConstructionStage { get; }
        public int ConstructionStages { get; }
        /// <summary>The project's restoration choice (previews, consequences) while it can be picked or is underway; else null.</summary>
        public RestorationChoicesReadModel.Choice? Choice { get; }
    }

    public enum DialogueActionKind
    {
        /// <summary>Look closely at the place's focus project (bible §16 "Inspect the bridge").</summary>
        Inspect = 0,
        /// <summary>Open the project card for the place's goal; the card's start action picks it (bible §16).</summary>
        HelpRestore = 1,
        /// <summary>Close and return to the world (bible §15 step 6).</summary>
        MaybeLater = 2,
    }

    /// <summary>One action offered under a line. Label through <see cref="PlayerStrings"/> (<see cref="LabelKey"/>).</summary>
    public sealed class DialogueAction
    {
        public DialogueAction(DialogueActionKind kind, string locationId, string? projectId)
        { Kind = kind; LocationId = locationId; ProjectId = projectId; }

        public DialogueActionKind Kind { get; }
        public string LocationId { get; }
        public string? ProjectId { get; }

        public string LabelKey => Kind switch
        {
            DialogueActionKind.Inspect => "dialogue.action.inspect",
            DialogueActionKind.HelpRestore => "dialogue.action.helpRestore",
            _ => "dialogue.action.maybeLater",
        };

        public override string ToString() => Kind + ":" + LocationId + ":" + ProjectId;
    }

    public enum WorldTapTargetKind
    {
        Landmark = 0,
        Character = 1,
        /// <summary>An inspectable prop bound to a project (e.g. the damaged bridge).</summary>
        Prop = 2,
    }

    public enum TapOutcome
    {
        /// <summary>Nothing there, decoration, an absent character or a locked place: nothing opens.</summary>
        Ignored = 0,
        Opened = 1,
    }

    /// <summary>One tappable area in screen space (any units, y down or up; the router only compares).</summary>
    public readonly struct WorldHitArea
    {
        public WorldHitArea(WorldTapTargetKind kind, string id, float x, float y, float width, float height, int sortOrder)
        { Kind = kind; Id = id; X = x; Y = y; Width = width; Height = height; SortOrder = sortOrder; }

        public WorldTapTargetKind Kind { get; }
        public string Id { get; }
        public float X { get; }
        public float Y { get; }
        public float Width { get; }
        public float Height { get; }
        /// <summary>Higher is in front. Ties go to the area registered later.</summary>
        public int SortOrder { get; }
    }

    /// <summary>
    /// Physics-free tap hit-testing (Phase A audit §4.6): areas resolved front to back, each grown to at
    /// least the minimum touch target around its centre (56 dp rule, UX_DESIGN §14). Decoration is never
    /// registered, so it can never capture a tap (bible §10).
    /// </summary>
    public sealed class WorldTapRouter
    {
        private readonly List<WorldHitArea> _areas = new List<WorldHitArea>();
        private readonly float _minTarget;

        /// <param name="minTouchTarget">Minimum side of a hit area in the same units as the areas (e.g. 56 dp in pixels).</param>
        public WorldTapRouter(float minTouchTarget) { _minTarget = Math.Max(0f, minTouchTarget); }

        public IReadOnlyList<WorldHitArea> Areas => _areas;
        public void Clear() => _areas.Clear();
        public void Add(WorldHitArea area) { if (!string.IsNullOrEmpty(area.Id)) _areas.Add(area); }

        /// <summary>The front-most area containing the point, or null.</summary>
        public WorldHitArea? HitTest(float x, float y)
        {
            WorldHitArea? best = null;
            for (int i = 0; i < _areas.Count; i++)
            {
                var a = _areas[i];
                float w = Math.Max(a.Width, _minTarget), h = Math.Max(a.Height, _minTarget);
                float left = a.X + a.Width / 2f - w / 2f, top = a.Y + a.Height / 2f - h / 2f;
                if (x < left || x > left + w || y < top || y > top + h) continue;
                if (best == null || a.SortOrder >= best.Value.SortOrder) best = a;
            }
            return best;
        }

        /// <summary>Hit-tests and forwards the tap to the controller. A miss opens nothing.</summary>
        public TapOutcome Route(WorldPresentationController controller, float x, float y)
        {
            if (controller == null) throw new ArgumentNullException(nameof(controller));
            var hit = HitTest(x, y);
            if (hit == null) return TapOutcome.Ignored;
            switch (hit.Value.Kind)
            {
                case WorldTapTargetKind.Character: return controller.TapCharacter(hit.Value.Id);
                case WorldTapTargetKind.Prop: return controller.TapProp(hit.Value.Id);
                default: return controller.TapLandmark(hit.Value.Id) == LandmarkTapOutcome.Opened ? TapOutcome.Opened : TapOutcome.Ignored;
            }
        }
    }
}
