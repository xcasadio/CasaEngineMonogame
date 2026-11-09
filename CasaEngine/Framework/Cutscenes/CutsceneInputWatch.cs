using System.Collections;
using CasaEngine.Engine.Input;
using CasaEngine.Framework.Input;

namespace CasaEngine.Framework.Cutscenes;

/// <summary>
/// The coroutine armed by an ExitOnInput action. It owns an <see cref="InputMappingManager"/> limited to the listed
/// inputs, so it does not collide with the mappings of the game, and updates it every frame from the devices it was
/// given. The first update after the arming only records the state of the inputs: a button held when the watch is armed
/// triggers only after a release and a new press. Its step does not allocate.
/// </summary>
internal sealed class CutsceneInputWatch
{
    private readonly CutsceneDirector _director;
    private readonly InputMappingManager _mappings = new();
    private readonly InputMapping[] _inputs;
    private readonly KeyboardManager _keyboard;
    private readonly MouseManager _mouse;
    private readonly GamePadManager _gamePad;
    private readonly CutsceneAsset _next;

    /// <param name="inputs">The mappings to watch. A mapping listed twice is updated once.</param>
    public CutsceneInputWatch(
        CutsceneDirector director,
        IReadOnlyList<InputMapping> inputs,
        KeyboardManager keyboard,
        MouseManager mouse,
        GamePadManager gamePad,
        CutsceneAsset next)
    {
        _director = director ?? throw new ArgumentNullException(nameof(director));
        ArgumentNullException.ThrowIfNull(inputs);
        _keyboard = keyboard ?? throw new ArgumentNullException(nameof(keyboard));
        _mouse = mouse ?? throw new ArgumentNullException(nameof(mouse));
        _gamePad = gamePad ?? throw new ArgumentNullException(nameof(gamePad));
        _next = next ?? throw new ArgumentNullException(nameof(next));

        var distinct = new List<InputMapping>(inputs.Count);
        for (int index = 0; index < inputs.Count; index++)
        {
            if (!distinct.Contains(inputs[index]))
            {
                distinct.Add(inputs[index]);
                _mappings.AddInputMapping(inputs[index]);
            }
        }

        _inputs = distinct.ToArray();
    }

    /// <summary>The watch itself: one step per frame, for the rest of the play.</summary>
    public IEnumerator Run()
    {
        // First update: records the state only (InputMapping.Update copies Pressed to PressedPreviousFrame first).
        _mappings.Update(_keyboard, _mouse, _gamePad);
        yield return null;

        while (true)
        {
            _mappings.Update(_keyboard, _mouse, _gamePad);
            for (int index = 0; index < _inputs.Length; index++)
            {
                InputMapping input = _inputs[index];
                if (input.Pressed && !input.PressedPreviousFrame)
                {
                    // Play stops the play in progress, this watch included: nothing runs after it.
                    _director.Play(_next);
                    yield break;
                }
            }

            yield return null;
        }
    }
}
