using System.Collections.Generic;

namespace Wendao.Systems.Tutorial
{
    public interface ITutorialService
    {
        bool IsActive { get; }
        string ActiveTutorialId { get; }
        string ActiveStepId { get; }
        bool IsForced { get; }
        IReadOnlyList<string> KnownTutorialIds { get; }

        bool TryStart(string tutorialId);
        bool RequestStart(string tutorialId);
        void CompleteStep(string stepId);
        void Complete(string tutorialId);
        bool DismissCurrent();
        void Skip();
        bool HasCompleted(string tutorialId);
        bool AllowsInput(TutorialInputAction action);
        void RepublishActivePrompt();
    }
}
