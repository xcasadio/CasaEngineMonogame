using CasaEngine.Framework.Cutscenes;

namespace CasaEngine.EditorServices.Cutscenes;

/// <summary>
/// A value of the file that the loader dropped or rewrote without a format error, so that a save would not write it
/// back as it was (ADR-0134). It blocks the save until the field is edited.
/// </summary>
/// <param name="Action">The action that holds the field; null for a root action that is not an object.</param>
/// <param name="Key">The JSON key of the field, <c>actions</c> for the list of children, <c>root_action</c> for the root.</param>
/// <param name="Path">The path of the action, in the format of the validator, such as <c>root_action.actions[2]</c>.</param>
/// <param name="Message">What differs between the file and the save.</param>
public sealed record CutsceneLoadLoss(CutsceneActionData? Action, string Key, string Path, string Message);
